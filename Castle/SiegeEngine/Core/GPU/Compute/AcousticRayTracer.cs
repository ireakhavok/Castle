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
        public enum DebugSegmentKind : int { Listener = 0, Source = 1, Meeting = 2 }

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
        }

        private readonly IRenderContext _renderContext;
        private readonly ComputeProgram _freeRayProgram;
        private readonly ComputeProgram _meetingProgram;
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

        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));

            _freeRayProgram = new ComputeProgram(_renderContext, AcousticFreeRayShader.Source);
            _meetingProgram = new ComputeProgram(_renderContext, AcousticMeetingShader.Source);

            string residualSrc = AcousticCommon.Source + AcousticResidualShader.Source;
            _residualProgram = new ComputeProgram(_renderContext, residualSrc);

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
            if (_disposed)
            {
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer.KickDebugBidirectional] early-out: disposed");
                return;
            }

            Vector3 primarySource = (sources != null && sources.Count > 0) ? sources[0] : listenerPos + new Vector3(0, 10, 0);

            if (_geometry.TriangleCount <= 0)
            {
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer.KickDebugBidirectional] early-out: TriangleCount<=0 → synthetic free segments");

                _debugSegments.Clear();
                const int n = 48;
                for (int i = 0; i < n; i++)
                {
                    float a = i * MathF.PI * 2f / n;
                    float elev = ((i % 5) - 2) * 0.3f;
                    Vector3 dir = Vector3.Normalize(new Vector3(MathF.Cos(a), MathF.Sin(a), elev));
                    _debugSegments.Add(new DebugSegment
                    {
                        A = listenerPos,
                        B = listenerPos + dir * 30f,
                        Kind = DebugSegmentKind.Listener
                    });
                }

                if (sources != null)
                {
                    for (int s = 0; s < sources.Count; s++)
                    {
                        Vector3 src = sources[s];
                        for (int i = 0; i < 16; i++)
                        {
                            float a = i * MathF.PI * 2f / 16f;
                            Vector3 dir = Vector3.Normalize(new Vector3(MathF.Cos(a), MathF.Sin(a), 0.05f));
                            _debugSegments.Add(new DebugSegment
                            {
                                A = src,
                                B = src + dir * 20f,
                                Kind = DebugSegmentKind.Source
                            });
                        }

                        Vector3 mid = (listenerPos + src) * 0.5f;
                        _debugSegments.Add(new DebugSegment
                        {
                            A = mid - new Vector3(0.6f, 0, 0),
                            B = mid + new Vector3(0.6f, 0, 0),
                            Kind = DebugSegmentKind.Meeting
                        });
                        _debugSegments.Add(new DebugSegment
                        {
                            A = mid - new Vector3(0, 0.6f, 0),
                            B = mid + new Vector3(0, 0.6f, 0),
                            Kind = DebugSegmentKind.Meeting
                        });
                    }
                }
                return;
            }

            if (diagnosticOnce)
                Console.WriteLine($"[AcousticRayTracer.KickDebugBidirectional] dispatch GPU path listener=({listenerPos.X:F2},{listenerPos.Y:F2},{listenerPos.Z:F2}) primarySource=({primarySource.X:F2},{primarySource.Y:F2},{primarySource.Z:F2}) triangles={_geometry.TriangleCount}");

            _geometry.Buffer.BindBase(0);
            _resultSsbo[_writeIdx].BindBase(1);
            _debugSsbo[_writeIdx].BindBase(2);

            // Pass 1 – free rays
            _freeRayProgram.Use();
            _freeRayProgram.SetUniform("uSourcePos", primarySource.X, primarySource.Y, primarySource.Z);
            _freeRayProgram.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _freeRayProgram.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _freeRayProgram.SetUniform("uRayCount", ContinuousRays);
            _freeRayProgram.SetUniform("uMaxBounces", ContinuousBounces);
            _freeRayProgram.SetUniform("uListenerRadius", 5.0f);
            _freeRayProgram.SetUniform("uMaxDistance", 350.0f);
            _freeRayProgram.SetUniform("uDebugMode", 1);
            _freeRayProgram.SetUniform("uSourceCount", sources != null ? Math.Min(sources.Count, 8) : 0);

            uint groups = (uint)((ContinuousRays + 63) / 64);
            _freeRayProgram.Dispatch(groups, 1, 1);
            _freeRayProgram.Barrier();

            // Pass 2 – free-end ↔ free-end meetings
            _meetingProgram.Use();
            _meetingProgram.SetUniform("uSourcePos", primarySource.X, primarySource.Y, primarySource.Z);
            _meetingProgram.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _meetingProgram.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _meetingProgram.SetUniform("uRayCount", ContinuousRays);
            _meetingProgram.SetUniform("uMaxBounces", ContinuousBounces);
            _meetingProgram.SetUniform("uListenerRadius", 5.0f);
            _meetingProgram.SetUniform("uMaxDistance", 350.0f);
            _meetingProgram.SetUniform("uDebugMode", 1);
            _meetingProgram.SetUniform("uSourceCount", sources != null ? Math.Min(sources.Count, 8) : 0);

            uint meetGroups = (uint)((ContinuousRays / 2 + 63) / 64);
            _meetingProgram.Dispatch(meetGroups, 1, 1);
            _meetingProgram.Barrier();

            ReadDebugSegmentsFromBuffer(_writeIdx, diagnosticOnce);

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
                    Console.WriteLine("[AcousticRayTracer.ReadDebugSegmentsFromBuffer] MapRange returned null");
                return;
            }

            int accepted = 0;
            for (int i = 0; i < MaxDebugSegments; i++)
            {
                float kindF = segs[i].B.W;
                if (kindF < -0.5f) continue;
                int kind = (int)(kindF + 0.5f);
                if (kind < 0 || kind > 2) continue;

                Vector3 a = new Vector3(segs[i].A.X, segs[i].A.Y, segs[i].A.Z);
                Vector3 b = new Vector3(segs[i].B.X, segs[i].B.Y, segs[i].B.Z);
                if ((a - b).LengthSquared() < 1e-5f) continue;

                _debugSegments.Add(new DebugSegment
                {
                    A = a,
                    B = b,
                    Kind = (DebugSegmentKind)kind
                });
                accepted++;
            }

            _debugSsbo[bufIdx].Unmap();

            if (diagnosticOnce)
                Console.WriteLine($"[AcousticRayTracer.ReadDebugSegmentsFromBuffer] accepted {accepted} segments from SSBO (filtered length/kind)");
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
            int txCount = 0, diffrCount = 0, connCount = 0;

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

                    if (results[i].Pad > 1.5f) connCount++;
                    else if (results[i].Pad > 0.5f) diffrCount++;
                    else txCount++;
                }
            }

            _resultSsbo[_readIdx].Unmap();

            if (valid == 0 || totalEnergy < 1e-8f)
            {
                Console.WriteLine($"AcousticRayTracer residual: Primary=blocked residual=0.001 dir=(0,0,0) pathways tx=0 diffr=0 conn=0");
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

            Console.WriteLine($"AcousticRayTracer residual: Primary=blocked residual={intensity:F3} dir=({arrival.X:F2},{arrival.Y:F2},{arrival.Z:F2}) pathways tx={txCount} diffr={diffrCount} conn={connCount}");

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
                _meetingProgram?.Dispose();
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