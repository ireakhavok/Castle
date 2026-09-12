using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// Records the GL-shaped UI calls (BufferData / DrawElements / TexImage2D / uniforms)
    /// so D3D12ContextManager can flush them on Present.
    /// </summary>
    public unsafe class D3D12RenderContext : IRenderContext
    {
        public readonly struct DrawOp
        {
            public readonly float[] Verts;
            public readonly int VertFloats;
            public readonly uint IndexCount;
            public readonly float R, G, B, A;
            public readonly float UseTexture;
            public readonly uint Texture;
            public readonly int VpX, VpY, VpW, VpH;
            public DrawOp(float[] verts, int vertFloats, uint indexCount, float r, float g, float b, float a, float useTex, uint tex, int vpX, int vpY, int vpW, int vpH)
            {
                Verts = verts; VertFloats = vertFloats; IndexCount = indexCount;
                R = r; G = g; B = b; A = a; UseTexture = useTex; Texture = tex;
                VpX = vpX; VpY = vpY; VpW = vpW; VpH = vpH;
            }
        }

        public sealed class CpuTexture
        {
            public int Width, Height;
            public byte[] Rgba;
            public int Generation;
        }

        readonly List<DrawOp> _draws = new List<DrawOp>();
        readonly Dictionary<uint, byte[]> _buffers = new Dictionary<uint, byte[]>();
        readonly Dictionary<uint, CpuTexture> _textures = new Dictionary<uint, CpuTexture>();
        readonly Dictionary<int, float[]> _uniforms = new Dictionary<int, float[]>();
        readonly Dictionary<string, int> _uniformNames = new Dictionary<string, int>(StringComparer.Ordinal);
        uint _nextBuffer = 1;
        uint _nextTexture = 1;
        uint _nextShader = 1;
        uint _nextProgram = 1;
        uint _boundArray;
        uint _boundElement;
        uint _boundTexture;
        uint _boundProgram;
        int _nextUniformLoc = 1;
        int _stride = 16;

        public float ClearR, ClearG, ClearB, ClearA = 1f;
        public int ViewportX { get; private set; }
        public int ViewportY { get; private set; }
        public int ViewportWidth { get; private set; }
        public int ViewportHeight { get; private set; }
        public AbstractRenderEnums Enums { get; } = new OpenGLEnums();

        public D3D12RenderContext(int width, int height)
        {
            ViewportX = 0;
            ViewportY = 0;
            ViewportWidth = width;
            ViewportHeight = height;
        }

        public DrawOp[] TakeDraws()
        {
            var arr = _draws.ToArray();
            _draws.Clear();
            return arr;
        }

        public bool TryGetTexture(uint id, out CpuTexture tex) => _textures.TryGetValue(id, out tex);

        public void ClearColor(float red, float green, float blue, float alpha)
        {
            ClearR = red; ClearG = green; ClearB = blue; ClearA = alpha;
        }

        public void Clear(int mask) { }
        public void Viewport(int x, int y, uint width, uint height)
        {
            ViewportX = x;
            ViewportY = y;
            ViewportWidth = Math.Max(1, (int)width);
            ViewportHeight = Math.Max(1, (int)height);
        }

        public int GetError() => 0;
        public void Enable(int cap) { }
        public void Disable(int cap) { }
        public void BlendFunc(int src, int dst) { }
        public void DepthMask(bool mask) { }
        public void DepthFunc(int func) { }
        public void ColorMask(bool r, bool g, bool b, bool a) { }
        public void ActiveTexture(int unit) { }
        public void BindFramebuffer(int target, uint framebuffer) { }
        public int CheckFramebufferStatus(int target) => Enums.FramebufferComplete;
        public void DrawBuffer(int mode) { }
        public void ReadBuffer(int mode) { }
        public void Scissor(int x, int y, uint width, uint height) { }
        public void CullFace(int mode) { }
        public void FrontFace(int mode) { }
        public void LineWidth(float width) { }
        public void GetFloat(int pname, out float param) { param = 0; }
        public void GetInteger(int pname, out int data) { data = 0; }
        public void GetInteger(int pname, int* data) { if (data != null) *data = 0; }
        public bool IsExtensionPresent(string extension) => false;

        public uint GenVertexArray() => _nextBuffer++;
        public void GenVertexArrays(uint n, out uint arrays) { arrays = _nextBuffer++; }
        public uint GenBuffer() => _nextBuffer++;
        public void GenBuffers(uint n, out uint buffers) { buffers = _nextBuffer++; }
        public void BindVertexArray(uint array) { }
        public void BindBuffer(int target, uint buffer)
        {
            if (target == Enums.ElementArrayBuffer) _boundElement = buffer;
            else _boundArray = buffer;
        }

        public void BufferData(int target, uint size, void* data, int usage)
        {
            uint id = target == Enums.ElementArrayBuffer ? _boundElement : _boundArray;
            if (id == 0) return;
            var bytes = new byte[size];
            if (data != null && size > 0) Marshal.Copy((nint)data, bytes, 0, (int)size);
            _buffers[id] = bytes;
        }

        public void BufferSubData(int target, int offset, uint size, void* data)
        {
            uint id = target == Enums.ElementArrayBuffer ? _boundElement : _boundArray;
            if (id == 0 || data == null) return;
            if (!_buffers.TryGetValue(id, out var bytes) || bytes == null) return;
            int len = Math.Min((int)size, bytes.Length - offset);
            if (len <= 0) return;
            Marshal.Copy((nint)data, bytes, offset, len);
        }

        public void EnableVertexAttribArray(uint index) { }
        public void DisableVertexAttribArray(uint index) { }
        public void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer)
        {
            if (index == 0)
                _stride = stride == 0 ? Math.Max(8, size * 4) : (int)stride;
        }
        public void VertexAttribIPointer(uint index, int size, int type, uint stride, void* pointer) { }
        public void DeleteVertexArray(uint array) { }
        public void DeleteBuffer(uint buffer) { }
        public void DeleteBuffers(uint n, uint* buffers) { }
        public void DrawArrays(int mode, int first, uint count)
        {
            RecordDraw(count, indexed: false);
        }
        public void DrawElements(int mode, uint count, int type, void* indices)
        {
            RecordDraw(count, indexed: true);
        }

        static float[] PackXyUv(byte[] vb, int stride)
        {
            if (vb == null || vb.Length < 8) return Array.Empty<float>();
            if (stride < 8) stride = 8;
            int verts = vb.Length / stride;
            if (verts <= 0) return Array.Empty<float>();
            int uvOff = (stride == 20 || stride == 24) ? 12 : 8;
            var dst = new float[verts * 4];
            for (int i = 0; i < verts; i++)
            {
                int s = i * stride;
                dst[i * 4 + 0] = BitConverter.ToSingle(vb, s);
                dst[i * 4 + 1] = BitConverter.ToSingle(vb, s + 4);
                if (stride >= uvOff + 8)
                {
                    dst[i * 4 + 2] = BitConverter.ToSingle(vb, s + uvOff);
                    dst[i * 4 + 3] = BitConverter.ToSingle(vb, s + uvOff + 4);
                }
            }
            return dst;
        }

        void RecordDraw(uint indexCount, bool indexed)
        {
            if (!_buffers.TryGetValue(_boundArray, out var vb) || vb == null || vb.Length < 8) return;
            int stride = _stride <= 0 ? 16 : _stride;
            if (!indexed && indexCount > 0)
            {
                int guess = vb.Length / (int)indexCount;
                if (guess >= 8 && guess <= 32) stride = guess;
            }
            else if (indexed && _buffers.TryGetValue(_boundElement, out var ib) && ib != null && ib.Length >= 2)
            {
                int idxStride = (indexCount > 0 && ib.Length == (int)indexCount * 2) ? 2 : 4;
                int nIdx = Math.Min(idxStride == 2 ? ib.Length / 2 : ib.Length / 4, indexCount > 0 ? (int)indexCount : int.MaxValue);
                int maxI = 0;
                for (int i = 0; i < nIdx; i++)
                {
                    int v = idxStride == 2 ? BitConverter.ToUInt16(ib, i * 2) : BitConverter.ToInt32(ib, i * 4);
                    if (v > maxI) maxI = v;
                }
                int guess = vb.Length / Math.Max(1, maxI + 1);
                if (guess >= 8 && guess <= 64) stride = guess;
            }
            float[] packed = PackXyUv(vb, stride);
            if (packed.Length < 8) return;
            if (!indexed && indexCount > 0)
            {
                int keep = Math.Min(packed.Length, (int)indexCount * 4);
                if (keep < packed.Length)
                {
                    var cut = new float[keep];
                    Array.Copy(packed, cut, keep);
                    packed = cut;
                }
            }
            GetUniform4(Loc("uColor"), out float r, out float g, out float b, out float a);
            float useTex = GetUniform1(Loc("uUseTexture"));
            if (useTex == 0 && GetUniform1(Loc("uUseTex")) != 0) useTex = GetUniform1(Loc("uUseTex"));
            // Unset CSS background is Vector4.Zero. Do not promote it to opaque white.
            if (useTex < 0.5f && a <= 0f) return;
            _draws.Add(new DrawOp(packed, packed.Length, indexCount == 0 ? (uint)(packed.Length / 4) : indexCount, r, g, b, a, useTex, _boundTexture, ViewportX, ViewportY, ViewportWidth, ViewportHeight));
        }

        public bool IsVertexArray(uint array) => array != 0;
        public bool IsBuffer(uint buffer) => _buffers.ContainsKey(buffer);

        public void GenTextures(uint n, out uint textures)
        {
            textures = _nextTexture++;
            _textures[textures] = new CpuTexture();
        }
        public void BindTexture(int target, uint texture) { _boundTexture = texture; }
        public void TexImage2D(int target, int level, int internalformat, uint width, uint height, int border, int format, int type, void* pixels)
        {
            if (_boundTexture == 0)
            {
                _boundTexture = _nextTexture++;
            }
            int gen = 1;
            if (_textures.TryGetValue(_boundTexture, out var prev) && prev != null)
                gen = prev.Generation + 1;
            var tex = new CpuTexture { Width = (int)width, Height = (int)height, Generation = gen };
            int bytes = (int)width * (int)height * 4;
            tex.Rgba = new byte[Math.Max(bytes, 4)];
            if (pixels != null && bytes > 0)
            {
                try
                {
                    bool bgra = format == Enums.PixelBgra;
                    bool bgr = format == Enums.PixelBgr;
                    bool rgba = format == Enums.Rgba || format == Enums.PixelRgba;
                    int srcStride = (bgra || rgba) ? 4 : 3;
                    int srcBytes = (int)width * (int)height * srcStride;
                    var src = new byte[srcBytes];
                    Marshal.Copy((nint)pixels, src, 0, srcBytes);
                    if (rgba)
                    {
                        Buffer.BlockCopy(src, 0, tex.Rgba, 0, Math.Min(srcBytes, bytes));
                    }
                    else if (bgra)
                    {
                        int di = 0;
                        for (int i = 0; i + 3 < src.Length && di + 3 < tex.Rgba.Length; i += 4)
                        {
                            tex.Rgba[di++] = src[i + 2];
                            tex.Rgba[di++] = src[i + 1];
                            tex.Rgba[di++] = src[i];
                            tex.Rgba[di++] = src[i + 3];
                        }
                    }
                    else
                    {
                        // PixelBgr / PixelRgb — BackgroundRenderer uploads Format24bppRgb as BGR
                        int di = 0;
                        for (int i = 0; i + 2 < src.Length && di + 3 < tex.Rgba.Length; i += 3)
                        {
                            if (bgr)
                            {
                                tex.Rgba[di++] = src[i + 2];
                                tex.Rgba[di++] = src[i + 1];
                                tex.Rgba[di++] = src[i];
                            }
                            else
                            {
                                tex.Rgba[di++] = src[i];
                                tex.Rgba[di++] = src[i + 1];
                                tex.Rgba[di++] = src[i + 2];
                            }
                            tex.Rgba[di++] = 255;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DirectX12] TexImage2D copy failed: {ex.Message}");
                }
            }
            _textures[_boundTexture] = tex;
            if (tex.Width * tex.Height >= 64)
                Console.WriteLine($"[DirectX12] TexImage2D id={_boundTexture} {tex.Width}x{tex.Height} format={format} bytes={tex.Rgba?.Length ?? 0} gen={tex.Generation}");
        }
        public void TexParameter(int target, int pname, int param) { }
        public void TexParameterf(int target, int pname, float param) { }
        public void PixelStore(int pname, int param) { }
        public void DeleteTexture(uint texture) { _textures.Remove(texture); }
        public void DeleteTextures(uint n, uint* textures) { }
        public bool IsTexture(uint texture) => _textures.ContainsKey(texture);
        public void GenerateMipmap(int target) { }

        public uint CreateProgram() => _nextProgram++;
        public uint CreateShader(int type) => _nextShader++;
        public void ShaderSource(uint shader, string source) { }
        public void CompileShader(uint shader) { }
        public void GetShader(uint shader, int param, out int value) { value = 1; }
        public string GetShaderInfoLog(uint shader) => "";
        public void AttachShader(uint program, uint shader) { }
        public void DetachShader(uint program, uint shader) { }
        public void LinkProgram(uint program) { }
        public void GetProgram(uint program, int prop, out int value) { value = 1; }
        public string GetProgramInfoLog(uint program) => "";
        public void DeleteShader(uint shader) { }
        public void DeleteProgram(uint program) { }
        public void UseProgram(uint program) { _boundProgram = program; }
        public int GetUniformLocation(uint program, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            if (_uniformNames.TryGetValue(name, out int loc)) return loc;
            loc = _nextUniformLoc++;
            _uniformNames[name] = loc;
            return loc;
        }
        public void Uniform1(int location, float value) { _uniforms[location] = new[] { value }; }
        public void Uniform1(int location, int value) { _uniforms[location] = new[] { (float)value }; }
        public void Uniform2(int location, float x, float y) { _uniforms[location] = new[] { x, y }; }
        public void Uniform3(int location, float x, float y, float z) { _uniforms[location] = new[] { x, y, z }; }
        public void Uniform4(int location, float x, float y, float z, float w) { _uniforms[location] = new[] { x, y, z, w }; }
        public void UniformMatrix4(int location, uint count, bool transpose, float* value)
        {
            var m = new float[16];
            if (value != null) Marshal.Copy((nint)value, m, 0, 16);
            _uniforms[location] = m;
        }
        public void UniformMatrix3(int location, uint count, bool transpose, float* value) { }
        public void GetProgramInterface(uint program, int programInterface, int pname, out int param) { param = 0; }
        public int GetProgramResourceLocation(uint program, int programInterface, string name) => GetUniformLocation(program, name);

        int Loc(string name) => _uniformNames.TryGetValue(name, out int loc) ? loc : -1;
        float GetUniform1(int loc)
        {
            if (loc < 0 || !_uniforms.TryGetValue(loc, out var v) || v == null || v.Length == 0) return 0;
            return v[0];
        }
        void GetUniform4(int loc, out float x, out float y, out float z, out float w)
        {
            x = y = z = 0; w = 1;
            if (loc < 0 || !_uniforms.TryGetValue(loc, out var v) || v == null) return;
            if (v.Length > 0) x = v[0];
            if (v.Length > 1) y = v[1];
            if (v.Length > 2) z = v[2];
            if (v.Length > 3) w = v[3];
        }

        public void GenFramebuffers(uint n, out uint framebuffers) { framebuffers = 0; }
        public void DeleteFramebuffers(uint n, uint* framebuffers) { }
        public void FramebufferTexture2D(int target, int attachment, int textarget, uint texture, int level) { }
        public void GenRenderbuffers(uint n, out uint renderbuffers) { renderbuffers = 0; }
        public void DeleteRenderbuffers(uint n, uint* renderbuffers) { }
        public void BindRenderbuffer(int target, uint renderbuffer) { }
        public void RenderbufferStorage(int target, int internalformat, uint width, uint height) { }
        public void FramebufferRenderbuffer(int target, int attachment, int renderbuffertarget, uint renderbuffer) { }
        public void ReadPixels(int x, int y, uint width, uint height, int format, int type, void* data) { }
        public void ClearBufferuiv(int buffer, int drawbuffer, uint* value) { }
        public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ) { }
        public void MemoryBarrier(int barriers) { }
        public void BindBufferBase(int target, uint index, uint buffer) { }
        public void BindBufferRange(int target, uint index, uint buffer, int offset, uint size) { }
        public void* MapBuffer(int target, int access) => null;
        public void* MapBufferRange(int target, int offset, uint length, int access) => null;
        public bool UnmapBuffer(int target) => true;
        public uint FenceSync(int condition, uint flags) => 0;
        public int ClientWaitSync(uint sync, uint flags, ulong timeout) => 0;
        public void DeleteSync(uint sync) { }
    }
}
