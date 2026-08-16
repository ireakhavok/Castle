// Folder: SiegeEngine/Audio
// File: WaveOutPlayer.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace SiegeEngine.Audio
{
    /// <summary>
    /// Streaming stereo 16-bit PCM output via winmm.dll (waveOut*).
    /// Keeps decoded mono PCM resident and applies live spatial gains on every buffer fill.
    /// No third-party libraries. World / spatial sources only.
    /// Music playlist continues to use System.Media.SoundPlayer.
    /// </summary>
    public sealed class WaveOutPlayer : IDisposable
    {
        private const int BufferFrames = 4096;
        private const int NumBuffers = 3;
        private const uint WAVE_MAPPER = 0xFFFFFFFF;
        private const uint CALLBACK_NULL = 0x00000000;
        private const uint WHDR_DONE = 0x00000001;
        private const uint WHDR_PREPARED = 0x00000002;
        private IntPtr _hWaveOut = IntPtr.Zero;
        private short[] _mono;
        private int _sampleRate;
        private bool _loop;
        private long _cursor;
        private bool _playing;
        private bool _disposed;
        private readonly object _sync = new object();
        private Thread _refillThread;
        private volatile bool _stopRequested;
        // Live spatial parameters (written from main thread, read on refill thread)
        private volatile float _leftGain = 0.7071f;
        private volatile float _rightGain = 0.7071f;
        private volatile float _volume = 1f;
        private volatile float _lpAlpha = 0f; // 0 = disabled
        private float _lpState;
        private GCHandle[] _bufferHandles = new GCHandle[NumBuffers];
        private GCHandle[] _headerHandles = new GCHandle[NumBuffers];
        private WAVEHDR[] _headers = new WAVEHDR[NumBuffers];
        private byte[][] _rawBuffers = new byte[NumBuffers][];
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutOpen(out IntPtr hWaveOut, uint uDeviceID,
            ref WAVEFORMATEX lpFormat, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutPrepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutUnprepareHeader(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutReset(IntPtr hWaveOut);
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveOutClose(IntPtr hWaveOut);
        /// <summary>
        /// Start continuous (or one-shot) playback from resident mono PCM.
        /// Spatial gains can be updated at any time via UpdateSpatial without restarting.
        /// </summary>
        public bool Start(short[] monoSamples, int sampleRate, bool loop)
        {
            if (monoSamples == null || monoSamples.Length == 0 || sampleRate <= 0)
                return false;
            lock (_sync)
            {
                StopInternal();
                _mono = monoSamples;
                _sampleRate = sampleRate;
                _loop = loop;
                _cursor = 0;
                _lpState = 0f;
                _stopRequested = false;
                _playing = true;
                var fmt = new WAVEFORMATEX
                {
                    wFormatTag = 1,
                    nChannels = 2,
                    nSamplesPerSec = (uint)sampleRate,
                    wBitsPerSample = 16,
                    nBlockAlign = 4,
                    nAvgBytesPerSec = (uint)(sampleRate * 4),
                    cbSize = 0
                };
                uint r = waveOutOpen(out _hWaveOut, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
                if (r != 0)
                {
                    Console.WriteLine($"WaveOutPlayer: waveOutOpen failed ({r})");
                    _hWaveOut = IntPtr.Zero;
                    _playing = false;
                    return false;
                }
                int byteLen = BufferFrames * 4; // stereo 16-bit
                for (int i = 0; i < NumBuffers; i++)
                {
                    _rawBuffers[i] = new byte[byteLen];
                    _bufferHandles[i] = GCHandle.Alloc(_rawBuffers[i], GCHandleType.Pinned);
                    _headers[i] = new WAVEHDR
                    {
                        lpData = _bufferHandles[i].AddrOfPinnedObject(),
                        dwBufferLength = (uint)byteLen,
                        dwFlags = 0,
                        dwLoops = 0
                    };
                    _headerHandles[i] = GCHandle.Alloc(_headers[i], GCHandleType.Pinned);
                    r = waveOutPrepareHeader(_hWaveOut, _headerHandles[i].AddrOfPinnedObject(), (uint)Marshal.SizeOf<WAVEHDR>());
                    if (r != 0)
                    {
                        Console.WriteLine($"WaveOutPlayer: waveOutPrepareHeader failed ({r})");
                        Cleanup();
                        return false;
                    }
                    FillBuffer(i);
                    r = waveOutWrite(_hWaveOut, _headerHandles[i].AddrOfPinnedObject(), (uint)Marshal.SizeOf<WAVEHDR>());
                    if (r != 0)
                    {
                        Console.WriteLine($"WaveOutPlayer: waveOutWrite failed ({r})");
                        Cleanup();
                        return false;
                    }
                }
                _refillThread = new Thread(RefillLoop)
                {
                    IsBackground = true,
                    Name = "WaveOutSpatialRefill"
                };
                _refillThread.Start();
                return true;
            }
        }
        /// <summary>
        /// Update spatial parameters. Applied on the next buffer fill. Thread-safe.
        /// </summary>
        public void UpdateSpatial(float leftGain, float rightGain, float volume, float lowPassCutoffHz = 0f)
        {
            _leftGain = Math.Clamp(leftGain, 0f, 1f);
            _rightGain = Math.Clamp(rightGain, 0f, 1f);
            _volume = Math.Clamp(volume, 0f, 1f);
            if (lowPassCutoffHz > 20f && lowPassCutoffHz < _sampleRate * 0.45f)
            {
                // one-pole low-pass coefficient
                float x = (float)Math.Exp(-2.0 * Math.PI * lowPassCutoffHz / _sampleRate);
                _lpAlpha = x;
            }
            else
            {
                _lpAlpha = 0f;
            }
        }
        public bool IsPlaying
        {
            get { lock (_sync) return _playing && !_disposed; }
        }
        public void Stop()
        {
            lock (_sync) { StopInternal(); }
        }
        private void StopInternal()
        {
            _stopRequested = true;
            _playing = false;
            if (_refillThread != null && _refillThread.IsAlive)
            {
                try { _refillThread.Join(200); } catch { }
                _refillThread = null;
            }
            if (_hWaveOut != IntPtr.Zero)
            {
                try { waveOutReset(_hWaveOut); } catch { }
                Cleanup();
            }
        }
        private void RefillLoop()
        {
            while (!_stopRequested && _playing)
            {
                bool anyWork = false;
                for (int i = 0; i < NumBuffers; i++)
                {
                    if (_stopRequested) break;
                    // Re-read header from pinned memory
                    WAVEHDR hdr = (WAVEHDR)Marshal.PtrToStructure(_headerHandles[i].AddrOfPinnedObject(), typeof(WAVEHDR));
                    if ((hdr.dwFlags & WHDR_DONE) != 0)
                    {
                        if (!FillBuffer(i))
                        {
                            // end of non-looping clip
                            _playing = false;
                            break;
                        }
                        uint r = waveOutWrite(_hWaveOut, _headerHandles[i].AddrOfPinnedObject(), (uint)Marshal.SizeOf<WAVEHDR>());
                        if (r != 0)
                        {
                            _playing = false;
                            break;
                        }
                        anyWork = true;
                    }
                }
                if (!anyWork)
                    Thread.Yield();
            }
        }
        private bool FillBuffer(int index)
        {
            if (_mono == null || _mono.Length == 0) return false;
            float left = _leftGain;
            float right = _rightGain;
            float vol = _volume;
            float alpha = _lpAlpha;
            float lp = _lpState;
            short[] mono = _mono;
            long cursor = _cursor;
            int monoLen = mono.Length;
            bool loop = _loop;
            byte[] dest = _rawBuffers[index];
            int frames = BufferFrames;
            for (int f = 0; f < frames; f++)
            {
                if (cursor >= monoLen)
                {
                    if (loop)
                        cursor = 0;
                    else
                    {
                        // pad remaining with silence and signal end
                        for (int k = f; k < frames; k++)
                        {
                            int bi = k * 4;
                            dest[bi] = 0; dest[bi + 1] = 0;
                            dest[bi + 2] = 0; dest[bi + 3] = 0;
                        }
                        _cursor = cursor;
                        _lpState = lp;
                        return false;
                    }
                }
                float sample = mono[cursor] * vol;
                if (alpha > 0f)
                {
                    lp = alpha * lp + (1f - alpha) * sample;
                    sample = lp;
                }
                short sL = (short)Math.Clamp((int)(sample * left), short.MinValue, short.MaxValue);
                short sR = (short)Math.Clamp((int)(sample * right), short.MinValue, short.MaxValue);
                int baseIdx = f * 4;
                dest[baseIdx] = (byte)(sL & 0xFF);
                dest[baseIdx + 1] = (byte)((sL >> 8) & 0xFF);
                dest[baseIdx + 2] = (byte)(sR & 0xFF);
                dest[baseIdx + 3] = (byte)((sR >> 8) & 0xFF);
                cursor++;
            }
            _cursor = cursor;
            _lpState = lp;
            // Clear DONE flag so the header can be written again
            _headers[index].dwFlags = WHDR_PREPARED;
            Marshal.StructureToPtr(_headers[index], _headerHandles[index].AddrOfPinnedObject(), false);
            return true;
        }
        private void Cleanup()
        {
            if (_hWaveOut != IntPtr.Zero)
            {
                for (int i = 0; i < NumBuffers; i++)
                {
                    if (_headerHandles[i].IsAllocated)
                    {
                        try
                        {
                            waveOutUnprepareHeader(_hWaveOut, _headerHandles[i].AddrOfPinnedObject(), (uint)Marshal.SizeOf<WAVEHDR>());
                        }
                        catch { }
                        _headerHandles[i].Free();
                    }
                    if (_bufferHandles[i].IsAllocated)
                        _bufferHandles[i].Free();
                }
                try { waveOutClose(_hWaveOut); } catch { }
                _hWaveOut = IntPtr.Zero;
            }
            _rawBuffers = new byte[NumBuffers][];
            _headers = new WAVEHDR[NumBuffers];
            _bufferHandles = new GCHandle[NumBuffers];
            _headerHandles = new GCHandle[NumBuffers];
            _mono = null;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}