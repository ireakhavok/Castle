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
        private readonly ShaderProgram _idProgram;
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private const int MaxDebugSegments = 65536;
        private const int IdBufferSize = 512;
        private const float VisibilityMoveThreshold = 0.25f;
        private const float SpeedOfSound = 34300f;
        private readonly List<DebugSegment> _debugSegments = new List<DebugSegment>(MaxDebugSegments);
        private Vector3 _lastListenerPos;
        private Vector3 _lastPrimarySource;
        // Free-surface previous-completed double-buffer
        private readonly HashSet<int>[] _listenerVisible = { new HashSet<int>(), new HashSet<int>() };
        private readonly HashSet<int>[] _sourceVisible = { new HashSet<int>(), new HashSet<int>() };
        private readonly HashSet<int>[] _mutual = { new HashSet<int>(), new HashSet<int>() };
        private readonly Vector3[] _fsListenerPos = new Vector3[2];
        private readonly Vector3[] _fsSourcePos = new Vector3[2];
        private readonly uint[] _fsGeometryVersion = new uint[2];
        private readonly bool[] _fsValid = new bool[2];
        private int _fsWrite = 0;
        private int _fsRead = 1;
        private uint _visibilityVersion;
        // Pending raster (true one-frame-behind production)
        private bool _pendingRaster;
        private Vector3 _pendingListener;
        private Vector3 _pendingSource;
        private uint _pendingGeometryVersion;
        private uint _fbo;
        private uint _idTexture;
        private uint _depthRb;
        private uint[] _idReadback;
        private bool _fboReady;
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
        public uint VisibilityVersion => _visibilityVersion;
        public bool VisibilityCacheValid => _fsValid[_fsRead];
        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _idProgram = new ShaderProgram(_renderContext, AcousticIdShader.VertexSource, AcousticIdShader.FragmentSource);
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
            // Residual multi-bounce path removed entirely for this stage.
        }
        /// <summary>
        /// Zero-cost gate check. If a recompute is required, records pending positions
        /// and returns immediately. The expensive RasterSphericalVisibility runs only
        /// inside TryCompletePendingRaster (true one-frame-behind production).
        /// </summary>
        public void KickDebugBidirectional(Vector3 listenerPos, IReadOnlyList<Vector3> sources)
        {
            if (_disposed) return;
            Vector3 primarySource = (sources != null && sources.Count > 0) ? sources[0] : listenerPos + new Vector3(0, 10, 0);
            _lastListenerPos = listenerPos;
            _lastPrimarySource = primarySource;
            if (_geometry.TriangleCount <= 0 || !_fboReady)
            {
                _debugSegments.Clear();
                return;
            }
            int read = _fsRead;
            bool needRecompute =
                !_fsValid[read] ||
                _geometry.GeometryVersion != _fsGeometryVersion[read] ||
                Vector3.DistanceSquared(listenerPos, _fsListenerPos[read]) > VisibilityMoveThreshold * VisibilityMoveThreshold ||
                Vector3.DistanceSquared(primarySource, _fsSourcePos[read]) > VisibilityMoveThreshold * VisibilityMoveThreshold;
            if (needRecompute)
            {
                _pendingListener = listenerPos;
                _pendingSource = primarySource;
                _pendingGeometryVersion = _geometry.GeometryVersion;
                _pendingRaster = true;
            }
            // Hot path ends here. No Raster, no TryClosestHit, no HashSet mutation.
        }
        /// <summary>
        /// Performs the expensive spherical ID raster into the write side and swaps
        /// only after a full successful recompute. Consumers always see the previous
        /// completed result. Call after the current frame has already consumed the
        /// previous state (true one-frame-behind).
        /// </summary>
        public bool TryCompletePendingRaster()
        {
            if (_disposed || !_pendingRaster || _geometry.TriangleCount <= 0 || !_fboReady)
                return false;
            int write = _fsWrite;
            _listenerVisible[write].Clear();
            _sourceVisible[write].Clear();
            _mutual[write].Clear();
            RasterSphericalVisibility(_pendingListener, _listenerVisible[write]);
            RasterSphericalVisibility(_pendingSource, _sourceVisible[write]);
            foreach (int tri in _listenerVisible[write])
                if (_sourceVisible[write].Contains(tri))
                    _mutual[write].Add(tri);
            _fsListenerPos[write] = _pendingListener;
            _fsSourcePos[write] = _pendingSource;
            _fsGeometryVersion[write] = _pendingGeometryVersion;
            _fsValid[write] = true;
            // Swap so consumers always see the previous completed state
            _fsRead = write;
            _fsWrite = 1 - write;
            _visibilityVersion++;
            _pendingRaster = false;
            // Rebuild lightweight debug LOS segment only after the expensive work
            _debugSegments.Clear();
            Vector3 toSource = _pendingSource - _pendingListener;
            float dist = toSource.Length();
            if (dist > 1e-4f)
            {
                Vector3 dir = toSource / dist;
                bool losClear = false;
                if (_geometry.TryClosestHit(_pendingListener, dir, out float tHit, out _, out _))
                {
                    if (tHit >= dist * 0.98f) losClear = true;
                }
                else losClear = true;
                if (losClear)
                {
                    _debugSegments.Add(new DebugSegment
                    {
                        A = _pendingListener,
                        B = _pendingSource,
                        Kind = DebugSegmentKind.Diffracted,
                        Intensity = 1.0f,
                        Radius = 0,
                        Normal = Vector3.UnitZ,
                        TriangleIndex = -1
                    });
                }
            }
            return true;
        }
        /// <summary>
        /// Exact free-surface perceived result used by the debug overlay orange ray.
        /// Energy-weighted inverse-square accumulation over mutual free triangles.
        /// This is the authoritative occluded path for live AudioSystem.
        /// Always reads the previous completed free-surface state.
        /// </summary>
        public SoundRayTraceResult ComputeFreeSurfacePerceived(Vector3 listener, Vector3 source)
        {
            int read = _fsRead;
            if (!_fsValid[read] || _mutual[read].Count == 0)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0f,
                    LowPassCutoff = 0f,
                    ApparentDirection = Vector3.Zero
                };
            }
            float dist = Vector3.Distance(listener, source);
            float energy = 0f;
            Vector3 weightedArrival = Vector3.Zero;
            float maxContrib = 0f;
            Vector3 strongestDir = Vector3.Zero;
            float strongestPath = dist;
            foreach (int tri in _mutual[read])
            {
                if (!_geometry.GetTriangle(tri, out Vector3 a, out Vector3 b, out Vector3 c)) continue;
                Vector3 centroid = (a + b + c) * (1f / 3f);
                float rL = Vector3.Distance(centroid, listener);
                float rS = Vector3.Distance(centroid, source);
                if (rL < 0.05f || rS < 0.05f) continue;
                float pathLength = rL + rS;
                float contrib = 1.0f / (pathLength * pathLength);
                energy += contrib;
                Vector3 arrival = centroid - listener;
                float arrivalLen = arrival.Length();
                if (arrivalLen < 1e-5f) continue;
                Vector3 arrivalDir = arrival / arrivalLen;
                weightedArrival += arrivalDir * contrib;
                if (contrib > maxContrib)
                {
                    maxContrib = contrib;
                    strongestDir = arrivalDir;
                    strongestPath = pathLength;
                }
            }
            if (energy <= 0f)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0f,
                    LowPassCutoff = 0f,
                    ApparentDirection = Vector3.Zero
                };
            }
            Vector3 perceivedDir = weightedArrival.LengthSquared() > 1e-12f
                ? Vector3.Normalize(weightedArrival)
                : strongestDir;
            float freeField = 1.0f / Math.Max(dist * dist, 1e-8f);
            float intensity = energy / freeField;
            return new SoundRayTraceResult
            {
                Intensity = intensity,
                Delay = strongestPath / SpeedOfSound,
                LowPassCutoff = 2800f + 3200f * intensity,
                ApparentDirection = perceivedDir
            };
        }
        private void RasterSphericalVisibility(Vector3 origin, HashSet<int> visibleSet)
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
                        visibleSet.Add(tri);
                }
            }
            _renderContext.BindFramebuffer(_renderContext.Enums.Framebuffer, 0);
            _renderContext.Viewport(0, 0, (uint)savedViewportW, (uint)savedViewportH);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
        }
        public IReadOnlyCollection<int> GetListenerFree() => _listenerVisible[_fsRead];
        public IReadOnlyCollection<int> GetSourceFree() => _sourceVisible[_fsRead];
        public IReadOnlyCollection<int> GetMutualFree() => _mutual[_fsRead];
        public IReadOnlyList<DebugSegment> GetDebugSegments() => _debugSegments;
        public SoundRayTraceResult ReadCompletedResult()
        {
            // Residual multi-bounce path removed entirely for this stage.
            return new SoundRayTraceResult
            {
                Intensity = 0.001f,
                Delay = 0f,
                LowPassCutoff = 0f,
                ApparentDirection = Vector3.Zero
            };
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _idProgram?.Dispose();
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