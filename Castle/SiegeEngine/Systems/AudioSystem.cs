using System;
using System.IO;
using System.Media;
using System.Numerics;
using System.Threading.Tasks;
using SiegeEngine.Definitions;
using SiegeEngine.Events;
using SiegeEngine.Interfaces;

namespace SiegeEngine.Systems
{
    public partial class AudioSystem : GameSystem
    {
        private readonly EventBus _eventBus;
        private readonly bool _isServer;
        private readonly ISoundValidator _validationSystem;
        private readonly Random _random = new Random();
        private const float MaxDistance = 2000f; // 20 units in cm
        private const float MinIntensity = 0.00001f; // -80 dB
        private const float ListenerRadius = 10f; // cm
        private Vector3 _listenerPosition;

        public AudioSystem(IGameServer server, EventBus eventBus, bool isServer, ISoundValidator validationSystem = null) : base(server)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _isServer = isServer;
            _validationSystem = validationSystem;
            _eventBus.Subscribe<SoundEmissionEvent>(OnSoundEmission);
            if (!isServer)
            {
                _eventBus.Subscribe<SoundEvent>(OnSoundEvent);
            }
        }

        public void SetListenerPosition(Vector3 position)
        {
            _listenerPosition = position;
        }

        public override void Update(float deltaTime)
        {
        }

        private void OnSoundEmission(SoundEmissionEvent e)
        {
            if (_isServer)
            {
                if (e.Source.IsSensitive)
                {
                    if (_validationSystem != null && _validationSystem.ValidateSoundSource(e.Source))
                    {
                        var result = RayTraceSound(e.Source);
                        if (result != null)
                        {
                            var soundEvent = new SoundEvent { Source = e.Source, Result = result };
                            _server.Publish(soundEvent, true);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Sound source validation failed.");
                    }
                }
            }
            else
            {
                if (!e.Source.IsSensitive)
                {
                    var result = RayTraceSound(e.Source);
                    if (result != null)
                    {
                        PlaySound(e.Source, result);
                    }
                }
            }
        }

        private void OnSoundEvent(SoundEvent e)
        {
            PlaySound(e.Source, e.Result);
        }

        private SoundRayTraceResult RayTraceSound(SoundSource source)
        {
            int numRays = _isServer ? 500 : 300;
            int maxBounces = _isServer ? 8 : 6;
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
                    var result = _isServer ? _server.RequestRayTrace(position, direction, MaxDistance) : SimulateClientRayTrace(position, direction);
                    distance += result.Distance;

                    if (!result.DidHit || distance > MaxDistance)
                        break;

                    intensity *= 1.0f / (distance * distance);
                    if (result.Material != null)
                    {
                        float density = result.Material.Density;
                        intensity *= (float)Math.Pow(10, -2 * density * result.Distance / 10);
                        intensity *= density > 1.5f ? 0.9f : 0.7f;
                    }

                    if (Vector3.Distance(result.HitPoint, _listenerPosition) < ListenerRadius)
                    {
                        totalIntensity += intensity;
                        totalDelay += distance / 34300f;
                        validRays++;
                        break;
                    }

                    direction = Vector3.Reflect(direction, result.HitNormal);
                    position = result.HitPoint + direction * 0.01f;
                    bounceCount++;

                    if (bounceCount < maxBounces)
                    {
                        for (int j = 0; j < 6; j++)
                        {
                            Vector3 scatterDir = RandomScatterDirection(direction, 10f);
                            var scatterResult = _isServer ? _server.RequestRayTrace(position, scatterDir, MaxDistance) : SimulateClientRayTrace(position, scatterDir);
                            if (scatterResult.DidHit && Vector3.Distance(scatterResult.HitPoint, _listenerPosition) < ListenerRadius)
                            {
                                float scatterDistance = distance + scatterResult.Distance;
                                float scatterIntensity = intensity * 0.1f / (scatterDistance * scatterDistance);
                                if (scatterResult.Material != null)
                                {
                                    scatterIntensity *= (float)Math.Pow(10, -2 * scatterResult.Material.Density * scatterResult.Distance / 10);
                                }
                                totalIntensity += scatterIntensity;
                                totalDelay += scatterDistance / 34300f;
                                validRays++;
                            }
                        }
                    }
                }
            }

            if (validRays == 0)
                return null;

