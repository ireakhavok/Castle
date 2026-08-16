// Folder: SiegeEngine/Systems
// File: AudioSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Numerics;
using SiegeEngine.Audio;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Interfaces;
using SiegeEngine.PlayerSystem;

namespace SiegeEngine.Systems
{
    public partial class AudioSystem : GameSystem
    {
        private readonly EventBus _eventBus;
        private readonly bool _isServer;
        private readonly ISoundValidator _validationSystem;
        private readonly Random _random = new Random();
        private const float MaxDistance = 2000f;
        private const float MinIntensity = 0.00001f;
        private const float ListenerRadius = 10f;
        private const float SpatialMaxDistance = 200f;
        private const float SpatialRefDistance = 1.5f;
        private const float MaxITDSeconds = 0.00065f;
        private const float OcclusionUpdateInterval = 0.12f; // ~8 Hz continuous occlusion queries

        private Vector3 _listenerPosition;
        private Vector3 _listenerForward = new Vector3(0, 1, 0);
        private bool _listenerValid;
        private int _nextHandle = 1;
        private readonly Dictionary<int, PlaybackInstance> _activePlayers = new Dictionary<int, PlaybackInstance>();
        private readonly Dictionary<int, WaveOutPlayer> _spatialPlayers = new Dictionary<int, WaveOutPlayer>();
        private readonly List<AutoPlayRegistration> _autoPlayRegs = new List<AutoPlayRegistration>();
        private bool _autoPlayScanned;
        private readonly List<string> _playlist = new List<string>();
        private int _playlistIndex = -1;
        private int _currentPlaylistHandle = -1;
        private string _currentTitle = "";
        private float _occlusionTimer;

        // Resident mono PCM sound bank – loaded once, kept for lifetime of the system
        private readonly Dictionary<string, MonoPcmClip> _soundBank = new Dictionary<string, MonoPcmClip>(StringComparer.OrdinalIgnoreCase);

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
        }

        public AudioSystem(IGameServer server, EventBus eventBus, bool isServer, ISoundValidator validationSystem = null) : base(server)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _isServer = isServer;
            _validationSystem = validationSystem;
            _eventBus.Subscribe<SoundEmissionEvent>(OnSoundEmission);
            if (!isServer)
                _eventBus.Subscribe<SoundEvent>(OnSoundEvent);
            _eventBus.Subscribe<GenericEvent>(OnGenericEvent);
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
                if (_listenerValid)
                {
                    _occlusionTimer += deltaTime;
                    bool doOcclusion = _occlusionTimer >= OcclusionUpdateInterval;
                    if (doOcclusion)
                        _occlusionTimer = 0f;
                    TickAutoPlayRegistrations(doOcclusion);
                }
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

                // Pre-load into bank so the first Start is instantaneous
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

        private void TickAutoPlayRegistrations(bool updateOcclusion)
        {
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
                    // First occlusion query so the sound starts with correct wall filtering
                    if (updateOcclusion || reg.LastRayResult == null)
                        reg.LastRayResult = RayTraceSound(reg.Source);

                    int h = PlaySpatial(reg.Source, reg.LastRayResult);
                    if (h >= 0)
                    {
                        reg.Handle = h;
                        reg.Started = true;
                    }
                    continue;
                }

                // Continuous occlusion against real physics geometry
                if (updateOcclusion)
                    reg.LastRayResult = RayTraceSound(reg.Source);

