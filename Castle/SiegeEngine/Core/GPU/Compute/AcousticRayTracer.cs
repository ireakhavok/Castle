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
            FreeLeg = 0,
            SourceFree = 1,
            BounceLeg = 2,
            Splat = 3,
            Diffracted = 4
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
        private readonly ShaderStorageBuffer _hitIdSsbo;
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private int _writeIdx;
        private int _readIdx = 1;
        private const int MaxRays = 256;
        private const int ContinuousRays = 128;
        private const int ContinuousBounces = 6;
        private const int MaxDebugSegments = 4096;
        private const int ProjectiveGridRes = 48;
        private const int MaxSamples = ProjectiveGridRes * ProjectiveGridRes;
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
            int hitIdBytes = MaxSamples * sizeof(int);

            for (int i = 0; i < 2; i++)
            {
                _resultSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _resultSsbo[i].SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
                _debugSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _debugSsbo[i].SetData((uint)debugBytes, null, _renderContext.Enums.DynamicCopy);
            }
            _hitIdSsbo = new ShaderStorageBuffer(_renderContext);
            _hitIdSsbo.SetData((uint)hitIdBytes, null, _renderContext.Enums.DynamicCopy);
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
                Console.WriteLine($"[AcousticRayTracer] === KickDebug (GPU projective camera) ===");
                Console.WriteLine($" Listener = ({listenerPos.X:F2},{listenerPos.Y:F2},{listenerPos.Z:F2})");
                Console.WriteLine($" Source = ({primarySource.X:F2},{primarySource.Y:F2},{primarySource.Z:F2})");
                Console.WriteLine($" TriangleCount = {_geometry.TriangleCount}");
            }

            var listenerVisible = new HashSet<int>();
            var sourceVisible = new HashSet<int>();

            DispatchProjectiveCamera(listenerPos, primarySource, listenerVisible);
            DispatchProjectiveCamera(primarySource, listenerPos, sourceVisible);

            var mutual = new HashSet<int>();
            foreach (int tri in listenerVisible)
                if (sourceVisible.Contains(tri))
                    mutual.Add(tri);

            _debugSegments.Clear();
            PaintContinuousSurfaces(listenerVisible, sourceVisible, mutual, diagnosticOnce);

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

        private void DispatchProjectiveCamera(Vector3 origin, Vector3 lookAt, HashSet<int> visibleSet)
        {
            int sampleCount = MaxSamples;
            _geometry.Buffer.BindBase(0);
            _hitIdSsbo.BindBase(1);

            _visibilityProgram.Use();
            _visibilityProgram.SetUniform("uOrigin", origin.X, origin.Y, origin.Z);
            _visibilityProgram.SetUniform("uLookAt", lookAt.X, lookAt.Y, lookAt.Z);
            _visibilityProgram.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _visibilityProgram.SetUniform("uMaxDistance", 350.0f);
            _visibilityProgram.SetUniform("uGridRes", ProjectiveGridRes);
            _visibilityProgram.SetUniform("uSampleCount", sampleCount);

            uint groups = (uint)((sampleCount + 63) / 64);
            _visibilityProgram.Dispatch(groups, 1, 1);
            _visibilityProgram.Barrier();

            // Read unique hit triangle indices
            uint byteSize = (uint)(sampleCount * sizeof(int));
            int* hits = (int*)_hitIdSsbo.MapRange(0, byteSize, _renderContext.Enums.MapReadBit);
            if (hits == null) return;

            for (int i = 0; i < sampleCount; i++)
            {
                int tri = hits[i];
                if (tri >= 0 && tri < _geometry.TriangleCount)
                    visibleSet.Add(tri);
            }
            _hitIdSsbo.Unmap();
        }

        private void PaintContinuousSurfaces(HashSet<int> listenerVisible, HashSet<int> sourceVisible, HashSet<int> mutual, bool diagnosticOnce)
        {
            int injected = 0;
            const int maxInject = 800;

            foreach (int tri in mutual)
            {
                if (injected >= maxInject) break;
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                EmitFilledTriangle(a, b, c, DebugSegmentKind.Diffracted, tri);
                injected++;
            }

            foreach (int tri in listenerVisible)
            {
                if (injected >= maxInject) break;
                if (mutual.Contains(tri)) continue;
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                EmitFilledTriangle(a, b, c, DebugSegmentKind.FreeLeg, tri);
                injected++;
            }

            foreach (int tri in sourceVisible)
            {
                if (injected >= maxInject) break;
                if (mutual.Contains(tri)) continue;
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                EmitFilledTriangle(a, b, c, DebugSegmentKind.SourceFree, tri);
                injected++;
            }

            if (diagnosticOnce)
            {
                Console.WriteLine($"[AcousticRayTracer] GPU projective camera: listener={listenerVisible.Count} source={sourceVisible.Count} mutual={mutual.Count} injected={injected}");
            }
        }

        private void EmitFilledTriangle(Vector3 a, Vector3 b, Vector3 c, DebugSegmentKind kind, int triIndex)
        {
            _debugSegments.Add(new DebugSegment { A = a, B = b, Kind = kind, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = triIndex });
            _debugSegments.Add(new DebugSegment { A = b, B = c, Kind = kind, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = triIndex });
            _debugSegments.Add(new DebugSegment { A = c, B = a, Kind = kind, Intensity = 1f, Radius = 0, Normal = Vector3.UnitZ, TriangleIndex = triIndex });
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
                _hitIdSsbo?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}