// Folder: SiegeEngine/Core/GPU/Compute
// File: AcousticRayTracer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
namespace SiegeEngine.Core.GPU.Compute
{
    public unsafe class AcousticRayTracer : IDisposable
    {
        public enum DebugSegmentKind : int
        {
            FreeLeg = 0,      // listener-visible surface edges (blue)
            SourceFree = 1,   // source-visible surface edges (red)
            BounceLeg = 2,
            Splat = 3,        // unused
            Diffracted = 4    // mutual visibility (cyan)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GpuRayResult
        {
            public float Intensity;
            public float Delay;
            public float LowPass;
            public float Pad;
            public Vector4 ArrivalDir;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GpuDebugSegment
        {
            public Vector4 A;
            public Vector4 B;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GpuVisibility
        {
            public float ListenerVisible;
            public float SourceVisible;
            public float Pad0;
            public float Pad1;
        }

        public struct DebugSegment
        {
            public Vector3 A;
            public Vector3 B;
            public DebugSegmentKind Kind;
            public float Intensity;
            public float Radius;
            public Vector3 Normal;
            public int TriangleIndex;
        }

        private readonly IRenderContext _renderContext;
        private readonly ComputeProgram _freeRayProgram;
        private readonly ComputeProgram _residualProgram;
        private readonly ComputeProgram _visibilityProgram;
        private readonly ShaderStorageBuffer[] _resultSsbo = new ShaderStorageBuffer[2];
        private readonly ShaderStorageBuffer[] _debugSsbo = new ShaderStorageBuffer[2];
        private readonly ShaderStorageBuffer _visibilitySsbo;
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private int _writeIdx;
        private int _readIdx = 1;
        private const int MaxRays = 256;
        private const int ContinuousRays = 128;
        private const int ContinuousBounces = 6;
        private const int MaxDebugSegments = 1024;
        private const int MaxVisibilityTris = 8192;
        private readonly List<DebugSegment> _debugSegments = new List<DebugSegment>(MaxDebugSegments);
        private Vector3 _lastListenerPos;
        private Vector3 _lastPrimarySource;

        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _freeRayProgram = new ComputeProgram(_renderContext, AcousticFreeRayShader.Source);
            _residualProgram = new ComputeProgram(_renderContext, AcousticCommon.Source + AcousticResidualShader.Source);
            _visibilityProgram = new ComputeProgram(_renderContext, AcousticVisibilityShader.Source);

            int resultBytes = MaxRays * sizeof(GpuRayResult);
            int debugBytes = MaxDebugSegments * sizeof(GpuDebugSegment);
            int visBytes = MaxVisibilityTris * sizeof(GpuVisibility);

            for (int i = 0; i < 2; i++)
            {
                _resultSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _resultSsbo[i].SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
                _debugSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _debugSsbo[i].SetData((uint)debugBytes, null, _renderContext.Enums.DynamicCopy);
            }
            _visibilitySsbo = new ShaderStorageBuffer(_renderContext);
            _visibilitySsbo.SetData((uint)visBytes, null, _renderContext.Enums.DynamicCopy);
        }

        public void KickContinuousTrace(Vector3 sourcePos, Vector3 listenerPos)
        {
            if (_disposed || _geometry.TriangleCount <= 0) return;
            _geometry.Buffer.BindBase(0);
            _resultSsbo[_writeIdx].BindBase(1);
            _debugSsbo[_writeIdx].BindBase(2);
            _residualProgram.Use();
            _residualProgram.SetUniform("uSourcePos", sourcePos.X, sourcePos.Y, sourcePos.Z);
            _residualProgram.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _residualProgram.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _residualProgram.SetUniform("uRayCount", ContinuousRays);
            _residualProgram.SetUniform("uMaxBounces", ContinuousBounces);
            _residualProgram.SetUniform("uListenerRadius", 5.0f);
            _residualProgram.SetUniform("uMaxDistance", 350.0f);
            _residualProgram.SetUniform("uDebugMode", 0);
            _residualProgram.SetUniform("uSourceCount", 1);
            uint groups = (uint)((ContinuousRays + 63) / 64);
            _residualProgram.Dispatch(groups, 1, 1);
            _readIdx = _writeIdx;
            _writeIdx = 1 - _writeIdx;
        }

        public void KickDebugBidirectional(Vector3 listenerPos, IReadOnlyList<Vector3> sources, bool diagnosticOnce = false)
        {
            if (_disposed) return;
            Vector3 primarySource = (sources != null && sources.Count > 0) ? sources[0] : listenerPos + new Vector3(0, 10, 0);
            _lastListenerPos = listenerPos;
            _lastPrimarySource = primarySource;

            if (_geometry.TriangleCount <= 0)
            {
                _debugSegments.Clear();
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer] TriangleCount == 0, no dispatch");
                return;
            }

            if (diagnosticOnce)
            {
                Console.WriteLine($"[AcousticRayTracer] === KickDebug (GPU viewport) ===");
                Console.WriteLine($" Listener = ({listenerPos.X:F2},{listenerPos.Y:F2},{listenerPos.Z:F2})");
                Console.WriteLine($" Source = ({primarySource.X:F2},{primarySource.Y:F2},{primarySource.Z:F2})");
                Console.WriteLine($" TriangleCount = {_geometry.TriangleCount}");
            }

            // GPU parallel per-triangle viewport visibility
            int triCount = Math.Min(_geometry.TriangleCount, MaxVisibilityTris);
            _geometry.Buffer.BindBase(0);
            _visibilitySsbo.BindBase(1);

            _visibilityProgram.Use();
            _visibilityProgram.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _visibilityProgram.SetUniform("uSourcePos", primarySource.X, primarySource.Y, primarySource.Z);
            _visibilityProgram.SetUniform("uTriangleCount", triCount);
            _visibilityProgram.SetUniform("uMaxDistance", 350.0f);

            uint groups = (uint)((triCount + 63) / 64);
            _visibilityProgram.Dispatch(groups, 1, 1);
            _visibilityProgram.Barrier();

            // Read results and paint continuous surface edges
            _debugSegments.Clear();
            ReadVisibilityAndPaint(triCount, diagnosticOnce);

            // Direct LOS still shown if clear
            Vector3 toSource = primarySource - listenerPos;
            float dist = toSource.Length();
            if (dist > 1e-4f)
            {
                Vector3 dir = toSource / dist;
                bool losClear = false;
                if (_geometry.TryClosestHit(listenerPos, dir, out float tHit, out _, out _))
                {
                    if (tHit >= dist * 0.98f) losClear = true;
                }
                else losClear = true;

                if (losClear)
                {
                    _debugSegments.Add(new DebugSegment
                    {
                        A = listenerPos,
                        B = primarySource,
                        Kind = DebugSegmentKind.Diffracted,
                        Intensity = 1.0f,
                        Radius = 0,
                        Normal = Vector3.UnitZ,
                        TriangleIndex = -1
                    });
                }
            }
        }

