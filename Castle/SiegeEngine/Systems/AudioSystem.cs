// Folder: SiegeEngine/Systems
// File: AudioSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using System.Threading;
using SiegeEngine.Audio;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.GPU.Compute;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.PlayerSystem;
namespace SiegeEngine.Systems
{
    public partial class AudioSystem : GameSystem
    {
        private readonly EventBus _eventBus;
        private readonly bool _isServer;
        private readonly ISoundValidator _validationSystem;
        private readonly Random _random = new Random();
        private const float SpatialRefDistance = 5.0f;
        private const float SpatialMaxDistance = 300f;
        private const float MinAudibleVolume = 0.01f;
        // Continuous smoothing rates (applied on main thread every frame)
        private const float IntensitySmoothRate = 18.0f;
        private const float DirectionSmoothRate = 22.0f;
        private Vector3 _listenerPosition;
        private Vector3 _listenerForward = new Vector3(0, 1, 0);
        private volatile bool _listenerValid;
        private int _nextHandle = 1;
        private readonly Dictionary<int, PlaybackInstance> _activePlayers = new Dictionary<int, PlaybackInstance>();
        private readonly Dictionary<int, WaveOutPlayer> _spatialPlayers = new Dictionary<int, WaveOutPlayer>();
        private readonly List<AutoPlayRegistration> _autoPlayRegs = new List<AutoPlayRegistration>();
        private readonly object _regsLock = new object();
        private readonly List<AutoPlayRegistration> _workerSnapshot = new List<AutoPlayRegistration>();
        private bool _autoPlayScanned;
        private bool _geometryUploaded;
        private readonly List<string> _playlist = new List<string>();
        private int _playlistIndex = -1;
        private int _currentPlaylistHandle = -1;
        private string _currentTitle = "";
        private AcousticGeometry _acousticGeometry;
        private AcousticRayTracer _acousticRayTracer;
        private bool _gpuOcclusionReady;
        private IHeightProvider _heightProvider;
        private readonly Dictionary<string, MonoPcmClip> _soundBank =
            new Dictionary<string, MonoPcmClip>(StringComparer.OrdinalIgnoreCase);
        // Dedicated audio worker
        private Thread _audioWorker;
        private volatile bool _workerRunning;
        private readonly object _rayLock = new object(); // protects RequestRayTrace
        // Round-robin index for throttled GPU kicks (main thread only)
        private int _gpuKickRoundRobin;
        private class MonoPcmClip
        {
            public short[] Samples;
            public int SampleRate;
        }
        private class PlaybackInstance
        {
            public SoundPlayer Player;
            public float Volume;
            public bool Loop;
            public bool IsMusic;
            public string Path;
            public bool IsPaused;
        }
        private class AutoPlayRegistration
        {
            public int EntityId;
            public SoundSource Source;
            public int Handle = -1;
            public bool Started;
            // Written by the dedicated worker, read by main thread
            public SoundRayTraceResult WorkerResult = new SoundRayTraceResult();
            public volatile bool HasWorkerResult;
            // Smoothing state (main thread only)
            public float SmoothedIntensity = 1f;
            public Vector3 SmoothedDirection = Vector3.Zero;
            public float SmoothedLowPass = 12000f;
            public bool HasSmoothedState;
            // GPU residual slot – written exclusively by main, read by worker (zero-alloc, versioned)
            public float GpuResidualIntensity;
            public float GpuLowPass;
            public Vector3 GpuApparentDirection;
            public volatile uint GpuVersion;
            public volatile bool HasGpuResult;
        }
        public AudioSystem(IGameServer server, EventBus eventBus, bool isServer,
            ISoundValidator validationSystem = null, IRenderContext renderContext = null)
            : base(server)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _isServer = isServer;
            _validationSystem = validationSystem;
            _eventBus.Subscribe<SoundEmissionEvent>(OnSoundEmission);
            if (!isServer)
                _eventBus.Subscribe<SoundEvent>(OnSoundEvent);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
            if (!isServer && renderContext != null)
                InitializeGpuOcclusion(renderContext);
            // Start the single dedicated audio worker thread (client only)
            if (!isServer)
            {
                _workerRunning = true;
                _audioWorker = new Thread(AudioWorkerLoop)
                {
                    IsBackground = true,
                    Name = "AudioSystemWorker",
                    Priority = ThreadPriority.BelowNormal
                };
                _audioWorker.Start();
                Console.WriteLine("AudioSystem: Dedicated worker thread started.");
            }
        }
        public void InitializeGpuOcclusion(IRenderContext renderContext)
        {
            if (_isServer || renderContext == null) return;
            try
            {
                _acousticRayTracer?.Dispose();
                _acousticGeometry?.Dispose();
                _acousticGeometry = new AcousticGeometry(renderContext);
                _acousticRayTracer = new AcousticRayTracer(renderContext, _acousticGeometry);
                _gpuOcclusionReady = true;
                _geometryUploaded = false;
                Console.WriteLine("AudioSystem: GPU occlusion infrastructure ready (currently disabled).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AudioSystem: GPU occlusion init failed: {ex.Message}");
                _gpuOcclusionReady = false;
            }
        }
        public void SetHeightProvider(IHeightProvider provider)
        {
            _heightProvider = provider;
        }
        public void RebuildAcousticGeometry()
        {
            if (!_gpuOcclusionReady || _acousticGeometry == null || _server == null) return;
            if (_heightProvider == null &&
                _server is ClientGameServerProxy proxy &&
                proxy.PhysicsWorld != null)
            {
                _heightProvider = proxy.PhysicsWorld.HeightProvider;
            }
            _acousticGeometry.Rebuild(_server.GetEntities(), _heightProvider);
            _geometryUploaded = true;
            Console.WriteLine($"AudioSystem: Acoustic geometry rebuilt – {_acousticGeometry.TriangleCount} triangles" +
                              (_heightProvider != null ? " (with heightmap)" : " (OBBs only)"));
        }
        public string CurrentTitle => _currentTitle;
        public bool HasPlaylist => _playlist.Count > 0;
        public int PlaylistCount => _playlist.Count;
        public void SetListenerPosition(Vector3 position)
        {
            _listenerPosition = position;
            _listenerValid = true;
        }
        public void SetListener(Vector3 position, Vector3 forward)
        {
            _listenerPosition = position;
            if (forward.LengthSquared() > 0.0001f)
                _listenerForward = Vector3.Normalize(forward);
            _listenerValid = true;
        }
        // ------------------------------------------------------------------
        // Dedicated worker thread – owns all expensive ray / probe work
        // Free-running: no sleeps, no fixed timing constraints.
        // ------------------------------------------------------------------
        private void AudioWorkerLoop()
        {
            while (_workerRunning)
            {
                try
                {
                    if (_listenerValid && _server != null)
                    {
                        lock (_regsLock)
                        {
                            _workerSnapshot.Clear();
                            _workerSnapshot.AddRange(_autoPlayRegs);
                        }
                        Vector3 listenerPos = _listenerPosition;
                        foreach (var reg in _workerSnapshot)
                        {
                            if (!reg.Started) continue;
                            // Keep source position up to date
                            var entity = _server.GetEntityById(reg.EntityId);
                            if (entity != null)
                            {
                                var phys = entity.GetComponent<PhysicsComponent>();
                                if (phys != null)
                                    reg.Source.Position = phys.Position;
                            }
                            Vector3 srcPos = reg.Source.Position;
                            // Continuous energy-weighted multi-path (no hard switches, no lag gates)
                            SoundRayTraceResult result = ComputeContinuousResult(srcPos, listenerPos, reg);
                            // Mutate pre-allocated slot (zero allocation)
                            reg.WorkerResult.Intensity = result.Intensity;
                            reg.WorkerResult.Delay = result.Delay;
                            reg.WorkerResult.LowPassCutoff = result.LowPassCutoff;
                            reg.WorkerResult.ApparentDirection = result.ApparentDirection;
                            reg.HasWorkerResult = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AudioSystem worker error: {ex.Message}");
                }
                // Free-running: only yield when idle so the thread does not spin at 100 %
                if (_workerSnapshot.Count == 0)
                    Thread.Yield();
            }
        }
        // Pure continuous energy-weighted combination of PrimaryLos + all successful diffraction probes.
        // No intensity membership gates that cause lag or sticky behaviour.
        // GPU residual is used ONLY when Primary LOS is blocked and a matching versioned result exists.
        private SoundRayTraceResult ComputeContinuousResult(Vector3 sourcePos, Vector3 listenerPos, AutoPlayRegistration reg)
        {
            SoundRayTraceResult los = PrimaryLosRayInternal(sourcePos, listenerPos);
            Vector3 toListener = listenerPos - sourcePos;
            float dist = toListener.Length();
            if (dist < 0.01f)
                return los;
            // Primary is clear → pure Primary, ignore residual
            if (los.Intensity >= 0.95f)
                return los;
            // Primary blocked → prefer version-matched GPU residual if available
            if (reg != null && reg.HasGpuResult &&
                _acousticGeometry != null &&
                reg.GpuVersion == _acousticGeometry.GeometryVersion)
            {
                float residualIntensity = Math.Clamp(reg.GpuResidualIntensity, 0.001f, 0.85f);
                float residualMax = Math.Max(los.Intensity, residualIntensity);
                Vector3 residualArrival = reg.GpuApparentDirection.LengthSquared() > 0.0001f
                    ? Vector3.Normalize(reg.GpuApparentDirection)
                    : Vector3.Zero;
                float residualLowPass = reg.GpuLowPass > 0f ? reg.GpuLowPass : (2800f + 3200f * residualMax);
                return new SoundRayTraceResult
                {
                    Intensity = Math.Clamp(residualMax, 0.001f, 1f),
                    Delay = dist / 34300f,
                    LowPassCutoff = residualLowPass,
                    ApparentDirection = residualArrival
                };
            }
            // Fallback: existing CPU continuous multi-probe diffraction
            float totalEnergy = 0f;
            Vector3 weightedDir = Vector3.Zero;
            float maxIntensity = 0f;
            float bestTotalDist = dist;
            if (los.Intensity > 0.0001f)
            {
                float energy = los.Intensity * los.Intensity;
                totalEnergy += energy;
                Vector3 losDir = sourcePos - listenerPos;
                if (losDir.LengthSquared() > 1e-8f)
                    losDir = Vector3.Normalize(losDir);
                weightedDir += losDir * energy;
                maxIntensity = los.Intensity;
            }
            // Diffraction probes – continuous contribution, no hard intensity cutoffs
            Vector3 dir = toListener / dist;
            Vector3 up = Vector3.UnitZ;
            Vector3 right = Vector3.Cross(dir, up);
            if (right.LengthSquared() < 1e-6f)
                right = Vector3.UnitX;
            else
                right = Vector3.Normalize(right);
            Vector3 realUp = Vector3.Normalize(Vector3.Cross(right, dir));
            float[] lateral = { -70f, -50f, -35f, -20f, -10f, 10f, 20f, 35f, 50f, 70f };
            float[] elev = { -15f, 0f, 20f, 40f };
            const float Deg2Rad = MathF.PI / 180f;
            for (int li = 0; li < lateral.Length; li++)
            {
                for (int ei = 0; ei < elev.Length; ei++)
                {
                    float a = lateral[li] * Deg2Rad;
                    float e = elev[ei] * Deg2Rad;
                    Vector3 probeDir = Vector3.Normalize(
                        dir + right * MathF.Tan(a) + realUp * MathF.Tan(e));
                    float probeMax = dist * 1.8f;
                    RayTraceResult hit;
                    lock (_rayLock)
                    {
                        hit = _server.RequestRayTrace(sourcePos, probeDir, probeMax);
                    }
                    float clearDist = hit.DidHit ? hit.Distance : probeMax;
                    // Soft contribution – no hard continue that creates lag
                    float freeT = Math.Min(clearDist * 0.92f, dist * 0.95f);
                    if (freeT < 0.05f) continue;
                    Vector3 freePoint = sourcePos + probeDir * freeT;
                    Vector3 finalVec = freePoint - listenerPos;
                    float finalDist = finalVec.Length();
                    if (finalDist < 0.05f) continue;
                    Vector3 finalDir = finalVec / finalDist;
                    Vector3 travelDir = -finalDir;
                    RayTraceResult finalHit;
                    lock (_rayLock)
                    {
                        finalHit = _server.RequestRayTrace(freePoint, travelDir, finalDist + 0.5f);
                    }
                    // Soft occlusion factor instead of binary reject
                    float occlusionFactor = 1f;
                    if (finalHit.DidHit && finalHit.Distance < finalDist - 0.1f)
                    {
                        float blocked = (finalDist - finalHit.Distance) / finalDist;
                        occlusionFactor = Math.Clamp(1f - blocked * 1.5f, 0.05f, 1f);
                    }
                    float totalD = freeT + finalDist;
                    float pathFactor = MathF.Sqrt(dist / Math.Max(totalD, 0.5f));
                    float inten = pathFactor * 0.75f * occlusionFactor;
                    if (inten < 0.01f) continue;
                    float energy = inten * inten;
                    totalEnergy += energy;
                    weightedDir += finalDir * energy;
                    if (inten > maxIntensity)
                    {
                        maxIntensity = inten;
                        bestTotalDist = totalD;
                    }
                }
            }
            if (totalEnergy < 1e-8f)
                return los;
            Vector3 arrival = Vector3.Normalize(weightedDir);
            float intensity = Math.Clamp(maxIntensity, 0.001f, 1f);
            float lowPass = 2800f + 3200f * intensity;
            return new SoundRayTraceResult
            {
                Intensity = intensity,
                Delay = bestTotalDist / 34300f,
                LowPassCutoff = lowPass,
                ApparentDirection = arrival
            };
        }
        // ------------------------------------------------------------------
        // Main-thread Update – only listener, smoothing, and ApplySpatial
        // ------------------------------------------------------------------
        public override void Update(float deltaTime)
        {
            if (_isServer) return;
            // Update listener from player camera
            if (_server != null)
            {
                foreach (var e in _server.GetEntities())
                {
                    var player = e.GetComponent<Player>();
                    if (player?.Camera != null)
                    {
                        float yawRad = player.Camera.Yaw * (float)(Math.PI / 180.0);
                        float pitchRad = player.Camera.Pitch * (float)(Math.PI / 180.0);
                        Vector3 forward = new Vector3(
                            (float)(Math.Cos(pitchRad) * Math.Sin(yawRad)),
                            (float)(Math.Cos(pitchRad) * Math.Cos(yawRad)),
                            (float)Math.Sin(pitchRad)
                        );
                        SetListener(player.Camera.Position, forward);
                        break;
                    }
                }
            }
            if (!_autoPlayScanned)
            {
                ScanAndRegisterAutoPlay();
                _autoPlayScanned = true;
            }
            if (_gpuOcclusionReady && !_geometryUploaded && _server != null && _server.GetEntities().Count > 0)
                RebuildAcousticGeometry();
            if (_listenerValid)
                TickMainThread(deltaTime);
        }
        private void TickMainThread(float deltaTime)
        {
            List<AutoPlayRegistration> snapshot;
            lock (_regsLock)
            {
                snapshot = new List<AutoPlayRegistration>(_autoPlayRegs);
            }
            // Throttled GPU residual update (main/render thread only, 1 source per frame)
            if (_gpuOcclusionReady && _geometryUploaded && _acousticRayTracer != null && snapshot.Count > 0)
            {
                int startedCount = 0;
                for (int i = 0; i < snapshot.Count; i++)
                    if (snapshot[i].Started) startedCount++;
                if (startedCount > 0)
                {
                    int target = _gpuKickRoundRobin % startedCount;
                    int seen = 0;
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        var reg = snapshot[i];
                        if (!reg.Started) continue;
                        if (seen == target)
                        {
                            _acousticRayTracer.KickContinuousTrace(reg.Source.Position, _listenerPosition);
                            var gpu = _acousticRayTracer.ReadCompletedResult();
                            // Scale residual into Primary mathematical range
                            float residual = Math.Clamp(gpu.Intensity * 0.85f, 0.001f, 0.85f);
                            reg.GpuResidualIntensity = residual;
                            reg.GpuLowPass = gpu.LowPassCutoff > 0f ? gpu.LowPassCutoff : (2800f + 3200f * residual);
                            reg.GpuApparentDirection = gpu.ApparentDirection.LengthSquared() > 0.0001f
                                ? Vector3.Normalize(gpu.ApparentDirection)
                                : Vector3.Zero;
                            reg.GpuVersion = _acousticGeometry != null ? _acousticGeometry.GeometryVersion : 0u;
                            reg.HasGpuResult = true;
                            break;
                        }
                        seen++;
                    }
                    _gpuKickRoundRobin++;
                }
            }
            foreach (var reg in snapshot)
            {
                // Keep source position current on main thread as well
                var entity = _server?.GetEntityById(reg.EntityId);
                if (entity != null)
                {
                    var phys = entity.GetComponent<PhysicsComponent>();
                    if (phys != null)
                        reg.Source.Position = phys.Position;
                }
                if (!reg.Started)
                {
                    if (!_listenerValid) continue;
                    // Bootstrap with a simple LOS so playback can start immediately
                    var bootstrap = PrimaryLosRayInternal(reg.Source.Position, _listenerPosition);
                    int h = PlaySpatial(reg.Source, bootstrap);
                    if (h >= 0)
                    {
                        reg.Handle = h;
                        reg.Started = true;
                        reg.SmoothedIntensity = bootstrap.Intensity;
                        reg.SmoothedDirection = bootstrap.ApparentDirection.LengthSquared() > 0.0001f
                            ? bootstrap.ApparentDirection
                            : (reg.Source.Position - _listenerPosition);
                        reg.SmoothedLowPass = bootstrap.LowPassCutoff > 0f ? bootstrap.LowPassCutoff : 12000f;
                        reg.HasSmoothedState = true;
                    }
                    continue;
                }
                if (reg.Handle < 0 || !_spatialPlayers.TryGetValue(reg.Handle, out var player) || !player.IsPlaying)
                    continue;
                // Consume latest worker result (pre-allocated slot)
                SoundRayTraceResult target = reg.HasWorkerResult
                    ? reg.WorkerResult
                    : new SoundRayTraceResult { Intensity = 1f, ApparentDirection = Vector3.Zero, LowPassCutoff = 12000f };
                float targetIntensity = Math.Clamp(target.Intensity, 0.001f, 1f);
                Vector3 targetDir = target.ApparentDirection.LengthSquared() > 0.0001f
                    ? Vector3.Normalize(target.ApparentDirection)
                    : Vector3.Normalize(reg.Source.Position - _listenerPosition);
                float targetLowPass = 2800f + 3200f * targetIntensity;
                if (!reg.HasSmoothedState)
                {
                    reg.SmoothedIntensity = targetIntensity;
                    reg.SmoothedDirection = targetDir;
                    reg.SmoothedLowPass = targetLowPass;
                    reg.HasSmoothedState = true;
                }
                else
                {
                    float aInt = 1f - MathF.Exp(-IntensitySmoothRate * deltaTime);
                    float aDir = 1f - MathF.Exp(-DirectionSmoothRate * deltaTime);
                    float aLp = 1f - MathF.Exp(-IntensitySmoothRate * deltaTime);
                    reg.SmoothedIntensity += (targetIntensity - reg.SmoothedIntensity) * aInt;
                    reg.SmoothedLowPass += (targetLowPass - reg.SmoothedLowPass) * aLp;
                    // Always use continuous spherical interpolation – no special-case gate
                    float dot = Math.Clamp(Vector3.Dot(reg.SmoothedDirection, targetDir), -1f, 1f);
                    float theta = MathF.Acos(dot);
                    if (theta < 1e-5f)
                    {
                        reg.SmoothedDirection = targetDir;
                    }
                    else
                    {
                        float sinTheta = MathF.Sin(theta);
                        float w1 = MathF.Sin((1f - aDir) * theta) / sinTheta;
                        float w2 = MathF.Sin(aDir * theta) / sinTheta;
                        reg.SmoothedDirection = Vector3.Normalize(
                            reg.SmoothedDirection * w1 + targetDir * w2);
                    }
                }
                var smoothed = new SoundRayTraceResult
                {
                    Intensity = reg.SmoothedIntensity,
                    Delay = target.Delay,
                    LowPassCutoff = reg.SmoothedLowPass,
                    ApparentDirection = reg.SmoothedDirection
                };
                ApplySpatialToPlayer(player, reg.Source, smoothed);
            }
        }
        // ------------------------------------------------------------------
        // Ray / probe helpers (called only from the worker thread)
        // ------------------------------------------------------------------
        private SoundRayTraceResult PrimaryLosRayInternal(Vector3 sourcePos, Vector3 listenerPos)
        {
            if (_server == null)
                return new SoundRayTraceResult { Intensity = 1f, Delay = 0f, LowPassCutoff = 0f, ApparentDirection = Vector3.Zero };
            Vector3 toListener = listenerPos - sourcePos;
            float dist = toListener.Length();
            if (dist < 0.01f)
                return new SoundRayTraceResult { Intensity = 1f, Delay = 0f, LowPassCutoff = 0f, ApparentDirection = Vector3.Zero };
            Vector3 dir = toListener / dist;
            RayTraceResult result;
            lock (_rayLock)
            {
                result = _server.RequestRayTrace(sourcePos, dir, dist + 0.5f);
            }
            if (!result.DidHit || result.Distance >= dist - 0.1f)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 1f,
                    Delay = 0f,
                    LowPassCutoff = 0f,
                    ApparentDirection = Vector3.Zero
                };
            }
            float dens = result.Material != null ? Math.Max(0.1f, result.Material.Density) : 1.0f;
            float intensity = Math.Clamp(MathF.Exp(-0.8f * dens), 0.001f, 0.95f);
            float lowPass = 600f + 800f / dens;
            return new SoundRayTraceResult
            {
                Intensity = intensity,
                Delay = 0f,
                LowPassCutoff = lowPass,
                ApparentDirection = Vector3.Zero
            };
        }
        // ------------------------------------------------------------------
        // Playback / spatial application (main thread)
        // ------------------------------------------------------------------
        private void ApplySpatialToPlayer(WaveOutPlayer player, SoundSource source, SoundRayTraceResult rayResult)
        {
            Vector3 toSource = rayResult != null && rayResult.ApparentDirection.LengthSquared() > 0.0001f
                ? rayResult.ApparentDirection
                : source.Position - _listenerPosition;
            float distance = Vector3.Distance(source.Position, _listenerPosition);
            float geometric;
            if (!_listenerValid || distance <= SpatialRefDistance)
                geometric = 1f;
            else
            {
                // Pure continuous free-field 20·log10 (1/r) – no hard max-distance clamp
                float gainDb = -20f * MathF.Log10(distance / SpatialRefDistance);
                geometric = MathF.Pow(10f, gainDb / 20f);
            }
            float occlusion = 1f;
            float lowPass = 0f;
            if (rayResult != null)
            {
                occlusion = Math.Clamp(rayResult.Intensity, 0.001f, 1f);
                if (rayResult.LowPassCutoff > 0f)
                    lowPass = rayResult.LowPassCutoff;
            }
            float finalVolume = Math.Clamp(geometric * occlusion * Math.Max(source.Volume, 0.01f), 0f, 1f);
            Vector3 flatTo = new Vector3(toSource.X, toSource.Y, 0f);
            Vector3 flatFwd = new Vector3(_listenerForward.X, _listenerForward.Y, 0f);
            float pan = 0f;
            if (flatTo.LengthSquared() > 0.0001f && flatFwd.LengthSquared() > 0.0001f)
            {
                flatTo = Vector3.Normalize(flatTo);
                flatFwd = Vector3.Normalize(flatFwd);
                float cross = flatFwd.X * flatTo.Y - flatFwd.Y * flatTo.X;
                float dot = Vector3.Dot(flatFwd, flatTo);
                float angle = (float)Math.Atan2(cross, dot);
                pan = -Math.Clamp((float)Math.Sin(angle), -1f, 1f);
            }
            float leftGain = (float)Math.Sqrt((1f - pan) * 0.5f);
            float rightGain = (float)Math.Sqrt((1f + pan) * 0.5f);
            player.UpdateSpatial(leftGain, rightGain, finalVolume, lowPass);
        }
        private void ScanAndRegisterAutoPlay()
        {
            if (_server == null) return;
            var entities = _server.GetEntities();
            if (entities == null) return;
            lock (_regsLock)
            {
                foreach (var entity in entities)
                {
                    var soundComp = entity.GetComponent<SoundComponent>();
                    if (soundComp == null || !soundComp.AutoPlay) continue;
                    var physics = entity.GetComponent<PhysicsComponent>();
                    if (physics == null) continue;
                    var src = new SoundSource
                    {
                        EntityId = entity.Id,
                        Position = physics.Position,
                        Type = soundComp.Type ?? "SoundSource",
                        IsSensitive = soundComp.IsSensitive,
                        AudioClip = soundComp.AudioClip ?? "",
                        SteamId = 0,
                        Loop = soundComp.Loop,
                        Volume = soundComp.Volume
                    };
                    string pathHint = !string.IsNullOrEmpty(src.AudioClip) ? src.AudioClip : src.Type;
                    string resolved = ResolveSoundPath(pathHint);
                    if (resolved != null)
                        GetOrLoadMonoClip(resolved);
                    if (soundComp.Loop)
                    {
                        _autoPlayRegs.Add(new AutoPlayRegistration
                        {
                            EntityId = entity.Id,
                            Source = src,
                            Handle = -1,
                            Started = false
                        });
                        Console.WriteLine($"AudioSystem: Registered looping AutoPlay entity {entity.Id} clip='{src.AudioClip}'");
                    }
                    else
                    {
                        _eventBus.Publish(new SoundEmissionEvent { Source = src });
                    }
                }
            }
        }
        // ------------------------------------------------------------------
        // Public playback API (unchanged)
        // ------------------------------------------------------------------
        public int Play(string clipNameOrPath, float volume = 1f, bool loop = false, bool isMusic = false)
        {
            if (string.IsNullOrWhiteSpace(clipNameOrPath))
            {
                Console.WriteLine("AudioSystem: Play called with empty path.");
                return -1;
            }
            string path = ResolveSoundPath(clipNameOrPath);
            if (path == null || !File.Exists(path))
            {
                Console.WriteLine($"AudioSystem: Sound file not found for '{clipNameOrPath}'.");
                return -1;
            }
            try
            {
                var player = new SoundPlayer(path);
                if (loop) player.PlayLooping();
                else player.Play();
                int handle = _nextHandle++;
                _activePlayers[handle] = new PlaybackInstance
                {
                    Player = player,
                    Volume = Math.Clamp(volume, 0f, 1f),
                    Loop = loop,
                    IsMusic = isMusic,
                    Path = path,
                    IsPaused = false
                };
                if (isMusic)
                {
                    _currentPlaylistHandle = handle;
                    _currentTitle = Path.GetFileNameWithoutExtension(path);
                }
                return handle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AudioSystem: Failed to play '{path}': {ex.Message}");
                return -1;
            }
        }
        public int PlaySpatial(SoundSource source, SoundRayTraceResult rayResult = null)
        {
            if (source == null) return -1;
            string pathHint = !string.IsNullOrEmpty(source.AudioClip) ? source.AudioClip : source.Type;
            string resolved = ResolveSoundPath(pathHint);
            if (resolved == null)
            {
                Console.WriteLine($"AudioSystem: [Spatial] file not found for '{pathHint}'.");
                return -1;
            }
            MonoPcmClip clip = GetOrLoadMonoClip(resolved);
            if (clip == null || clip.Samples == null || clip.Samples.Length == 0)
            {
                Console.WriteLine($"AudioSystem: [Spatial] failed to load mono clip '{resolved}'.");
                return -1;
            }
            try
            {
                var wave = new WaveOutPlayer();
                if (!wave.Start(clip.Samples, clip.SampleRate, source.Loop))
                {
                    wave.Dispose();
                    Console.WriteLine("AudioSystem: [Spatial] WaveOutPlayer.Start failed.");
                    return -1;
                }
                ApplySpatialToPlayer(wave, source, rayResult);
                int handle = _nextHandle++;
                _spatialPlayers[handle] = wave;
                float distance = Vector3.Distance(source.Position, _listenerPosition);
                Console.WriteLine($"AudioSystem: [Spatial] started '{Path.GetFileName(resolved)}' " +
                                  $"pos={source.Position} listener={_listenerPosition} dist={distance:F1} loop={source.Loop}");
                return handle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AudioSystem: [Spatial] exception: {ex.Message}");
                return -1;
            }
        }
        private MonoPcmClip GetOrLoadMonoClip(string resolvedPath)
        {
            if (_soundBank.TryGetValue(resolvedPath, out var existing))
                return existing;
            byte[] audioData;
            try { audioData = File.ReadAllBytes(resolvedPath); }
            catch (Exception ex)
            {
                Console.WriteLine($"AudioSystem: [Bank] failed to read '{resolvedPath}': {ex.Message}");
                return null;
            }
            if (resolvedPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                audioData = ConvertMp3ToWav(audioData);
                if (audioData == null)
                {
                    Console.WriteLine($"AudioSystem: [Bank] MP3→WAV failed for '{resolvedPath}'.");
                    return null;
                }
            }
            if (!TryParseWav(audioData, out short channels, out int sampleRate, out _, out _, out _, out short[] samples))
            {
                Console.WriteLine($"AudioSystem: [Bank] WAV parse failed for '{resolvedPath}'.");
                return null;
            }
            short[] mono;
            if (channels == 1)
            {
                mono = samples;
            }
            else if (channels == 2)
            {
                int frames = samples.Length / 2;
                mono = new short[frames];
                for (int i = 0; i < frames; i++)
                    mono[i] = (short)((samples[i * 2] + samples[i * 2 + 1]) / 2);
            }
            else
            {
                Console.WriteLine("AudioSystem: [Bank] unsupported channel count.");
                return null;
            }
            var clip = new MonoPcmClip { Samples = mono, SampleRate = sampleRate };
            _soundBank[resolvedPath] = clip;
            Console.WriteLine($"AudioSystem: [Bank] loaded mono '{Path.GetFileName(resolvedPath)}' ({mono.Length} samples @ {sampleRate} Hz)");
            return clip;
        }
        private bool TryParseWav(byte[] audioData, out short channels, out int sampleRate, out short bitsPerSample,
                                 out int dataSize, out long dataStart, out short[] samples)
        {
            channels = 0; sampleRate = 0; bitsPerSample = 0; dataSize = 0; dataStart = 0; samples = null;
            try
            {
                using var ms = new MemoryStream(audioData);
                using var reader = new BinaryReader(ms);
                if (new string(reader.ReadChars(4)) != "RIFF") return false;
                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") return false;
                while (ms.Position < ms.Length - 8)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();
                    if (chunkId == "fmt ")
                    {
                        short format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();
                        if (format != 1 || bitsPerSample != 16) return false;
                        if (chunkSize > 16) reader.ReadBytes(chunkSize - 16);
                    }
                    else if (chunkId == "data")
                    {
                        dataSize = chunkSize;
                        dataStart = ms.Position;
                        int numSamples = dataSize / 2;
                        samples = new short[numSamples];
                        for (int i = 0; i < numSamples; i++)
                            samples[i] = reader.ReadInt16();
                        break;
                    }
                    else
                    {
                        if (chunkSize < 0 || ms.Position + chunkSize > ms.Length) break;
                        reader.ReadBytes(chunkSize);
                    }
                }
                return channels > 0 && sampleRate > 0 && samples != null;
            }
            catch { return false; }
        }
        public void PlayFolder(string folderRelativeOrAbsolute, bool shuffle = false, bool loopPlaylist = true)
        {
            string folder = folderRelativeOrAbsolute;
            if (!Path.IsPathRooted(folder))
                folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder.TrimStart('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder))
            {
                Console.WriteLine($"AudioSystem: Music folder not found: {folder}");
                return;
            }
            var files = Directory.GetFiles(folder, "*.wav", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0)
            {
                Console.WriteLine($"AudioSystem: No .wav/.mp3 files in {folder}");
                return;
            }
            _playlist.Clear();
            _playlist.AddRange(files);
            if (shuffle)
            {
                for (int i = _playlist.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    var tmp = _playlist[i];
                    _playlist[i] = _playlist[j];
                    _playlist[j] = tmp;
                }
            }
            _playlistIndex = 0;
            PlayCurrentPlaylistTrack(loopPlaylist);
        }
        public void Next(bool loopPlaylist = true)
        {
            if (_playlist.Count == 0) return;
            _playlistIndex = (_playlistIndex + 1) % _playlist.Count;
            PlayCurrentPlaylistTrack(loopPlaylist);
        }
        public void Previous(bool loopPlaylist = true)
        {
            if (_playlist.Count == 0) return;
            _playlistIndex = (_playlistIndex - 1 + _playlist.Count) % _playlist.Count;
            PlayCurrentPlaylistTrack(loopPlaylist);
        }
        private void PlayCurrentPlaylistTrack(bool loop)
        {
            if (_playlistIndex < 0 || _playlistIndex >= _playlist.Count) return;
            if (_currentPlaylistHandle >= 0) Stop(_currentPlaylistHandle);
            string path = _playlist[_playlistIndex];
            _currentPlaylistHandle = Play(path, 1f, loop, true);
            _currentTitle = Path.GetFileNameWithoutExtension(path);
            Console.WriteLine($"AudioSystem: Playlist now playing '{_currentTitle}' ({_playlistIndex + 1}/{_playlist.Count})");
        }
        public void Stop(int handle)
        {
            if (_spatialPlayers.TryGetValue(handle, out var wave))
            {
                wave.Stop();
                wave.Dispose();
                _spatialPlayers.Remove(handle);
                return;
            }
            if (_activePlayers.TryGetValue(handle, out var inst))
            {
                try { inst.Player?.Stop(); inst.Player?.Dispose(); } catch { }
                _activePlayers.Remove(handle);
                if (handle == _currentPlaylistHandle) _currentPlaylistHandle = -1;
            }
        }
        public void StopAll(bool musicOnly = false)
        {
            if (!musicOnly)
            {
                foreach (var kv in _spatialPlayers.ToList())
                {
                    kv.Value.Stop();
                    kv.Value.Dispose();
                }
                _spatialPlayers.Clear();
            }
            var keys = new List<int>(_activePlayers.Keys);
            foreach (var h in keys)
            {
                if (musicOnly && !_activePlayers[h].IsMusic) continue;
                Stop(h);
            }
            if (musicOnly) _currentPlaylistHandle = -1;
        }
        public void StopNonMusic()
        {
            foreach (var kv in _spatialPlayers.ToList())
            {
                kv.Value.Stop();
                kv.Value.Dispose();
            }
            _spatialPlayers.Clear();
            var keys = new List<int>(_activePlayers.Keys);
            foreach (var h in keys)
            {
                if (_activePlayers.TryGetValue(h, out var inst) && !inst.IsMusic)
                    Stop(h);
            }
        }
        public void Pause(int handle)
        {
            if (_activePlayers.TryGetValue(handle, out var inst) && !inst.IsPaused)
            {
                try { inst.Player?.Stop(); inst.IsPaused = true; } catch { }
            }
        }
        public void PauseCurrent()
        {
            if (_currentPlaylistHandle >= 0) Pause(_currentPlaylistHandle);
        }
        public void Resume(int handle)
        {
            if (_activePlayers.TryGetValue(handle, out var inst) && inst.IsPaused)
            {
                try
                {
                    if (inst.Loop) inst.Player.PlayLooping();
                    else inst.Player.Play();
                    inst.IsPaused = false;
                }
                catch { }
            }
        }
        public void ResumeCurrent()
        {
            if (_currentPlaylistHandle >= 0) Resume(_currentPlaylistHandle);
        }
        public void SetVolume(int handle, float volume)
        {
            if (_activePlayers.TryGetValue(handle, out var inst))
                inst.Volume = Math.Clamp(volume, 0f, 1f);
        }
        private string ResolveSoundPath(string clipNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(clipNameOrPath)) return null;
            if (Path.IsPathRooted(clipNameOrPath) && File.Exists(clipNameOrPath))
                return clipNameOrPath;
            string cleaned = clipNameOrPath.TrimStart('\\', '/').Replace('/', Path.DirectorySeparatorChar);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = Path.GetFileName(cleaned);
            string[] candidates =
            {
                Path.Combine(baseDir, cleaned),
                Path.Combine(baseDir, "Assets", cleaned),
                Path.Combine(baseDir, "Assets", "Sounds", cleaned),
                Path.Combine(baseDir, "Assets", "Sounds", fileName),
                Path.Combine(baseDir, "Assets", "Sounds", "IDE", "Music", fileName),
                Path.Combine(baseDir, "Sounds", cleaned),
                Path.Combine(baseDir, "Sounds", fileName),
                Path.Combine(baseDir, "..", "Assets", "Sounds", fileName),
                Path.Combine(baseDir, "..", "..", "Assets", "Sounds", fileName),
                Path.Combine(baseDir, "..", "..", "..", "Assets", "Sounds", fileName),
            };
            foreach (var c in candidates)
            {
                try
                {
                    string full = Path.GetFullPath(c);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
            return null;
        }
        private void OnGenericEvent(GenericEvent e)
        {
            if (e?.Hook == "StopSoundPreview")
                StopNonMusic();
        }
        private void OnSoundEmission(SoundEmissionEvent e)
        {
            if (e?.Source == null) return;
            if (_isServer)
            {
                if (e.Source.IsSensitive)
                {
                    if (_validationSystem != null && _validationSystem.ValidateSoundSource(e.Source))
                    {
                        var result = PrimaryLosRayInternal(e.Source.Position, _listenerPosition);
                        if (result != null)
                            _server.Publish(new SoundEvent { Source = e.Source, Result = result }, true);
                    }
                    else
                    {
                        Console.WriteLine("Sound source validation failed.");
                    }
                }
            }
            else
            {
                if (!e.Source.Loop)
                    StopNonMusic();
                var result = PrimaryLosRayInternal(e.Source.Position, _listenerPosition);
                PlaySpatial(e.Source, result);
            }
        }
        private void OnSoundEvent(SoundEvent e)
        {
            if (e?.Source == null) return;
            PlaySpatial(e.Source, e.Result);
        }
        private byte[] ConvertMp3ToWav(byte[] mp3Data) => null;
        public void Dispose()
        {
            // Stop the dedicated worker
            _workerRunning = false;
            if (_audioWorker != null && _audioWorker.IsAlive)
            {
                _audioWorker.Join(500);
                _audioWorker = null;
            }
            _acousticRayTracer?.Dispose();
            _acousticGeometry?.Dispose();
            _acousticRayTracer = null;
            _acousticGeometry = null;
            _gpuOcclusionReady = false;
            _geometryUploaded = false;
            foreach (var kv in _spatialPlayers)
            {
                kv.Value.Stop();
                kv.Value.Dispose();
            }
            _spatialPlayers.Clear();
            foreach (var kv in _activePlayers)
            {
                try { kv.Value.Player?.Stop(); kv.Value.Player?.Dispose(); } catch { }
            }
            _activePlayers.Clear();
        }
    }
}