                if (reg.Handle >= 0 && _spatialPlayers.TryGetValue(reg.Handle, out var player) && player.IsPlaying)
                {
                    ApplySpatialToPlayer(player, reg.Source, reg.LastRayResult);
                }
            }
        }

        private void ApplySpatialToPlayer(WaveOutPlayer player, SoundSource source, SoundRayTraceResult rayResult)
        {
            Vector3 toSource = source.Position - _listenerPosition;
            float distance = toSource.Length();

            // Base geometric attenuation (inverse-square)
            float geometric;
            if (!_listenerValid)
            {
                geometric = 1f;
            }
            else if (distance <= SpatialRefDistance)
            {
                geometric = 1f;
            }
            else
            {
                float ratio = SpatialRefDistance / distance;
                geometric = Math.Max(0.02f, ratio * ratio);
            }

            // Occlusion intensity from multi-bounce ray-trace (walls / geometry)
            float occlusion = 1f;
            float lowPass = 0f;
            if (rayResult != null)
            {
                occlusion = Math.Clamp(rayResult.Intensity, 0.02f, 1f);
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
                // Negate so that +X (right of listener when facing +Y) produces positive pan → right ear
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
                Console.WriteLine($"AudioSystem: [Spatial] file not found for '{pathHint}'. " +
                                  "Place the clip under Assets/Sounds/ (project or engine) or use an absolute path.");
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

                // Apply initial spatialisation immediately (includes occlusion if provided)
                ApplySpatialToPlayer(wave, source, rayResult);

                int handle = _nextHandle++;
                _spatialPlayers[handle] = wave;

                Vector3 toSource = source.Position - _listenerPosition;
                float distance = toSource.Length();
                Console.WriteLine($"AudioSystem: [Spatial] started '{Path.GetFileName(resolved)}' " +
                                  $"pos={source.Position} listener={_listenerPosition} " +
                                  $"dist={distance:F1} loop={source.Loop}");
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
                        var result = RayTraceSound(e.Source);
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
                // One-shot also gets a fresh occlusion query
                var result = RayTraceSound(e.Source);
                PlaySpatial(e.Source, result);
            }
        }

        private void OnSoundEvent(SoundEvent e)
        {
            if (e?.Source == null) return;

            // For continuous AutoPlay sources, store the ray result so Tick keeps applying it
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

        /// <summary>
        /// Multi-bounce acoustic ray-trace against the real physics geometry
        /// (same path used by the authoritative server). Returns Intensity + LowPass
        /// that correctly muffles sound behind walls.
        /// </summary>
        private SoundRayTraceResult RayTraceSound(SoundSource source)
        {
            if (_server == null || !_listenerValid)
                return null;

            int numRays = _isServer ? 500 : 120;          // lighter on client for continuous use
            int maxBounces = _isServer ? 8 : 5;
            float totalIntensity = 0f;
            float totalDelay = 0f;
            int validRays = 0;

            for (int i = 0; i < numRays; i++)
            {
                Vector3 direction = RandomDirection();
                float intensity = 1.0f;
                float distance = 0f;
                int bounceCount = 0;
                Vector3 position = source.Position;

                while (bounceCount < maxBounces && intensity > MinIntensity)
                {
                    var result = _server.RequestRayTrace(position, direction, MaxDistance);
                    if (!result.DidHit || result.Distance <= 0.001f)
                    {
                        // Open path – still accumulate a small residual if we are close to the listener
                        float remaining = Vector3.Distance(position, _listenerPosition);
                        if (remaining < ListenerRadius * 2f)
                        {
                            totalIntensity += intensity * 0.15f;
                            totalDelay += (distance + remaining) / 34300f;
                            validRays++;
                        }
                        break;
                    }

                    distance += result.Distance;
                    if (distance > MaxDistance) break;

                    // Geometric spreading
                    intensity *= 1.0f / Math.Max(1f, distance * distance * 0.0001f + 1f);

                    // Material absorption
                    if (result.Material != null)
                    {
                        float dens = Math.Max(0.1f, result.Material.Density);
                        intensity *= (float)Math.Pow(10, -1.5 * dens * result.Distance / 10);
                        intensity *= dens > 1.5f ? 0.85f : 0.65f;
                    }

                    // Did this bounce reach the listener?
                    if (Vector3.Distance(result.HitPoint, _listenerPosition) < ListenerRadius)
                    {
                        totalIntensity += intensity;
                        totalDelay += distance / 34300f;
                        validRays++;
                        break;
                    }

                    // Reflect and continue
                    direction = Vector3.Reflect(direction, result.HitNormal);
                    position = result.HitPoint + direction * 0.02f;
                    bounceCount++;
                }
            }

            if (validRays == 0)
            {
                // Completely occluded or no path found – heavy muffling
                return new SoundRayTraceResult
                {
                    Intensity = 0.04f,
                    Delay = 0.05f,
                    LowPassCutoff = 800f
                };
            }

            float avgIntensity = totalIntensity / validRays;
            return new SoundRayTraceResult
            {
                Intensity = Math.Clamp(avgIntensity, 0.02f, 1f),
                Delay = totalDelay / validRays,
                LowPassCutoff = 18000f / (1f + avgIntensity * 4f)   // more occlusion → lower cutoff
            };
        }

        private Vector3 RandomDirection()
        {
            float theta = (float)(_random.NextDouble() * 2 * Math.PI);
            float phi = (float)Math.Acos(2 * _random.NextDouble() - 1);
            return new Vector3(
                (float)(Math.Sin(phi) * Math.Cos(theta)),
                (float)(Math.Sin(phi) * Math.Sin(theta)),
                (float)Math.Cos(phi)
            );
        }

        private byte[] ConvertMp3ToWav(byte[] mp3Data)
        {
            return null;
        }
    }
}