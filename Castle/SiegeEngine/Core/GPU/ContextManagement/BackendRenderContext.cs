using System;
using System.Collections.Generic;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    /// <summary>
    /// IRenderContext stand-in for DX/VK. Records textures, buffers, uniforms and
    /// DrawElements so a D3D11UiBatch can replay UI geometry on Present.
    /// </summary>
    public unsafe class BackendRenderContext : IRenderContext
    {
        readonly string _backend;
        bool _loggedDraw;
        uint _nextTex = 1;
        uint _nextBuf = 1;
        uint _boundArray, _boundElement, _boundTexture;
        float _uR = 1, _uG = 1, _uB = 1, _uA = 1, _uUseTex = 1;
        readonly List<PendingDraw> _draws = new List<PendingDraw>();
        D3D11UiBatch _ui;

        public float ClearR = 0.08f, ClearG = 0.08f, ClearB = 0.10f, ClearA = 1f;
        public int ViewportWidth { get; private set; }
        public int ViewportHeight { get; private set; }
        public AbstractRenderEnums Enums { get; } = new OpenGLEnums();

        struct PendingDraw
        {
            public uint Vbo, Ebo, Texture, IndexCount;
            public float R, G, B, A;
            public bool UseTex;
        }

        public struct QueuedDraw
        {
            public float[] Verts;
            public int[] Indices;
            public int Stride;
            public float R, G, B, A;
        }

        public readonly List<QueuedDraw> Draws = new List<QueuedDraw>();
        readonly Dictionary<uint, byte[]> _cpu = new Dictionary<uint, byte[]>();
        int _stride = 16;

        public BackendRenderContext(string backend, int width, int height)
        {
            _backend = backend;
            ViewportWidth = width;
            ViewportHeight = height;
        }

        internal void AttachUi(D3D11UiBatch ui) => _ui = ui;

        public void FlushDraws()
        {
            if (_ui == null || !_ui.Ready) { _draws.Clear(); return; }
            for (int i = 0; i < _draws.Count; i++)
            {
                var d = _draws[i];
                _ui.DrawIndexed(d.Vbo, d.Ebo, d.Texture, d.IndexCount, d.R, d.G, d.B, d.A, d.UseTex);
            }
            _draws.Clear();
            Draws.Clear();
        }

        void NoteOnce(string op)
        {
            if (_loggedDraw) return;
            _loggedDraw = true;
            Console.WriteLine($"[{_backend}] {op} recorded for D3D replay.");
        }

        public void ClearColor(float red, float green, float blue, float alpha)
        {
            ClearR = red; ClearG = green; ClearB = blue; ClearA = alpha;
        }

        public void Clear(int mask) { _draws.Clear(); Draws.Clear(); }

        public void Viewport(int x, int y, uint width, uint height)
        {
            ViewportWidth = (int)width;
            ViewportHeight = (int)height;
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

        public uint GenVertexArray() => 1;
        public void GenVertexArrays(uint n, out uint arrays) { arrays = 1; }
        public uint GenBuffer() => _nextBuf++;
        public void GenBuffers(uint n, out uint buffers) { buffers = _nextBuf++; }
        public void BindVertexArray(uint array) { }
        public void BindBuffer(int target, uint buffer)
        {
            if (target == Enums.ArrayBuffer) _boundArray = buffer;
            else if (target == Enums.ElementArrayBuffer) _boundElement = buffer;
        }

        public void BufferData(int target, uint size, void* data, int usage)
        {
            uint id = target == Enums.ElementArrayBuffer ? _boundElement : _boundArray;
            if (id == 0 || size == 0 || data == null) return;
            byte[] bytes = new byte[size];
            System.Runtime.InteropServices.Marshal.Copy((nint)data, bytes, 0, (int)size);
            _cpu[id] = bytes;
            _ui?.SetCpuBuffer(id, bytes);
        }

        public void BufferSubData(int target, int offset, uint size, void* data)
        {
            if (offset != 0) { BufferData(target, size, data, 0); return; }
            BufferData(target, size, data, 0);
        }

        public void EnableVertexAttribArray(uint index) { }
        public void DisableVertexAttribArray(uint index) { }
        public void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer)
        {
            if (index == 0)
                _stride = stride == 0 ? size * 4 : (int)stride;
        }
        public void VertexAttribIPointer(uint index, int size, int type, uint stride, void* pointer) { }
        public void DeleteVertexArray(uint array) { }
        public void DeleteBuffer(uint buffer) { }
        public void DeleteBuffers(uint n, uint* buffers) { }
        public void DrawArrays(int mode, int first, uint count) { NoteOnce("DrawArrays"); }
        public void DrawElements(int mode, uint count, int type, void* indices)
        {
            if (count == 0 || _boundArray == 0 || _boundElement == 0) return;
            _draws.Add(new PendingDraw
            {
                Vbo = _boundArray,
                Ebo = _boundElement,
                Texture = _boundTexture,
                IndexCount = count,
                R = _uR, G = _uG, B = _uB, A = _uA,
                UseTex = _uUseTex > 0.5f && _boundTexture != 0
            });
            if (_cpu.TryGetValue(_boundArray, out byte[] vb) && vb != null && vb.Length >= 8)
            {
                float[] verts = new float[vb.Length / 4];
                Buffer.BlockCopy(vb, 0, verts, 0, vb.Length);
                int[] idx = null;
                if (_cpu.TryGetValue(_boundElement, out byte[] ib) && ib != null && ib.Length >= 4)
                {
                    idx = new int[ib.Length / 4];
                    Buffer.BlockCopy(ib, 0, idx, 0, ib.Length);
                    if (count > 0 && count < idx.Length)
                    {
                        int[] cut = new int[count];
                        Array.Copy(idx, cut, (int)count);
                        idx = cut;
                    }
                }
                Draws.Add(new QueuedDraw { Verts = verts, Indices = idx, Stride = _stride <= 0 ? 16 : _stride, R = _uR, G = _uG, B = _uB, A = _uA });
            }
        }
        public bool IsVertexArray(uint array) => array != 0;
        public bool IsBuffer(uint buffer) => buffer != 0;

        public void GenTextures(uint n, out uint textures)
        {
            textures = _nextTex++;
        }
        public void BindTexture(int target, uint texture) { _boundTexture = texture; }
        public void TexImage2D(int target, int level, int internalformat, uint width, uint height, int border, int format, int type, void* pixels)
        {
            if (_boundTexture == 0 || width == 0 || height == 0 || pixels == null) return;
            byte[] rgba = ToRgba((int)width, (int)height, format, pixels);
            _ui?.SetTextureRgba(_boundTexture, (int)width, (int)height, rgba);
        }

        byte[] ToRgba(int width, int height, int format, void* pixels)
        {
            int count = width * height;
            byte[] dst = new byte[count * 4];
            byte* src = (byte*)pixels;
            bool bgra = format == Enums.PixelBgra;
            bool bgr = format == Enums.PixelBgr;
            bool rgb = format == Enums.PixelRgb;
            if (bgra)
            {
                for (int i = 0; i < count; i++)
                {
                    dst[i * 4 + 0] = src[i * 4 + 2];
                    dst[i * 4 + 1] = src[i * 4 + 1];
                    dst[i * 4 + 2] = src[i * 4 + 0];
                    dst[i * 4 + 3] = src[i * 4 + 3];
                }
            }
            else if (bgr)
            {
                for (int i = 0; i < count; i++)
                {
                    dst[i * 4 + 0] = src[i * 3 + 2];
                    dst[i * 4 + 1] = src[i * 3 + 1];
                    dst[i * 4 + 2] = src[i * 3 + 0];
                    dst[i * 4 + 3] = 255;
                }
            }
            else if (rgb)
            {
                for (int i = 0; i < count; i++)
                {
                    dst[i * 4 + 0] = src[i * 3 + 0];
                    dst[i * 4 + 1] = src[i * 3 + 1];
                    dst[i * 4 + 2] = src[i * 3 + 2];
                    dst[i * 4 + 3] = 255;
                }
            }
            else
            {
                int bytes = count * 4;
                System.Runtime.InteropServices.Marshal.Copy((nint)pixels, dst, 0, bytes);
            }
            return dst;
        }

        public void TexParameter(int target, int pname, int param) { }
        public void TexParameterf(int target, int pname, float param) { }
        public void PixelStore(int pname, int param) { }
        public void DeleteTexture(uint texture) { }
        public void DeleteTextures(uint n, uint* textures) { }
        public bool IsTexture(uint texture) => texture != 0;
        public void GenerateMipmap(int target) { }

        public uint CreateProgram() { return 1; }
        public uint CreateShader(int type) { return 1; }
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
        public void UseProgram(uint program) { }
        public int GetUniformLocation(uint program, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            if (name.Contains("Color", StringComparison.OrdinalIgnoreCase)) return 1;
            if (name.Contains("UseTexture", StringComparison.OrdinalIgnoreCase) || name.Contains("uUseTexture", StringComparison.OrdinalIgnoreCase)) return 2;
            if (name.Contains("Transform", StringComparison.OrdinalIgnoreCase)) return 3;
            return 4;
        }
        public void Uniform1(int location, float value)
        {
            if (location == 2) _uUseTex = value;
        }
        public void Uniform1(int location, int value)
        {
            if (location == 2) _uUseTex = value;
        }
        public void Uniform2(int location, float x, float y) { }
        public void Uniform3(int location, float x, float y, float z) { }
        public void Uniform4(int location, float x, float y, float z, float w)
        {
            if (location == 1) { _uR = x; _uG = y; _uB = z; _uA = w; }
        }
        public void UniformMatrix4(int location, uint count, bool transpose, float* value) { }
        public void UniformMatrix3(int location, uint count, bool transpose, float* value) { }
        public void GetProgramInterface(uint program, int programInterface, int pname, out int param) { param = 0; }
        public int GetProgramResourceLocation(uint program, int programInterface, string name) => -1;

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
