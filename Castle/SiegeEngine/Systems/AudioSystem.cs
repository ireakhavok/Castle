// Folder: SiegeEngine/Systems
// File: AudioSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SiegeEngine.Audio;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.Core.Networking;
using SiegeEngine.Core.Physics;
using SiegeEngine.Core.Rendering.Compute;
using SiegeEngine.Core.Rendering.ContextManagement;
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

        // Modest throttle for expensive ray work (keeps FPS high)
        private const int OcclusionUpdateIntervalFrames = 4;

        // Strong continuous smoothing of the applied result
        private const float IntensitySmoothRate = 12.0f;
        private const float DirectionSmoothRate = 14.0f;

        private Vector3 _listenerPosition;
        private Vector3 _listenerForward = new Vector3(0, 1, 0);
        private bool _listenerValid;
        private int _nextHandle = 1;
        private int _frameCounter;
        private readonly Dictionary<int, PlaybackInstance> _activePlayers = new Dictionary<int, PlaybackInstance>();
        private readonly Dictionary<int, WaveOutPlayer> _spatialPlayers = new Dictionary<int, WaveOutPlayer>();
        private readonly List<AutoPlayRegistration> _autoPlayRegs = new List<AutoPlayRegistration>();
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

        private readonly object _probeLock = new object();
        private volatile bool _probeBusy;

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
            public SoundRayTraceResult LastRayResult;

            public volatile SoundRayTraceResult ProbeResult;
            public volatile bool HasProbeResult;

            public float SmoothedIntensity = 1f;
            public Vector3 SmoothedDirection = Vector3.Zero;
            public float SmoothedLowPass = 12000f;
            public bool HasSmoothedState;
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
                Console.WriteLine("AudioSystem: GPU occlusion infrastructure ready (currently disabled for performance).");
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
        public override void Update(float deltaTime)
        {
            _frameCounter++;
            if (!_isServer && _server != null)
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
            if (!_isServer)
            {
                if (!_autoPlayScanned)
                {
                    ScanAndRegisterAutoPlay();
                    _autoPlayScanned = true;
                }
                if (_gpuOcclusionReady && !_geometryUploaded && _server != null && _server.GetEntities().Count > 0)
                    RebuildAcousticGeometry();
                if (_gpuOcclusionReady && _geometryUploaded && _heightProvider == null &&
                    _acousticGeometry != null && _acousticGeometry.TriangleCount <= 60 &&
                    _server is ClientGameServerProxy p && p.PhysicsWorld?.HeightProvider != null)
                {
                    _heightProvider = p.PhysicsWorld.HeightProvider;
                    RebuildAcousticGeometry();
                }
                if (_listenerValid)
                    TickAutoPlayRegistrations(deltaTime);
            }
        }
        private void ScanAndRegisterAutoPlay()
        {
            if (_server == null) return;
            var entities = _server.GetEntities();
            if (entities == null) return;
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
        private void TickAutoPlayRegistrations(float deltaTime)
        {
            bool doOcclusionUpdate = (_frameCounter % OcclusionUpdateIntervalFrames) == 0;

            for (int i = 0; i < _autoPlayRegs.Count; i++)
            {
                var reg = _autoPlayRegs[i];
                var entity = _server.GetEntityById(reg.EntityId);
                if (entity != null)
                {
                    var phys = entity.GetComponent<PhysicsComponent>();
                    if (phys != null)
                        reg.Source.Position = phys.Position;
                }
                if (!reg.Started)
                {
                    if (!_listenerValid) continue;
                    reg.LastRayResult = PrimaryLosRay(reg.Source);
                    int h = PlaySpatial(reg.Source, reg.LastRayResult);
                    if (h >= 0)
                    {
                        reg.Handle = h;
                        reg.Started = true;
                        reg.SmoothedIntensity = reg.LastRayResult?.Intensity ?? 1f;
                        reg.SmoothedDirection = reg.LastRayResult?.ApparentDirection.LengthSquared() > 0.0001f
                            ? reg.LastRayResult.ApparentDirection
                            : (reg.Source.Position - _listenerPosition);
                        reg.SmoothedLowPass = reg.LastRayResult?.LowPassCutoff > 0f
                            ? reg.LastRayResult.LowPassCutoff
                            : 12000f;
                        reg.HasSmoothedState = true;
                    }
                    continue;
                }
                if (reg.Handle >= 0 && _spatialPlayers.TryGetValue(reg.Handle, out var player) && player.IsPlaying)
                {
                    if (doOcclusionUpdate)
                    {
                        // Cheap PrimaryLos only (GPU path disabled)
                        SoundRayTraceResult losResult = PrimaryLosRay(reg.Source);
                        SoundRayTraceResult target = losResult;

                        // Prefer background probe when it has higher intensity
                        if (reg.HasProbeResult && reg.ProbeResult != null &&
                            reg.ProbeResult.Intensity > target.Intensity)
                        {
                            target = reg.ProbeResult;
                        }

                        reg.LastRayResult = target;

                        // Schedule background diffraction probe when blocked
                        if (losResult.Intensity < 0.95f && !_probeBusy)
                        {
                            Vector3 srcPos = reg.Source.Position;
                            Vector3 lisPos = _listenerPosition;
                            AutoPlayRegistration targetReg = reg;
                            _probeBusy = true;
                            Task.Run(() =>
                            {
                                try
                                {
                                    SoundRayTraceResult probe = DiffractionProbe(srcPos, lisPos, losResult);
                                    targetReg.ProbeResult = probe;
                                    targetReg.HasProbeResult = true;
                                }
                                finally
                                {
                                    _probeBusy = false;
                                }
                            });
                        }
                    }

                    // Continuous strong smoothing every frame toward the current target
                    SoundRayTraceResult targetResult = reg.LastRayResult ?? new SoundRayTraceResult { Intensity = 1f };
                    float targetIntensity = Math.Clamp(targetResult.Intensity, 0.001f, 1f);
                    Vector3 targetDir = targetResult.ApparentDirection.LengthSquared() > 0.0001f
                        ? Vector3.Normalize(targetResult.ApparentDirection)
                        : Vector3.Normalize(reg.Source.Position - _listenerPosition);

                    float targetLowPass;
                    if (targetResult.ApparentDirection.LengthSquared() < 0.0001f)
                        targetLowPass = 700f + 900f * targetIntensity;
                    else
                        targetLowPass = 2800f + 3200f * targetIntensity;

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

                        float dot = Math.Clamp(Vector3.Dot(reg.SmoothedDirection, targetDir), -1f, 1f);
                        if (dot > 0.9995f)
                        {
                            reg.SmoothedDirection = Vector3.Normalize(
                                reg.SmoothedDirection + (targetDir - reg.SmoothedDirection) * aDir);
                        }
                        else
                        {
                            float theta = MathF.Acos(dot);
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
                        Delay = targetResult.Delay,
                        LowPassCutoff = reg.SmoothedLowPass,
                        ApparentDirection = reg.SmoothedDirection
                    };

                    ApplySpatialToPlayer(player, reg.Source, smoothed);
                }
            }
        }

        private SoundRayTraceResult DiffractionProbe(Vector3 sourcePos, Vector3 listenerPos, SoundRayTraceResult losFallback)
        {
            if (_server == null)
                return losFallback;

            Vector3 toListener = listenerPos - sourcePos;
            float dist = toListener.Length();
            if (dist < 0.5f)
                return losFallback;

            Vector3 dir = toListener / dist;

            Vector3 up = Vector3.UnitZ;
            Vector3 right = Vector3.Cross(dir, up);
            if (right.LengthSquared() < 1e-6f)
                right = Vector3.UnitX;
            else
                right = Vector3.Normalize(right);
            Vector3 realUp = Vector3.Normalize(Vector3.Cross(right, dir));

            float[] lateral = { -50f, -30f, 30f, 50f };
            float[] elev = { 0f, 35f };

            float bestIntensity = 0f;
            Vector3 bestArrival = Vector3.Zero;
            float bestTotalDist = float.MaxValue;

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
                    lock (_probeLock)
                    {
                        hit = _server.RequestRayTrace(sourcePos, probeDir, probeMax);
                    }

                    float clearDist = hit.DidHit ? hit.Distance : probeMax;
                    if (clearDist < dist * 0.35f)
                        continue;

                    float freeT = Math.Min(clearDist * 0.92f, dist * 0.95f);
                    Vector3 freePoint = sourcePos + probeDir * freeT;

                    Vector3 finalVec = listenerPos - freePoint;
                    float finalDist = finalVec.Length();
                    if (finalDist < 0.1f)
                        continue;
                    Vector3 finalDir = finalVec / finalDist;

                    RayTraceResult finalHit;
                    lock (_probeLock)
                    {
                        finalHit = _server.RequestRayTrace(freePoint, finalDir, finalDist + 0.5f);
                    }
                    if (finalHit.DidHit && finalHit.Distance < finalDist - 0.15f)
                        continue;

                    float totalD = freeT + finalDist;
                    float pathFactor = MathF.Sqrt(dist / Math.Max(totalD, 0.5f));
                    float inten = pathFactor * 0.75f;

                    if (inten > bestIntensity)
                    {
                        bestIntensity = inten;
                        bestArrival = finalDir;
                        bestTotalDist = totalD;
                    }
                }
            }

            if (bestIntensity > 0.08f)
            {
                return new SoundRayTraceResult
                {
                    Intensity = Math.Clamp(bestIntensity, 0.08f, 0.95f),
                    Delay = bestTotalDist / 34300f,
                    LowPassCutoff = 3200f + 2800f * bestIntensity,
                    ApparentDirection = bestArrival
                };
            }

            return losFallback;
        }

        private void ApplySpatialToPlayer(WaveOutPlayer player, SoundSource source, SoundRayTraceResult rayResult)
        {
            Vector3 toSource;
            if (rayResult != null && rayResult.ApparentDirection.LengthSquared() > 0.0001f)
                toSource = rayResult.ApparentDirection;
            else
                toSource = source.Position - _listenerPosition;
            float distance = Vector3.Distance(source.Position, _listenerPosition);
            float geometric;
            if (!_listenerValid || distance <= SpatialRefDistance)
            {
                geometric = 1f;
            }
            else if (distance >= SpatialMaxDistance)
            {
                geometric = MinAudibleVolume;
            }
            else
            {
                float gainDb = -20f * MathF.Log10(distance / SpatialRefDistance);
                geometric = MathF.Pow(10f, gainDb / 20f);
                geometric = Math.Max(MinAudibleVolume, geometric);
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
                        var result = PrimaryLosRay(e.Source);
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
                var result = PrimaryLosRay(e.Source);
                PlaySpatial(e.Source, result);
            }
        }
        private void OnSoundEvent(SoundEvent e)
        {
            if (e?.Source == null) return;
            for (int i = 0; i < _autoPlayRegs.Count; i++)
            {
                if (_autoPlayRegs[i].EntityId == e.Source.EntityId)
                {
                    _autoPlayRegs[i].LastRayResult = e.Result;
                    break;
                }
            }
            PlaySpatial(e.Source, e.Result);
        }
        private SoundRayTraceResult PrimaryLosRay(SoundSource source)
        {
            if (_server == null || !_listenerValid)
                return new SoundRayTraceResult { Intensity = 1f, Delay = 0f, LowPassCutoff = 0f, ApparentDirection = Vector3.Zero };
            Vector3 toListener = _listenerPosition - source.Position;
            float dist = toListener.Length();
            if (dist < 0.01f)
                return new SoundRayTraceResult { Intensity = 1f, Delay = 0f, LowPassCutoff = 0f, ApparentDirection = Vector3.Zero };
            Vector3 dir = toListener / dist;
            var result = _server.RequestRayTrace(source.Position, dir, dist + 0.5f);
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
            float intensity = MathF.Exp(-0.8f * dens);
            intensity = Math.Clamp(intensity, 0.001f, 0.95f);
            float lowPass = 600f + 800f / dens;
            return new SoundRayTraceResult
            {
                Intensity = intensity,
                Delay = 0f,
                LowPassCutoff = lowPass,
                ApparentDirection = Vector3.Zero
            };
        }
        private byte[] ConvertMp3ToWav(byte[] mp3Data)
        {
            return null;
        }
        public void Dispose()
        {
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