        private void ReadVisibilityAndPaint(int triCount, bool diagnosticOnce)
        {
            uint byteSize = (uint)(triCount * sizeof(GpuVisibility));
            GpuVisibility* vis = (GpuVisibility*)_visibilitySsbo.MapRange(0, byteSize, _renderContext.Enums.MapReadBit);
            if (vis == null)
            {
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer] Visibility MapRange returned null");
                return;
            }

            var listenerVisible = new List<int>(512);
            var sourceVisible = new List<int>(512);
            var mutual = new List<int>(256);

            for (int t = 0; t < triCount; t++)
            {
                bool lVis = vis[t].ListenerVisible > 0.5f;
                bool sVis = vis[t].SourceVisible > 0.5f;
                if (lVis) listenerVisible.Add(t);
                if (sVis) sourceVisible.Add(t);
                if (lVis && sVis) mutual.Add(t);
            }
            _visibilitySsbo.Unmap();

            // Prefer mutual (cyan) first, then listener (blue), then source (red)
            // Soft cap so we stay inside MaxDebugSegments
            int injected = 0;
            const int maxInject = 300; // 300 triangles * 3 edges ≈ 900 segments

            foreach (int t in mutual)
            {
                if (injected >= maxInject) break;
                if (!_geometry.GetTriangle(t, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                _debugSegments.Add(new DebugSegment { A = a, B = b, Kind = DebugSegmentKind.Diffracted, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                _debugSegments.Add(new DebugSegment { A = b, B = c, Kind = DebugSegmentKind.Diffracted, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                _debugSegments.Add(new DebugSegment { A = c, B = a, Kind = DebugSegmentKind.Diffracted, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                injected++;
            }

            foreach (int t in listenerVisible)
            {
                if (injected >= maxInject) break;
                if (mutual.Contains(t)) continue; // already drawn cyan
                if (!_geometry.GetTriangle(t, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                _debugSegments.Add(new DebugSegment { A = a, B = b, Kind = DebugSegmentKind.FreeLeg, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                _debugSegments.Add(new DebugSegment { A = b, B = c, Kind = DebugSegmentKind.FreeLeg, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                _debugSegments.Add(new DebugSegment { A = c, B = a, Kind = DebugSegmentKind.FreeLeg, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                injected++;
            }

            foreach (int t in sourceVisible)
            {
                if (injected >= maxInject) break;
                if (mutual.Contains(t)) continue;
                if (!_geometry.GetTriangle(t, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                _debugSegments.Add(new DebugSegment { A = a, B = b, Kind = DebugSegmentKind.SourceFree, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                _debugSegments.Add(new DebugSegment { A = b, B = c, Kind = DebugSegmentKind.SourceFree, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                _debugSegments.Add(new DebugSegment { A = c, B = a, Kind = DebugSegmentKind.SourceFree, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = t });
                injected++;
            }

            if (diagnosticOnce)
            {
                Console.WriteLine($"[AcousticRayTracer] GPU viewport: listener={listenerVisible.Count} source={sourceVisible.Count} mutual={mutual.Count} injected={injected}");
            }
        }

        public IReadOnlyList<DebugSegment> GetDebugSegments() => _debugSegments;

        public SoundRayTraceResult ReadCompletedResult()
        {
            if (_disposed || _geometry.TriangleCount <= 0)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0f,
                    LowPassCutoff = 0f,
                    ApparentDirection = Vector3.Zero
                };
            }
            _residualProgram.Barrier();
            uint byteSize = (uint)(ContinuousRays * sizeof(GpuRayResult));
            GpuRayResult* results = (GpuRayResult*)_resultSsbo[_readIdx].MapRange(0, byteSize, _renderContext.Enums.MapReadBit);
            if (results == null)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0.02f,
                    LowPassCutoff = 800f,
                    ApparentDirection = Vector3.Zero
                };
            }
            float totalEnergy = 0f;
            Vector3 weightedDir = Vector3.Zero;
            float bestDelay = 0f;
            float bestLowPass = 0f;
            float maxSingle = 0f;
            int valid = 0;
            for (int i = 0; i < ContinuousRays; i++)
            {
                float inten = results[i].Intensity;
                if (inten > 0.00015f)
                {
                    valid++;
                    float energy = inten * inten;
                    totalEnergy += energy;
                    Vector3 dir = new Vector3(results[i].ArrivalDir.X, results[i].ArrivalDir.Y, results[i].ArrivalDir.Z);
                    if (dir.LengthSquared() > 1e-8f)
                        weightedDir += dir * energy;
                    if (inten > maxSingle)
                    {
                        maxSingle = inten;
                        bestDelay = results[i].Delay;
                        bestLowPass = results[i].LowPass;
                    }
                }
            }
            _resultSsbo[_readIdx].Unmap();
            if (valid == 0 || totalEnergy < 1e-8f)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0.04f,
                    LowPassCutoff = 800f,
                    ApparentDirection = Vector3.Zero
                };
            }
            Vector3 arrival = Vector3.Normalize(weightedDir);
            float intensity = Math.Clamp(MathF.Sqrt(totalEnergy / valid) * 1.8f, 0.001f, 0.82f);
            return new SoundRayTraceResult
            {
                Intensity = intensity,
                Delay = bestDelay,
                LowPassCutoff = bestLowPass > 0f ? bestLowPass : 2800f + 3200f * intensity,
                ApparentDirection = arrival
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _freeRayProgram?.Dispose();
                _residualProgram?.Dispose();
                _visibilityProgram?.Dispose();
                for (int i = 0; i < 2; i++)
                {
                    _resultSsbo[i]?.Dispose();
                    _debugSsbo[i]?.Dispose();
                }
                _visibilitySsbo?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}