            return new SoundRayTraceResult
            {
                Intensity = totalIntensity / validRays,
                Delay = totalDelay / validRays,
                LowPassCutoff = 8000f / (totalIntensity > 0 ? Math.Max(1f, totalIntensity) : 1f)
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

        private Vector3 RandomScatterDirection(Vector3 direction, float coneAngleDeg)
        {
            float coneAngleRad = coneAngleDeg * (float)(Math.PI / 180);
            float theta = (float)(_random.NextDouble() * 2 * Math.PI);
            float phi = (float)(_random.NextDouble() * coneAngleRad);
            Vector3 randomDir = new Vector3(
                (float)(Math.Sin(phi) * Math.Cos(theta)),
                (float)(Math.Sin(phi) * Math.Sin(theta)),
                (float)Math.Cos(phi)
            );
            Vector3 basisZ = direction;
            Vector3 basisX = Vector3.Normalize(Vector3.Cross(basisZ, Math.Abs(basisZ.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 basisY = Vector3.Cross(basisZ, basisX);
            return basisX * randomDir.X + basisY * randomDir.Y + basisZ * randomDir.Z;
        }

        private RayTraceResult SimulateClientRayTrace(Vector3 start, Vector3 direction)
        {
            return new RayTraceResult { DidHit = false, Distance = MaxDistance };
        }

        private void PlaySound(SoundSource source, SoundRayTraceResult result)
        {
            string wavPath = $"Assets/Sounds/{source.Type.ToLower()}.wav";
            string mp3Path = $"Assets/Sounds/{source.Type.ToLower()}.mp3";
            string soundPath = File.Exists(wavPath) ? wavPath : File.Exists(mp3Path) ? mp3Path : null;

            if (soundPath == null)
            {
                Console.WriteLine($"AudioSystem: Sound file for {source.Type} not found.");
                return;
            }

            byte[] audioData = File.ReadAllBytes(soundPath);
            bool isMp3 = soundPath.EndsWith(".mp3");

            if (isMp3)
            {
                audioData = ConvertMp3ToWav(audioData);
                if (audioData == null)
                {
                    Console.WriteLine($"AudioSystem: Failed to convert MP3 to WAV for {soundPath}.");
                    return;
                }
            }

            using MemoryStream ms = new MemoryStream(audioData);
            using BinaryReader reader = new BinaryReader(ms);

            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF")
            {
                Console.WriteLine("Invalid WAV file: Not RIFF");
                return;
            }
            int fileSize = reader.ReadInt32();
            string wave = new string(reader.ReadChars(4));
            if (wave != "WAVE")
            {
                Console.WriteLine("Invalid WAV file: Not WAVE");
                return;
            }

            short channels = 0;
            int sampleRate = 0;
            short bitsPerSample = 0;
            int dataSize = 0;
            long dataStart = 0;

            while (ms.Position < ms.Length)
            {
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();
                if (chunkId == "fmt ")
                {
                    short format = reader.ReadInt16();
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    int byteRate = reader.ReadInt32();
                    short blockAlign = reader.ReadInt16();
                    bitsPerSample = reader.ReadInt16();
                    if (format != 1)
                    {
                        Console.WriteLine("Only PCM WAV files are supported");
                        return;
                    }
                    if (bitsPerSample != 16)
                    {
                        Console.WriteLine("Only 16-bit WAV files are supported");
                        return;
                    }
                    if (chunkSize > 16)
                        reader.ReadBytes(chunkSize - 16);
                }
                else if (chunkId == "data")
                {
                    dataSize = chunkSize;
                    dataStart = ms.Position;
                    reader.ReadBytes(chunkSize);
                }
                else
                {
                    reader.ReadBytes(chunkSize);
                }
            }

            if (channels == 0 || sampleRate == 0 || dataSize == 0)
            {
                Console.WriteLine("Invalid WAV file: Missing format or data");
                return;
            }

            ms.Position = dataStart;
            int numSamples = dataSize / 2;
            short[] samples = new short[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                samples[i] = reader.ReadInt16();
            }

            float cutoff = result.LowPassCutoff;
            float alpha = (float)Math.Exp(-2 * Math.PI * cutoff / sampleRate);

            short[] filteredSamples;
            if (channels == 1)
            {
                filteredSamples = ApplyLowPassFilter(samples, alpha);
            }
            else if (channels == 2)
            {
                short[] left = new short[numSamples / 2];
                short[] right = new short[numSamples / 2];
                for (int i = 0; i < numSamples / 2; i++)
                {
                    left[i] = samples[2 * i];
                    right[i] = samples[2 * i + 1];
                }
                short[] filteredLeft = ApplyLowPassFilter(left, alpha);
                short[] filteredRight = ApplyLowPassFilter(right, alpha);
                filteredSamples = new short[numSamples];
                for (int i = 0; i < numSamples / 2; i++)
                {
                    filteredSamples[2 * i] = filteredLeft[i];
                    filteredSamples[2 * i + 1] = filteredRight[i];
                }
            }
            else
            {
                Console.WriteLine("Unsupported number of channels");
                return;
            }

            float volume = Math.Clamp(result.Intensity, 0f, 1f);
            for (int i = 0; i < filteredSamples.Length; i++)
            {
                int scaled = (int)(filteredSamples[i] * volume);
                filteredSamples[i] = (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
            }

            byte[] modifiedWav = (byte[])audioData.Clone();
            using (MemoryStream msMod = new MemoryStream(modifiedWav))
            {
                msMod.Position = dataStart;
                using BinaryWriter writer = new BinaryWriter(msMod);
                for (int i = 0; i < filteredSamples.Length; i++)
                {
                    writer.Write(filteredSamples[i]);
                }
            }

            Task.Run(async () =>
            {
                await Task.Delay((int)(result.Delay * 1000));
                using MemoryStream playStream = new MemoryStream(modifiedWav);
                SoundPlayer player = new SoundPlayer(playStream);
                player.Play();
            });
        }

        private byte[] ConvertMp3ToWav(byte[] mp3Data)
        {
            try
            {
                using MemoryStream mp3Stream = new MemoryStream(mp3Data);
                using MemoryStream wavStream = new MemoryStream();

                mp3Stream.Position = 0;
                byte[] header = new byte[4];
                mp3Stream.Read(header, 0, 4);
                if (header[0] != 0xFF || (header[1] & 0xE0) != 0xE0)
                {
                    Console.WriteLine("Invalid MP3 header");
                    return null;
                }

                int sampleRate = GetMp3SampleRate(header);
                int channels = (header[3] & 0xC0) == 0xC0 ? 1 : 2;
                if (sampleRate == 0)
                {
                    Console.WriteLine("Unsupported MP3 format");
                    return null;
                }

                short[] pcmSamples = DecodeMp3Frames(mp3Stream, channels, sampleRate);
                if (pcmSamples == null || pcmSamples.Length == 0)
                {
                    Console.WriteLine("Failed to decode MP3 frames");
                    return null;
                }

                using BinaryWriter writer = new BinaryWriter(wavStream);
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                int dataLength = pcmSamples.Length * 2;
                writer.Write(36 + dataLength);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(dataLength);

                for (int i = 0; i < pcmSamples.Length; i++)
                {
                    writer.Write(pcmSamples[i]);
                }

                return wavStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting MP3 to WAV: {ex.Message}");
                return null;
            }
        }

        private int GetMp3SampleRate(byte[] header)
        {
            int version = (header[1] & 0x18) >> 3;
            int frequencyIndex = (header[2] & 0x0C) >> 2;
            int[][] sampleRates = new int[][]
            {
                new int[] { 44100, 48000, 32000, 0 }, // MPEG-1
                new int[] { 22050, 24000, 16000, 0 }, // MPEG-2
                new int[] { 11025, 12000, 8000, 0 }   // MPEG-2.5
            };
            int mpegVersion = version == 3 ? 0 : version == 2 ? 1 : 2;
            return sampleRates[mpegVersion][frequencyIndex];
        }

        private short[] DecodeMp3Frames(Stream mp3Stream, int channels, int sampleRate)
        {
            const int samplesPerFrame = 1152;
            int samplesCapacity = 100000;
            short[] pcmSamples = new short[samplesCapacity];
            int sampleCount = 0;
            long initialPosition = mp3Stream.Position;

            mp3Stream.Position = initialPosition;
            byte[] buffer = new byte[4];

            while (mp3Stream.Position < mp3Stream.Length - 4)
            {
                mp3Stream.Read(buffer, 0, 4);
                if (buffer[0] != 0xFF || (buffer[1] & 0xE0) != 0xE0)
                {
                    mp3Stream.Position -= 3;
                    continue;
                }

                int frameSize = CalculateFrameSize(buffer, sampleRate);
                if (frameSize < 4 || mp3Stream.Position + frameSize - 4 > mp3Stream.Length)
                    break;

                byte[] frameData = new byte[frameSize - 4];
                mp3Stream.Read(frameData, 0, frameSize - 4);

                short[] frameSamples = DecodeMp3Frame(frameData, channels);
                if (frameSamples != null)
                {
                    if (sampleCount + frameSamples.Length > pcmSamples.Length)
                    {
                        Array.Resize(ref pcmSamples, pcmSamples.Length * 2);
                    }
                    Array.Copy(frameSamples, 0, pcmSamples, sampleCount, frameSamples.Length);
                    sampleCount += frameSamples.Length;
                }
            }

            if (sampleCount == 0)
                return null;

            Array.Resize(ref pcmSamples, sampleCount);
            return pcmSamples;
        }

        private int CalculateFrameSize(byte[] header, int sampleRate)
        {
            int bitrateIndex = (header[2] & 0xF0) >> 4;
            int version = (header[1] & 0x18) >> 3;
            int[][] bitrateTable = new int[][]
            {
                new int[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 }, // MPEG-1
                new int[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 }   // MPEG-2, 2.5
            };
            int mpegVersion = version == 3 ? 0 : 1;
            int bitrate = bitrateTable[mpegVersion][bitrateIndex] * 1000;
            if (bitrate == 0)
                return 0;

            int padding = (header[2] & 0x02) >> 1;
            return 144 * bitrate / sampleRate + padding;
        }

        private short[] DecodeMp3Frame(byte[] frameData, int channels)
        {
            const int samplesPerFrame = 1152;
            short[] samples = new short[samplesPerFrame * channels];

            using MemoryStream frameStream = new MemoryStream(frameData);
            using BinaryReader reader = new BinaryReader(frameStream);

            if (frameStream.Length < 17)
                return null;

            byte[] sideInfo = new byte[channels == 1 ? 17 : 32];
            frameStream.Read(sideInfo, 0, sideInfo.Length);

            int mainDataLength = (frameData.Length - sideInfo.Length) * 8;
            if (mainDataLength <= 0)
                return null;

            byte[] mainData = new byte[frameData.Length - sideInfo.Length];
            frameStream.Read(mainData, 0, mainData.Length);

            int[] scalefactors = new int[channels * 2];
            int scalefactorIndex = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                for (int gr = 0; gr < 2; gr++)
                {
                    scalefactors[scalefactorIndex++] = reader.ReadByte() & 0xFF;
                }
            }

            float[][] granules = new float[channels][];
            for (int ch = 0; ch < channels; ch++)
            {
                granules[ch] = new float[samplesPerFrame];
                for (int i = 0; i < samplesPerFrame; i++)
                {
                    int bitPos = i % 8;
                    int bytePos = i / 8;
                    if (bytePos < mainData.Length)
                    {
                        int bitValue = mainData[bytePos] >> 7 - bitPos & 1;
                        granules[ch][i] = bitValue * scalefactors[ch * 2 + i / 576];
                    }
                }
            }

            float[] mdctOutput = new float[36];
            float[] polyphaseOutput = new float[samplesPerFrame];
            for (int i = 0; i < samplesPerFrame; i += 36)
            {
                for (int j = 0; j < 36; j++)
                {
                    mdctOutput[j] = granules[0][i + j];
                }

                for (int j = 0; j < 36; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < 36; k++)
                    {
                        sum += mdctOutput[k] * (float)Math.Cos(Math.PI / 36 * (j + 0.5) * (k + 0.5));
                    }
                    polyphaseOutput[i + j] = sum;
                }
            }

            for (int ch = 0; ch < channels; ch++)
            {
                for (int i = 0; i < samplesPerFrame; i++)
                {
                    float sample = ch == 0 ? polyphaseOutput[i] : granules[1][i];
                    samples[i * channels + ch] = (short)Math.Clamp(sample * 32767, short.MinValue, short.MaxValue);
                }
            }

            return samples;
        }

        private short[] ApplyLowPassFilter(short[] samples, float alpha)
        {
            short[] filtered = new short[samples.Length];
            filtered[0] = samples[0];
            for (int i = 1; i < samples.Length; i++)
            {
                filtered[i] = (short)(alpha * filtered[i - 1] + (1 - alpha) * samples[i]);
            }
            return filtered;
        }
    }
}