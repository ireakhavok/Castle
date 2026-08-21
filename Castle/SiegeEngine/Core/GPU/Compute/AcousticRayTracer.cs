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
        private readonly ShaderProgram _idProgram;
        private readonly ShaderStorageBuffer[] _resultSsbo = new ShaderStorageBuffer[2];
        private readonly ShaderStorageBuffer[] _debugSsbo = new ShaderStorageBuffer[2];
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private int _writeIdx;
        private int _readIdx = 1;
        private const int MaxRays = 256;
        private const int ContinuousRays = 128;
        private const int ContinuousBounces = 6;
        private const int MaxDebugSegments = 65536;
        // Raised hard so thin real meshes can occlude the floor under the listener.
        private const int IdBufferSize = 512;
        private const float VisibilityMoveThreshold = 0.75f;
        private readonly List<DebugSegment> _debugSegments = new List<DebugSegment>(MaxDebugSegments);
        private Vector3 _lastListenerPos;
        private Vector3 _lastPrimarySource;
        private readonly HashSet<int> _cachedListenerVisible = new HashSet<int>();
        private readonly HashSet<int> _cachedSourceVisible = new HashSet<int>();
        private readonly HashSet<int> _cachedMutual = new HashSet<int>();
        private Vector3 _cachedListenerPos;
        private Vector3 _cachedSourcePos;
        private uint _cachedGeometryVersion;
        private bool _visibilityCacheValid;
        private uint _fbo;
        private uint _idTexture;
        private uint _depthRb;
        private uint[] _idReadback;
        private bool _fboReady;
        // Diagnostic: records the first face+pixel that wrote each triangle ID during the source raster.
        private readonly Dictionary<int, (int face, int px, int py)> _sourceLeakInfo = new Dictionary<int, (int, int, int)>();
        private static readonly Vector3[] CubeDirs =
        {
            new Vector3( 1, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3( 0, 1, 0),
            new Vector3( 0,-1, 0),
            new Vector3( 0, 0, 1),
            new Vector3( 0, 0,-1)
        };
        private static readonly Vector3[] CubeUps =
        {
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 1),
            new Vector3(0, 1, 0),
            new Vector3(0, 1, 0)
        };
        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _freeRayProgram = new ComputeProgram(_renderContext, AcousticFreeRayShader.Source);
            _residualProgram = new ComputeProgram(_renderContext, AcousticCommon.Source + AcousticResidualShader.Source);
            _idProgram = new ShaderProgram(_renderContext, AcousticIdShader.VertexSource, AcousticIdShader.FragmentSource);
            int resultBytes = MaxRays * sizeof(GpuRayResult);
            int debugBytes = MaxDebugSegments * sizeof(GpuDebugSegment);
            for (int i = 0; i < 2; i++)
            {
                _resultSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _resultSsbo[i].SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
                _debugSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _debugSsbo[i].SetData((uint)debugBytes, null, _renderContext.Enums.DynamicCopy);
            }
            _idReadback = new uint[IdBufferSize * IdBufferSize];
            CreateIdFbo();
        }
        private void CreateIdFbo()
        {
            _renderContext.GenFramebuffers(1, out _fbo);
            _renderContext.BindFramebuffer(_renderContext.Enums.Framebuffer, _fbo);
            _renderContext.GenTextures(1, out _idTexture);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, _idTexture);
            _renderContext.TexImage2D(_renderContext.Enums.Texture2D, 0, _renderContext.Enums.R32UI,
                IdBufferSize, IdBufferSize, 0, _renderContext.Enums.RedInteger, _renderContext.Enums.UnsignedIntType, null);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Nearest);
            _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Nearest);
            _renderContext.FramebufferTexture2D(_renderContext.Enums.Framebuffer, _renderContext.Enums.ColorAttachment0,
                _renderContext.Enums.Texture2D, _idTexture, 0);
            _renderContext.GenRenderbuffers(1, out _depthRb);
            _renderContext.BindRenderbuffer(_renderContext.Enums.Renderbuffer, _depthRb);
            _renderContext.RenderbufferStorage(_renderContext.Enums.Renderbuffer, _renderContext.Enums.DepthComponent24,
                IdBufferSize, IdBufferSize);
            _renderContext.FramebufferRenderbuffer(_renderContext.Enums.Framebuffer, _renderContext.Enums.DepthAttachment,
                _renderContext.Enums.Renderbuffer, _depthRb);
            int status = _renderContext.CheckFramebufferStatus(_renderContext.Enums.Framebuffer);
            _fboReady = (status == _renderContext.Enums.FramebufferComplete);
            _renderContext.BindFramebuffer(_renderContext.Enums.Framebuffer, 0);
            if (!_fboReady)
                Console.WriteLine($"[AcousticRayTracer] ID FBO incomplete, status={status}");
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
            if (_geometry.TriangleCount <= 0 || !_fboReady)
            {
                _debugSegments.Clear();
                _visibilityCacheValid = false;
                if (diagnosticOnce)
                    Console.WriteLine("[AcousticRayTracer] TriangleCount == 0 or FBO not ready");
                return;
            }
            if (diagnosticOnce)
            {
                Console.WriteLine($"[AcousticRayTracer] === KickDebug (full spherical ID) ===");
                Console.WriteLine($" Listener = ({listenerPos.X:F2},{listenerPos.Y:F2},{listenerPos.Z:F2})");
                Console.WriteLine($" Source = ({primarySource.X:F2},{primarySource.Y:F2},{primarySource.Z:F2})");
                Console.WriteLine($" TriangleCount = {_geometry.TriangleCount}");
            }
            bool needRecompute =
                !_visibilityCacheValid ||
                _geometry.GeometryVersion != _cachedGeometryVersion ||
                Vector3.DistanceSquared(listenerPos, _cachedListenerPos) > VisibilityMoveThreshold * VisibilityMoveThreshold ||
                Vector3.DistanceSquared(primarySource, _cachedSourcePos) > VisibilityMoveThreshold * VisibilityMoveThreshold;
            if (needRecompute)
            {
                _cachedListenerVisible.Clear();
                _cachedSourceVisible.Clear();
                _cachedMutual.Clear();
                _sourceLeakInfo.Clear();
                RasterSphericalVisibility(listenerPos, _cachedListenerVisible, diagnosticOnce, isSource: false);
                RasterSphericalVisibility(primarySource, _cachedSourceVisible, diagnosticOnce, isSource: true);
                foreach (int tri in _cachedListenerVisible)
                    if (_cachedSourceVisible.Contains(tri))
                        _cachedMutual.Add(tri);
                _cachedListenerPos = listenerPos;
                _cachedSourcePos = primarySource;
                _cachedGeometryVersion = _geometry.GeometryVersion;
                _visibilityCacheValid = true;
            }
            _debugSegments.Clear();
            PaintContinuousSurfaces(_cachedListenerVisible, _cachedSourceVisible, _cachedMutual, diagnosticOnce, listenerPos, primarySource);
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
        private void RasterSphericalVisibility(Vector3 origin, HashSet<int> visibleSet, bool diagnosticOnce, bool isSource)
        {
            int savedViewportW = _renderContext.ViewportWidth;
            int savedViewportH = _renderContext.ViewportHeight;
            _renderContext.BindFramebuffer(_renderContext.Enums.Framebuffer, _fbo);
            _renderContext.Viewport(0, 0, IdBufferSize, IdBufferSize);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.DepthFunc(_renderContext.Enums.Less);
            _renderContext.Disable(_renderContext.Enums.Blend);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.3f, 400.0f);
            _idProgram.Use();
            _idProgram.SetMatrix4("uProjection", proj);
            int maxTri = _geometry.TriangleCount;
            for (int face = 0; face < 6; face++)
            {
                Vector3 target = origin + CubeDirs[face] * 10.0f;
                Matrix4x4 view = Matrix4x4.CreateLookAt(origin, target, CubeUps[face]);
                _idProgram.SetMatrix4("uView", view);

                // FIX: ClearBufferuiv must be called with GL_COLOR (Enums.Color),
                // NOT ColorAttachment0. ColorAttachment0 is only valid for
                // FramebufferTexture2D. Passing the wrong enum made the clear
                // a no-op, leaving listener floor IDs in the texture that then
                // contaminated the source pass → false teal under the feet.
                uint clearVal = 0;
                _renderContext.ClearBufferuiv(_renderContext.Enums.Color, 0, &clearVal);
                _renderContext.Clear(_renderContext.Enums.DepthBufferBit);

                _geometry.Draw();
                fixed (uint* ptr = _idReadback)
                {
                    _renderContext.ReadPixels(0, 0, IdBufferSize, IdBufferSize,
                        _renderContext.Enums.RedInteger, _renderContext.Enums.UnsignedIntType, ptr);
                }
                for (int i = 0; i < _idReadback.Length; i++)
                {
                    uint raw = _idReadback[i];
                    if (raw == 0) continue;
                    int tri = (int)raw - 1;
                    if (tri >= 0 && tri < maxTri)
                    {
                        visibleSet.Add(tri);
                        if (diagnosticOnce && isSource && !_sourceLeakInfo.ContainsKey(tri))
                        {
                            int px = i % IdBufferSize;
                            int py = i / IdBufferSize;
                            _sourceLeakInfo[tri] = (face, px, py);
                        }
                    }
                }
            }
            _renderContext.BindFramebuffer(_renderContext.Enums.Framebuffer, 0);
            _renderContext.Viewport(0, 0, (uint)savedViewportW, (uint)savedViewportH);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
        }
        private void PaintContinuousSurfaces(HashSet<int> listenerVisible, HashSet<int> sourceVisible, HashSet<int> mutual, bool diagnosticOnce, Vector3 listenerPos, Vector3 sourcePos)
        {
            if (diagnosticOnce)
            {
                int near2 = 0, near5 = 0, near10 = 0;
                int logged = 0;
                int occludedCount = 0;
                int clearCount = 0;
                foreach (int tri in mutual)
                {
                    if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                    Vector3 centroid = (a + b + c) * (1f / 3f);
                    float dist = Vector3.Distance(centroid, listenerPos);
                    if (dist <= 2.0f) near2++;
                    if (dist <= 5.0f) near5++;
                    if (dist <= 10.0f) near10++;
                    // Only heightmap-style (large) near-listener mutuals get the raycast proof
                    Vector3 ab = b - a;
                    Vector3 ac = c - a;
                    float edgeAB = ab.Length();
                    float edgeAC = ac.Length();
                    float edgeBC = Vector3.Distance(b, c);
                    float maxEdge = MathF.Max(edgeAB, MathF.Max(edgeAC, edgeBC));
                    if (dist <= 5.0f && maxEdge > 1.0f && logged < 20)
                    {
                        Vector3 n = Vector3.Cross(ab, ac);
                        float nLen = n.Length();
                        float planeDist = 0f;
                        float nZ = 0f;
                        if (nLen > 1e-8f)
                        {
                            n /= nLen;
                            nZ = n.Z;
                            planeDist = MathF.Abs(Vector3.Dot(listenerPos - a, n));
                        }
                        float minZ = MathF.Min(a.Z, MathF.Min(b.Z, c.Z));
                        float maxZ = MathF.Max(a.Z, MathF.Max(b.Z, c.Z));
                        // Raycast from SOURCE toward the floor centroid to prove occlusion
                        Vector3 toCent = centroid - sourcePos;
                        float distToCent = toCent.Length();
                        bool occluded = false;
                        float tHit = float.MaxValue;
                        if (distToCent > 1e-4f)
                        {
                            Vector3 dir = toCent / distToCent;
                            if (_geometry.TryClosestHit(sourcePos, dir, out tHit, out _, out _))
                            {
                                if (tHit < distToCent * 0.98f)
                                    occluded = true;
                            }
                        }
                        if (occluded) occludedCount++;
                        else clearCount++;

                        string leakStr = "";
                        if (_sourceLeakInfo.TryGetValue(tri, out var leak))
                        {
                            float u = (leak.px + 0.5f) / IdBufferSize * 2f - 1f;
                            float v = (leak.py + 0.5f) / IdBufferSize * 2f - 1f;
                            Vector3 faceDir = CubeDirs[leak.face];
                            Vector3 up = CubeUps[leak.face];
                            Vector3 right = Vector3.Normalize(Vector3.Cross(up, faceDir));
                            up = Vector3.Normalize(Vector3.Cross(faceDir, right));
                            Vector3 sampleDir = Vector3.Normalize(faceDir + right * u + up * v);

                            float sampleT = float.MaxValue;
                            Vector3 sampleN = Vector3.Zero;
                            float sampleDens = 0f;
                            bool sampleHit = _geometry.TryClosestHit(sourcePos, sampleDir, out sampleT, out sampleN, out sampleDens);
                            string continuousResult;
                            if (!sampleHit)
                                continuousResult = "MISS";
                            else
                            {
                                Vector3 hitPoint = sourcePos + sampleDir * sampleT;
                                float distToFloorCentroid = Vector3.Distance(hitPoint, centroid);
                                if (distToFloorCentroid < 1.5f)
                                    continuousResult = $"FLOOR t={sampleT:F2}";
                                else
                                    continuousResult = $"WALL/OTHER t={sampleT:F2} hit=({hitPoint.X:F1},{hitPoint.Y:F1},{hitPoint.Z:F1})";
                            }

                            leakStr = $" | LEAK face={leak.face} px={leak.px},{leak.py} sampleDir=({sampleDir.X:F3},{sampleDir.Y:F3},{sampleDir.Z:F3}) CONTINUOUS_ON_SAMPLE={continuousResult}";
                        }
                        Console.WriteLine($"[AcousticRayTracer] NEAR-MUTUAL tri={tri} centroid=({centroid.X:F2},{centroid.Y:F2},{centroid.Z:F2}) distL={dist:F2} planeDist={planeDist:F3} nZ={nZ:F3} minZ={minZ:F2} maxZ={maxZ:F2} maxEdge={maxEdge:F2} | FROM-SOURCE distToCent={distToCent:F2} tHit={tHit:F2} OCCLUDED={occluded}{leakStr}");
                        logged++;
                    }
                }
                Console.WriteLine($"[AcousticRayTracer] Mutual near-listener counts: <=2.0={near2} <=5.0={near5} <=10.0={near10} (total mutual={mutual.Count})");
                Console.WriteLine($"[AcousticRayTracer] Heightmap-style near-mutual raycast proof: occluded={occludedCount} clear={clearCount} (logged {logged})");
            }
            foreach (int tri in mutual)
            {
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                EmitFilledTriangle(a, b, c, DebugSegmentKind.Diffracted, tri);
            }
            foreach (int tri in listenerVisible)
            {
                if (mutual.Contains(tri)) continue;
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                EmitFilledTriangle(a, b, c, DebugSegmentKind.FreeLeg, tri);
            }
            foreach (int tri in sourceVisible)
            {
                if (mutual.Contains(tri)) continue;
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                EmitFilledTriangle(a, b, c, DebugSegmentKind.SourceFree, tri);
            }
            if (diagnosticOnce)
            {
                Console.WriteLine($"[AcousticRayTracer] Spherical ID (no cap): listener={listenerVisible.Count} source={sourceVisible.Count} mutual={mutual.Count} segments={_debugSegments.Count}");
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
                _idProgram?.Dispose();
                for (int i = 0; i < 2; i++)
                {
                    _resultSsbo[i]?.Dispose();
                    _debugSsbo[i]?.Dispose();
                }
                if (_fbo != 0)
                {
                    uint f = _fbo;
                    _renderContext.DeleteFramebuffers(1, &f);
                }
                if (_idTexture != 0) _renderContext.DeleteTexture(_idTexture);
                if (_depthRb != 0)
                {
                    uint r = _depthRb;
                    _renderContext.DeleteRenderbuffers(1, &r);
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}