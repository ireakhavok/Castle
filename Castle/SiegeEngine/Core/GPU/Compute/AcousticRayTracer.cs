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
        // ===== PRIMARY sticky double-buffer (byte-identical, never written by secondary) =====
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
        // Progressive face state machine – PRIMARY only
        private bool _pendingRaster;
        private Vector3 _pendingListener;
        private Vector3 _pendingSource;
        private uint _pendingGeometryVersion;
        private int _pendingFace;
        private const int FacesPerCall = 6;
        private uint _fbo;
        private uint _idTexture;
        private uint _depthRb;
        private uint[] _idReadback;
        private bool _fboReady;
        // PBO + fence (shared hardware, but primary and secondary never run concurrently)
        private readonly uint[] _pbo = new uint[2];
        private int _pboIndex;
        private uint _pendingFence;
        private int _pendingPbo;
        private bool _fencePending;
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
        // ===== SECONDARY independent sticky slots (one per EntityId) =====
        private class SecondarySlot
        {
            public int EntityId;
            public Vector3 SourcePos;
            public readonly HashSet<int>[] ListenerVisible = { new HashSet<int>(), new HashSet<int>() };
            public readonly HashSet<int>[] SourceVisible = { new HashSet<int>(), new HashSet<int>() };
            public readonly HashSet<int>[] Mutual = { new HashSet<int>(), new HashSet<int>() };
            public readonly Vector3[] FsListenerPos = new Vector3[2];
            public readonly Vector3[] FsSourcePos = new Vector3[2];
            public readonly uint[] FsGeometryVersion = new uint[2];
            public readonly bool[] FsValid = new bool[2];
            public int FsWrite = 0;
            public int FsRead = 1;
            public bool PendingRaster;
            public int PendingFace;
            public Vector3 PendingListener;
            public Vector3 PendingSource;
            public uint PendingGeometryVersion;
        }
        private readonly Dictionary<int, SecondarySlot> _secondarySlots = new Dictionary<int, SecondarySlot>();
        private readonly Queue<int> _secondaryQueue = new Queue<int>();
        private int _activeSecondaryEntityId = -1;
        private bool _secondaryFencePending;
        private int _secondaryPendingFaceForExtraction = -1;
        private readonly HashSet<int> _joinedMutual = new HashSet<int>();
        private uint _joinedVersion;
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
            uint pboBytes = (uint)(IdBufferSize * IdBufferSize * sizeof(uint));
            for (int i = 0; i < 2; i++)
            {
                _pbo[i] = _renderContext.GenBuffer();
                _renderContext.BindBuffer(_renderContext.Enums.PixelPackBuffer, _pbo[i]);
                _renderContext.BufferData(_renderContext.Enums.PixelPackBuffer, pboBytes, null, _renderContext.Enums.StreamRead);
            }
            _renderContext.BindBuffer(_renderContext.Enums.PixelPackBuffer, 0);
            _pboIndex = 0;
            _pendingFence = 0;
            _fencePending = false;
            _secondaryFencePending = false;
        }
        public void KickContinuousTrace(Vector3 sourcePos, Vector3 listenerPos)
        {
            // Residual multi-bounce path removed entirely for this stage.
        }
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
            if (_pendingRaster) return;
            int read = _fsRead;
            bool needRecompute =
                !_fsValid[read] ||
                _geometry.GeometryVersion != _fsGeometryVersion[read] ||
                Vector3.DistanceSquared(listenerPos, _fsListenerPos[read]) > VisibilityMoveThreshold * VisibilityMoveThreshold ||
                Vector3.DistanceSquared(primarySource, _fsSourcePos[read]) > VisibilityMoveThreshold * VisibilityMoveThreshold;
            if (needRecompute)
            {
                if (_fencePending && _pendingFence != 0)
                {
                    _renderContext.ClientWaitSync(_pendingFence, 0, 0);
                    _renderContext.DeleteSync(_pendingFence);
                    _pendingFence = 0;
                    _fencePending = false;
                }
                _pendingListener = listenerPos;
                _pendingSource = primarySource;
                _pendingGeometryVersion = _geometry.GeometryVersion;
                _pendingRaster = true;
                _pendingFace = 0;
                int write = _fsWrite;
                _listenerVisible[write].Clear();
                _sourceVisible[write].Clear();
                _mutual[write].Clear();
            }
        }
        /// <summary>
        /// Enqueue secondary sources. Each receives its own independent mutual.
        /// Also prunes any SecondarySlots whose EntityId is no longer present.
        /// </summary>
        public void EnqueueSecondarySources(Vector3 listenerPos, IReadOnlyList<(int entityId, Vector3 pos)> secondaries)
        {
            if (_disposed) return;
            var liveIds = new HashSet<int>();
            if (secondaries != null)
            {
                for (int i = 0; i < secondaries.Count; i++)
                {
                    int id = secondaries[i].entityId;
                    Vector3 pos = secondaries[i].pos;
                    liveIds.Add(id);
                    if (!_secondarySlots.TryGetValue(id, out var slot))
                    {
                        slot = new SecondarySlot { EntityId = id };
                        _secondarySlots[id] = slot;
                    }
                    slot.SourcePos = pos;
                    int read = slot.FsRead;
                    bool dirty =
                        !slot.FsValid[read] ||
                        _geometry.GeometryVersion != slot.FsGeometryVersion[read] ||
                        Vector3.DistanceSquared(listenerPos, slot.FsListenerPos[read]) > VisibilityMoveThreshold * VisibilityMoveThreshold ||
                        Vector3.DistanceSquared(pos, slot.FsSourcePos[read]) > VisibilityMoveThreshold * VisibilityMoveThreshold;
                    if (dirty && !slot.PendingRaster)
                    {
                        if (!_secondaryQueue.Contains(id))
                            _secondaryQueue.Enqueue(id);
                    }
                }
            }
            // Lifetime prune – remove slots that are no longer live
            if (_secondarySlots.Count > 0)
            {
                var toRemove = new List<int>();
                foreach (var kv in _secondarySlots)
                {
                    if (!liveIds.Contains(kv.Key))
                        toRemove.Add(kv.Key);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int id = toRemove[i];
                    _secondarySlots.Remove(id);
                    if (_activeSecondaryEntityId == id)
                        _activeSecondaryEntityId = -1;
                }
                // Also drop any queued ids that were pruned
                if (toRemove.Count > 0 && _secondaryQueue.Count > 0)
                {
                    var kept = new Queue<int>();
                    while (_secondaryQueue.Count > 0)
                    {
                        int q = _secondaryQueue.Dequeue();
                        if (liveIds.Contains(q))
                            kept.Enqueue(q);
                    }
                    while (kept.Count > 0)
                        _secondaryQueue.Enqueue(kept.Dequeue());
                }
            }
        }
        public bool TryCompletePendingRaster()
        {
            if (_disposed || _geometry.TriangleCount <= 0 || !_fboReady)
                return false;
            bool primaryDidWork = false;
            if (_pendingRaster)
            {
                primaryDidWork = AdvancePrimary();
            }
            if (!_pendingRaster && !_fencePending && !_secondaryFencePending)
            {
                AdvanceSecondary();
            }
            return primaryDidWork;
        }
        private bool AdvancePrimary()
        {
            int write = _fsWrite;
            int facesDone = 0;
            if (_fencePending)
            {
                int status = _renderContext.ClientWaitSync(_pendingFence, 0, 0);
                if (status == _renderContext.Enums.AlreadySignaled || status == _renderContext.Enums.ConditionSatisfied)
                {
                    ExtractIdsInto(_listenerVisible[write], _sourceVisible[write], _pendingFace - 1);
                    _renderContext.DeleteSync(_pendingFence);
                    _pendingFence = 0;
                    _fencePending = false;
                }
                else
                {
                    return false;
                }
            }
            while (facesDone < FacesPerCall && _pendingFace < 12)
            {
                if (_pendingFace < 6)
                    IssueRasterFace(_pendingListener, _pendingFace);
                else
                    IssueRasterFace(_pendingSource, _pendingFace - 6);
                _pendingFace++;
                facesDone++;
                if (_fencePending)
                    return false;
            }
            if (_pendingFace < 12)
                return false;
            foreach (int tri in _listenerVisible[write])
                if (_sourceVisible[write].Contains(tri))
                    _mutual[write].Add(tri);
            _fsListenerPos[write] = _pendingListener;
            _fsSourcePos[write] = _pendingSource;
            _fsGeometryVersion[write] = _pendingGeometryVersion;
            _fsValid[write] = true;
            _fsRead = write;
            _fsWrite = 1 - write;
            _visibilityVersion++;
            _pendingRaster = false;
            RebuildJoinedMutual();
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
        private void AdvanceSecondary()
        {
            if (_activeSecondaryEntityId < 0)
            {
                while (_secondaryQueue.Count > 0)
                {
                    int id = _secondaryQueue.Dequeue();
                    if (_secondarySlots.TryGetValue(id, out var s) && !s.PendingRaster)
                    {
                        _activeSecondaryEntityId = id;
                        s.PendingListener = _lastListenerPos;
                        s.PendingSource = s.SourcePos;
                        s.PendingGeometryVersion = _geometry.GeometryVersion;
                        s.PendingRaster = true;
                        s.PendingFace = 0;
                        int w = s.FsWrite;
                        s.ListenerVisible[w].Clear();
                        s.SourceVisible[w].Clear();
                        s.Mutual[w].Clear();
                        break;
                    }
                }
            }
            if (_activeSecondaryEntityId < 0) return;
            if (!_secondarySlots.TryGetValue(_activeSecondaryEntityId, out var slot)) return;
            if (!slot.PendingRaster) { _activeSecondaryEntityId = -1; return; }
            int write = slot.FsWrite;
            int facesDone = 0;
            if (_secondaryFencePending)
            {
                int status = _renderContext.ClientWaitSync(_pendingFence, 0, 0);
                if (status == _renderContext.Enums.AlreadySignaled || status == _renderContext.Enums.ConditionSatisfied)
                {
                    ExtractIdsInto(slot.ListenerVisible[write], slot.SourceVisible[write], _secondaryPendingFaceForExtraction);
                    _renderContext.DeleteSync(_pendingFence);
                    _pendingFence = 0;
                    _secondaryFencePending = false;
                    _fencePending = false;
                }
                else
                {
                    return;
                }
            }
            while (facesDone < FacesPerCall && slot.PendingFace < 12)
            {
                int face = slot.PendingFace < 6 ? slot.PendingFace : slot.PendingFace - 6;
                Vector3 origin = slot.PendingFace < 6 ? slot.PendingListener : slot.PendingSource;
                IssueRasterFace(origin, face);
                _secondaryPendingFaceForExtraction = slot.PendingFace;
                slot.PendingFace++;
                facesDone++;
                if (_fencePending)
                {
                    _secondaryFencePending = true;
                    return;
                }
            }
            if (slot.PendingFace < 12)
                return;
            foreach (int tri in slot.ListenerVisible[write])
                if (slot.SourceVisible[write].Contains(tri))
                    slot.Mutual[write].Add(tri);
            slot.FsListenerPos[write] = slot.PendingListener;
            slot.FsSourcePos[write] = slot.PendingSource;
            slot.FsGeometryVersion[write] = slot.PendingGeometryVersion;
            slot.FsValid[write] = true;
            slot.FsRead = write;
            slot.FsWrite = 1 - write;
            slot.PendingRaster = false;
            _activeSecondaryEntityId = -1;
            _visibilityVersion++;
            RebuildJoinedMutual();
        }
        private void IssueRasterFace(Vector3 origin, int face)
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
            Vector3 target = origin + CubeDirs[face] * 10.0f;
            Matrix4x4 view = Matrix4x4.CreateLookAt(origin, target, CubeUps[face]);
            _idProgram.SetMatrix4("uView", view);
            uint clearVal = 0;
            _renderContext.ClearBufferuiv(_renderContext.Enums.Color, 0, &clearVal);
            _renderContext.Clear(_renderContext.Enums.DepthBufferBit);
            _geometry.Draw();
            int pbo = _pboIndex;
            _pboIndex = 1 - _pboIndex;
            _renderContext.BindBuffer(_renderContext.Enums.PixelPackBuffer, _pbo[pbo]);
            _renderContext.ReadPixels(0, 0, IdBufferSize, IdBufferSize,
                _renderContext.Enums.RedInteger, _renderContext.Enums.UnsignedIntType, null);
            _pendingFence = _renderContext.FenceSync(_renderContext.Enums.SyncGpuCommandsComplete, 0);
            _pendingPbo = pbo;
            _fencePending = true;
            _renderContext.BindBuffer(_renderContext.Enums.PixelPackBuffer, 0);
            _renderContext.BindFramebuffer(_renderContext.Enums.Framebuffer, 0);
            _renderContext.Viewport(0, 0, (uint)savedViewportW, (uint)savedViewportH);
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.Blend);
            _renderContext.BlendFunc(_renderContext.Enums.SrcAlpha, _renderContext.Enums.OneMinusSrcAlpha);
        }
        private void ExtractIdsInto(HashSet<int> listenerSet, HashSet<int> sourceSet, int faceIndex)
        {
            _renderContext.BindBuffer(_renderContext.Enums.PixelPackBuffer, _pbo[_pendingPbo]);
            void* mapped = _renderContext.MapBufferRange(
                _renderContext.Enums.PixelPackBuffer,
                0,
                (uint)(IdBufferSize * IdBufferSize * sizeof(uint)),
                _renderContext.Enums.MapReadBit);
            if (mapped != null)
            {
                uint* ptr = (uint*)mapped;
                int maxTri = _geometry.TriangleCount;
                HashSet<int> targetSet = (faceIndex < 6) ? listenerSet : sourceSet;
                for (int i = 0; i < IdBufferSize * IdBufferSize; i++)
                {
                    uint raw = ptr[i];
                    if (raw == 0) continue;
                    int tri = (int)raw - 1;
                    if (tri >= 0 && tri < maxTri)
                        targetSet.Add(tri);
                }
                _renderContext.UnmapBuffer(_renderContext.Enums.PixelPackBuffer);
            }
            _renderContext.BindBuffer(_renderContext.Enums.PixelPackBuffer, 0);
        }
        private void RebuildJoinedMutual()
        {
            _joinedMutual.Clear();
            if (_fsValid[_fsRead])
            {
                foreach (int t in _mutual[_fsRead])
                    _joinedMutual.Add(t);
            }
            foreach (var kv in _secondarySlots)
            {
                var slot = kv.Value;
                if (slot.FsValid[slot.FsRead])
                {
                    foreach (int t in slot.Mutual[slot.FsRead])
                        _joinedMutual.Add(t);
                }
            }
            _joinedVersion = _visibilityVersion;
        }
        public SoundRayTraceResult ComputeFreeSurfacePerceived(Vector3 listener, Vector3 source)
        {
            return ComputeFreeSurfacePerceived(listener, source, -1);
        }
        public SoundRayTraceResult ComputeFreeSurfacePerceived(Vector3 listener, Vector3 source, int entityId)
        {
            int read = _fsRead;
            if (_fsValid[read] &&
                Vector3.DistanceSquared(source, _fsSourcePos[read]) < VisibilityMoveThreshold * VisibilityMoveThreshold * 4f)
            {
                return ComputeFromMutual(_mutual[read], listener, source);
            }
            if (entityId >= 0 && _secondarySlots.TryGetValue(entityId, out var slot) && slot.FsValid[slot.FsRead])
            {
                return ComputeFromMutual(slot.Mutual[slot.FsRead], listener, source);
            }
            foreach (var kv in _secondarySlots)
            {
                var s = kv.Value;
                if (s.FsValid[s.FsRead] &&
                    Vector3.DistanceSquared(source, s.FsSourcePos[s.FsRead]) < VisibilityMoveThreshold * VisibilityMoveThreshold * 4f)
                {
                    return ComputeFromMutual(s.Mutual[s.FsRead], listener, source);
                }
            }
            return new SoundRayTraceResult
            {
                Intensity = 0.001f,
                Delay = 0f,
                LowPassCutoff = 0f,
                ApparentDirection = Vector3.Zero
            };
        }
        private SoundRayTraceResult ComputeFromMutual(HashSet<int> mutual, Vector3 listener, Vector3 source)
        {
            if (mutual == null || mutual.Count == 0)
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
            foreach (int tri in mutual)
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
        public IReadOnlyCollection<int> GetListenerFree() => _listenerVisible[_fsRead];
        public IReadOnlyCollection<int> GetSourceFree() => _sourceVisible[_fsRead];
        public IReadOnlyCollection<int> GetMutualFree() => _mutual[_fsRead];
        public IReadOnlyCollection<int> GetJoinedMutualFree()
        {
            if (_joinedVersion != _visibilityVersion)
                RebuildJoinedMutual();
            return _joinedMutual;
        }
        public IReadOnlyList<DebugSegment> GetDebugSegments() => _debugSegments;
        public SoundRayTraceResult ReadCompletedResult()
        {
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
                if (_fencePending && _pendingFence != 0)
                {
                    _renderContext.ClientWaitSync(_pendingFence, 0, 0);
                    _renderContext.DeleteSync(_pendingFence);
                    _pendingFence = 0;
                    _fencePending = false;
                }
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
                for (int i = 0; i < 2; i++)
                {
                    if (_pbo[i] != 0)
                        _renderContext.DeleteBuffer(_pbo[i]);
                }
                _secondarySlots.Clear();
                _secondaryQueue.Clear();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}