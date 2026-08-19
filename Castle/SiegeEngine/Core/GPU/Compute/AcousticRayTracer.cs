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
            FreeLeg = 0, // listener free
            SourceFree = 1, // source free
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
        }
        private readonly IRenderContext _renderContext;
        private readonly ComputeProgram _freeRayProgram;
        private readonly ComputeProgram _residualProgram;
        private readonly ShaderStorageBuffer[] _resultSsbo = new ShaderStorageBuffer[2];
        private readonly ShaderStorageBuffer[] _debugSsbo = new ShaderStorageBuffer[2];
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private int _writeIdx;
        private int _readIdx = 1;
        private const int MaxRays = 128;
        private const int ContinuousRays = 128;
        private const int ContinuousBounces = 6;
        private const int MaxDebugSegments = 1024;
        private readonly List<DebugSegment> _debugSegments = new List<DebugSegment>(MaxDebugSegments);
        private Vector3 _lastListenerPos;
        private Vector3 _lastPrimarySource;
        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _freeRayProgram = new ComputeProgram(_renderContext, AcousticFreeRayShader.Source);
            _residualProgram = new ComputeProgram(_renderContext, AcousticCommon.Source + AcousticResidualShader.Source);
            int resultBytes = MaxRays * sizeof(GpuRayResult);
            int debugBytes = MaxDebugSegments * sizeof(GpuDebugSegment);
            for (int i = 0; i < 2; i++)
            {
                _resultSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _resultSsbo[i].SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
                _debugSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _debugSsbo[i].SetData((uint)debugBytes, null, _renderContext.Enums.DynamicCopy);
            }
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
                Console.WriteLine($"[AcousticRayTracer] === KickDebug ===");
                Console.WriteLine($" Listener = ({listenerPos.X:F2},{listenerPos.Y:F2},{listenerPos.Z:F2})");
                Console.WriteLine($" Source = ({primarySource.X:F2},{primarySource.Y:F2},{primarySource.Z:F2})");
                Console.WriteLine($" TriangleCount = {_geometry.TriangleCount}");
            }
            _geometry.Buffer.BindBase(0);
            _resultSsbo[_writeIdx].BindBase(1);
            _debugSsbo[_writeIdx].BindBase(2);
            _freeRayProgram.Use();
            _freeRayProgram.SetUniform("uSourcePos", primarySource.X, primarySource.Y, primarySource.Z);
            _freeRayProgram.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _freeRayProgram.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _freeRayProgram.SetUniform("uRayCount", ContinuousRays);
            _freeRayProgram.SetUniform("uMaxBounces", 1);
            _freeRayProgram.SetUniform("uListenerRadius", 5.0f);
            _freeRayProgram.SetUniform("uMaxDistance", 350.0f);
            _freeRayProgram.SetUniform("uDebugMode", 1);
            _freeRayProgram.SetUniform("uSourceCount", sources != null ? Math.Min(sources.Count, 8) : 0);
            uint groups = (uint)((ContinuousRays + 63) / 64);
            _freeRayProgram.Dispatch(groups, 1, 1);
            _freeRayProgram.Barrier();
            ReadDebugSegmentsFromBuffer(_writeIdx, diagnosticOnce);
            SolveAndInjectRoute(listenerPos, primarySource, diagnosticOnce);
            _readIdx = _writeIdx;
            _writeIdx = 1 - _writeIdx;
        }
        public IReadOnlyList<DebugSegment> GetDebugSegments() => _debugSegments;
        private void ReadDebugSegmentsFromBuffer(int bufIdx, bool diagnosticOnce = false)
        {
            _debugSegments.Clear();
            uint byteSize = (uint)(MaxDebugSegments * sizeof(GpuDebugSegment));
            GpuDebugSegment* segs = (GpuDebugSegment*)_debugSsbo[bufIdx].MapRange(0, byteSize, _renderContext.Enums.MapReadBit);
            if (segs == null)
            {
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer] MapRange returned null");
                return;
            }
            int[] kindCount = new int[5];
            int accepted = 0;
            for (int i = 0; i < MaxDebugSegments; i++)
            {
                float kindF = segs[i].B.W;
                if (kindF < -0.5f) continue;
                int kind = (int)kindF;
                if (kind < 0 || kind > 4) continue;
                Vector3 a = new Vector3(segs[i].A.X, segs[i].A.Y, segs[i].A.Z);
                Vector3 b = new Vector3(segs[i].B.X, segs[i].B.Y, segs[i].B.Z);
                float intensity = 1.0f;
                if (kind == 3)
                {
                    intensity = Math.Clamp(kindF - 3.0f, 0.05f, 1.0f);
                    kind = 3;
                    b = a + new Vector3(0.2f, 0, 0);
                }
                if ((a - b).LengthSquared() < 1e-6f && kind != 3) continue;
                _debugSegments.Add(new DebugSegment
                {
                    A = a,
                    B = b,
                    Kind = (DebugSegmentKind)kind,
                    Intensity = intensity
                });
                if (kind >= 0 && kind <= 4) kindCount[kind]++;
                accepted++;
            }
            _debugSsbo[bufIdx].Unmap();
            if (diagnosticOnce)
            {
                Console.WriteLine($"[AcousticRayTracer] ReadDebugSegments accepted={accepted}");
                Console.WriteLine($" Kind 0 (ListenerFree) = {kindCount[0]}");
                Console.WriteLine($" Kind 1 (SourceFree) = {kindCount[1]}");
                Console.WriteLine($" Kind 2 (Bounce) = {kindCount[2]}");
                Console.WriteLine($" Kind 3 (Splat) = {kindCount[3]}");
                Console.WriteLine($" Kind 4 (Diffracted) = {kindCount[4]}");
                int sample = Math.Min(16, _debugSegments.Count);
                for (int i = 0; i < sample; i++)
                {
                    var s = _debugSegments[i];
                    Console.WriteLine($" seg[{i}] Kind={s.Kind} Int={s.Intensity:F2} A=({s.A.X:F1},{s.A.Y:F1},{s.A.Z:F1}) B=({s.B.X:F1},{s.B.Y:F1},{s.B.Z:F1})");
                }
            }
        }
        private void SolveAndInjectRoute(Vector3 listenerPos, Vector3 sourcePos, bool diagnosticOnce)
        {
            Vector3 toSource = sourcePos - listenerPos;
            float dist = toSource.Length();
            if (dist < 1e-4f) return;
            Vector3 dir = toSource / dist;
            bool losClear = false;
            if (_geometry.TryClosestHit(listenerPos, dir, out float tHit, out Vector3 nHit, out float dens))
            {
                if (tHit >= dist * 0.98f)
                    losClear = true;
            }
            else
            {
                losClear = true;
            }
            if (losClear)
            {
                _debugSegments.Add(new DebugSegment
                {
                    A = listenerPos,
                    B = sourcePos,
                    Kind = DebugSegmentKind.Diffracted,
                    Intensity = 1.0f
                });
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer] Solved route: DIRECT LOS");
                return;
            }
            var listenerEnds = new List<Vector3>(64);
            var sourceEnds = new List<Vector3>(64);
            for (int i = 0; i < _debugSegments.Count; i++)
            {
                var seg = _debugSegments[i];
                if (seg.Kind == DebugSegmentKind.FreeLeg)
                    listenerEnds.Add(seg.B);
                else if (seg.Kind == DebugSegmentKind.SourceFree)
                    sourceEnds.Add(seg.B);
                else if (seg.Kind == DebugSegmentKind.Splat)
                {
                    // Prefer splat centers (more accurate hit location)
                    // Heuristic: if closer to listener than source, treat as listener-side
                    float dL = (seg.A - listenerPos).LengthSquared();
                    float dS = (seg.A - sourcePos).LengthSquared();
                    if (dL <= dS)
                        listenerEnds.Add(seg.A);
                    else
                        sourceEnds.Add(seg.A);
                }
            }
            if (listenerEnds.Count == 0 || sourceEnds.Count == 0)
            {
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer] No free-ends available for pairing");
                return;
            }
            float bestDist = float.MaxValue;
            Vector3 bestL = Vector3.Zero;
            Vector3 bestS = Vector3.Zero;
            for (int i = 0; i < listenerEnds.Count; i++)
            {
                for (int j = 0; j < sourceEnds.Count; j++)
                {
                    float d = (listenerEnds[i] - sourceEnds[j]).Length();
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestL = listenerEnds[i];
                        bestS = sourceEnds[j];
                    }
                }
            }
            if (bestDist > 80.0f)
            {
                if (diagnosticOnce)
                    Console.WriteLine($"[AcousticRayTracer] Best free-end pair too far ({bestDist:F1}m), skipping");
                return;
            }
            // Inject full solved path in cyan (Kind 4) so the route is clearly visible
            _debugSegments.Add(new DebugSegment
            {
                A = listenerPos,
                B = bestL,
                Kind = DebugSegmentKind.Diffracted,
                Intensity = 0.95f
            });
            _debugSegments.Add(new DebugSegment
            {
                A = bestL,
                B = bestS,
                Kind = DebugSegmentKind.Diffracted,
                Intensity = 1.0f
            });
            _debugSegments.Add(new DebugSegment
            {
                A = bestS,
                B = sourcePos,
                Kind = DebugSegmentKind.Diffracted,
                Intensity = 0.95f
            });
            if (diagnosticOnce)
                Console.WriteLine($"[AcousticRayTracer] Solved route: free-end pair dist={bestDist:F1}m");
        }
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
                for (int i = 0; i < 2; i++)
                {
                    _resultSsbo[i]?.Dispose();
                    _debugSsbo[i]?.Dispose();
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}