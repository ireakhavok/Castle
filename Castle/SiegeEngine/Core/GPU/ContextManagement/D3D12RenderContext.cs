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
            public readonly int ScX, ScY, ScW, ScH;
            public readonly bool ScissorOn;
            public DrawOp(float[] verts, int vertFloats, uint indexCount, float r, float g, float b, float a, float useTex, uint tex, int vpX, int vpY, int vpW, int vpH, int scX, int scY, int scW, int scH, bool scissorOn)
            {
                Verts = verts; VertFloats = vertFloats; IndexCount = indexCount;
                R = r; G = g; B = b; A = a; UseTexture = useTex; Texture = tex;
                VpX = vpX; VpY = vpY; VpW = vpW; VpH = vpH;
                ScX = scX; ScY = scY; ScW = scW; ScH = scH; ScissorOn = scissorOn;
            }
        }

        /// <summary>Clip-space xyzw + uv world triangles. VS passes pos through after GL→D3D z.</summary>
        public readonly struct WorldDrawOp
        {
            public readonly float[] Verts; // 6 floats/vert: clip.xyzw, uv
            public readonly int VertFloats;
            public readonly float R, G, B, A;
            public readonly float UseTexture;
            public readonly uint Texture;
            public readonly int VpX, VpY, VpW, VpH;
            public readonly int ScX, ScY, ScW, ScH;
            public readonly bool ScissorOn;
            public WorldDrawOp(float[] verts, int vertFloats, float r, float g, float b, float a, float useTex, uint tex, int vpX, int vpY, int vpW, int vpH, int scX, int scY, int scW, int scH, bool scissorOn)
            {
                Verts = verts; VertFloats = vertFloats;
                R = r; G = g; B = b; A = a; UseTexture = useTex; Texture = tex;
                VpX = vpX; VpY = vpY; VpW = vpW; VpH = vpH;
                ScX = scX; ScY = scY; ScW = scW; ScH = scH; ScissorOn = scissorOn;
            }
        }

        sealed class VaoState
        {
            public uint Array;
            public uint Element;
            public readonly int[] Size = new int[8];
            public readonly int[] Stride = new int[8];
            public readonly int[] Offset = new int[8];
            public readonly bool[] Enabled = new bool[8];
        }

        public sealed class CpuTexture
        {
            public int Width, Height;
            public byte[] Rgba;
            public int Generation;
        }

        readonly List<DrawOp> _draws = new List<DrawOp>();
        readonly List<WorldDrawOp> _world = new List<WorldDrawOp>();
        readonly Dictionary<uint, VaoState> _vaos = new Dictionary<uint, VaoState>();
        readonly uint[] _texUnit = new uint[32];
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
        uint _boundVao;
        int _activeTexUnit;
        int _nextUniformLoc = 1;
        int _stride = 16;
        int _posSize = 2;
        int _posOff;
        int _uvOff = 8;
        int _scX, _scY, _scW, _scH;
        bool _scissorOn;
        bool _blendOn;
        bool _depthOn;
        uint _boundFbo;
        uint _nextFbo = 1;

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
            _scX = 0; _scY = 0; _scW = width; _scH = height;
            _scissorOn = false;
        }

        public DrawOp[] TakeDraws()
        {
            var arr = _draws.ToArray();
            _draws.Clear();
            return arr;
        }

        public WorldDrawOp[] TakeWorldDraws()
        {
            var arr = _world.ToArray();
            _world.Clear();
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
        public void Enable(int cap)
        {
            if (cap == Enums.ScissorTest) _scissorOn = true;
            if (cap == Enums.Blend) _blendOn = true;
            if (cap == Enums.DepthTest) _depthOn = true;
        }
        public void Disable(int cap)
        {
            if (cap == Enums.ScissorTest) _scissorOn = false;
            if (cap == Enums.Blend) _blendOn = false;
            if (cap == Enums.DepthTest) _depthOn = false;
        }
        public void BlendFunc(int src, int dst) { }
        public void DepthMask(bool mask) { }
        public void DepthFunc(int func) { }
        public void ColorMask(bool r, bool g, bool b, bool a) { }
        public void ActiveTexture(int unit)
        {
            int u = unit - Enums.Texture0;
            if (u < 0 || u > 31) u = 0;
            _activeTexUnit = u;
            _boundTexture = _texUnit[u];
        }
        public void BindFramebuffer(int target, uint framebuffer) { _boundFbo = framebuffer; }
        public int CheckFramebufferStatus(int target) => Enums.FramebufferComplete;
        public void DrawBuffer(int mode) { }
        public void ReadBuffer(int mode) { }
        public void Scissor(int x, int y, uint width, uint height)
        {
            _scX = x;
            _scY = y;
            _scW = Math.Max(1, (int)width);
            _scH = Math.Max(1, (int)height);
        }
        public void CullFace(int mode) { }
        public void FrontFace(int mode) { }
        public void LineWidth(float width) { }
        public void GetFloat(int pname, out float param) { param = 0; }
        public void GetInteger(int pname, out int data)
        {
            data = 0;
            if (pname == Enums.FramebufferBinding) data = (int)_boundFbo;
            else if (pname == Enums.ScissorTest) data = _scissorOn ? 1 : 0;
            else if (pname == Enums.Blend) data = _blendOn ? 1 : 0;
            else if (pname == Enums.Viewport) data = ViewportWidth;
        }
        public void GetInteger(int pname, int* data)
        {
            if (data == null) return;
            if (pname == Enums.Viewport)
            {
                data[0] = ViewportX;
                data[1] = ViewportY;
                data[2] = Math.Max(1, ViewportWidth);
                data[3] = Math.Max(1, ViewportHeight);
                return;
            }
            if (pname == Enums.ScissorBox)
            {
                data[0] = _scX;
                data[1] = _scY;
                data[2] = Math.Max(1, _scW);
                data[3] = Math.Max(1, _scH);
                return;
            }
            if (pname == Enums.FramebufferBinding) { *data = (int)_boundFbo; return; }
            if (pname == Enums.ScissorTest) { *data = _scissorOn ? 1 : 0; return; }
            if (pname == Enums.Blend) { *data = _blendOn ? 1 : 0; return; }
            *data = 0;
        }
        public bool IsExtensionPresent(string extension) => false;

        VaoState EnsureVao(uint id)
        {
            if (id == 0) return null;
            if (!_vaos.TryGetValue(id, out var v) || v == null)
            {
                v = new VaoState();
                _vaos[id] = v;
            }
            return v;
        }

        public uint GenVertexArray()
        {
            uint id = _nextBuffer++;
            _vaos[id] = new VaoState();
            return id;
        }
        public void GenVertexArrays(uint n, out uint arrays)
        {
            arrays = _nextBuffer++;
            _vaos[arrays] = new VaoState();
        }
        public uint GenBuffer() => _nextBuffer++;
        public void GenBuffers(uint n, out uint buffers) { buffers = _nextBuffer++; }
        public void BindVertexArray(uint array)
        {
            _boundVao = array;
            if (array == 0) return;
            var v = EnsureVao(array);
            _boundArray = v.Array;
            _boundElement = v.Element;
            _stride = v.Stride[0] > 0 ? v.Stride[0] : _stride;
            _posSize = v.Size[0] > 0 ? v.Size[0] : _posSize;
            _posOff = v.Offset[0];
            if (v.Size[2] >= 2)
                _uvOff = v.Offset[2];
        }
        public void BindBuffer(int target, uint buffer)
        {
            if (target == Enums.ElementArrayBuffer)
            {
                _boundElement = buffer;
                if (_boundVao != 0) EnsureVao(_boundVao).Element = buffer;
            }
            else
            {
                _boundArray = buffer;
                if (_boundVao != 0) EnsureVao(_boundVao).Array = buffer;
            }
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

        public void EnableVertexAttribArray(uint index)
        {
            if (index < 8 && _boundVao != 0) EnsureVao(_boundVao).Enabled[index] = true;
        }
        public void DisableVertexAttribArray(uint index)
        {
            if (index < 8 && _boundVao != 0) EnsureVao(_boundVao).Enabled[index] = false;
        }
        public void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer)
        {
            int off = (int)(nint)pointer;
            int str = stride == 0 ? Math.Max(8, size * 4) : (int)stride;
            if (index == 0)
            {
                _stride = str;
                _posSize = size;
                _posOff = off;
            }
            if (index == 2 && size >= 2)
                _uvOff = off;
            if (_boundVao != 0 && index < 8)
            {
                var v = EnsureVao(_boundVao);
                v.Size[index] = size;
                v.Stride[index] = str;
                v.Offset[index] = off;
                v.Enabled[index] = true;
            }
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

        static float[] PackXyUv(byte[] vb, int stride, int maxVerts)
        {
            if (vb == null || vb.Length < 8) return Array.Empty<float>();
            if (stride < 8) stride = 8;
            int verts = vb.Length / stride;
            if (maxVerts > 0 && verts > maxVerts) verts = maxVerts;
            if (verts <= 0) return Array.Empty<float>();
            int uvOff = (stride == 20 || stride == 24) ? 12 : 8;
            var dst = new float[verts * 4];
            for (int i = 0; i < verts; i++)
            {
                int s = i * stride;
                if (s + 4 >= vb.Length) break;
                dst[i * 4 + 0] = BitConverter.ToSingle(vb, s);
                dst[i * 4 + 1] = BitConverter.ToSingle(vb, s + 4);
                if (stride >= uvOff + 8 && s + uvOff + 8 <= vb.Length)
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
            if (_posSize >= 3 || stride > 16)
            {
                RecordWorld(vb, stride, indexCount, indexed);
                return;
            }
            // DrawArrays must not guess stride from allocated-bytes / draw-count.
            // TextRenderer keeps a large preallocated VBO and draws a short glyph run;
            // that guess lands in 8..32 and reads UV as XY — glyphs sliver across the screen.
            if (indexed && _buffers.TryGetValue(_boundElement, out var ib) && ib != null && ib.Length >= 2)
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
                if (guess >= 8 && guess <= 16) stride = guess;
            }
            if (stride > 16) return;
            int liveVerts = 0;
            if (!indexed && indexCount > 0)
                liveVerts = (int)indexCount;
            float[] packed = PackXyUv(vb, stride, liveVerts);
            if (packed.Length < 8) return;
            int packedVerts = packed.Length / 4;
            packed = ExpandToTriangles(packed, packedVerts, indexed, indexCount);
            if (packed.Length < 12) return;
            GetUniform4(Loc("uColor"), out float r, out float g, out float b, out float a);
            float useTex = GetUniform1(Loc("uUseTexture"));
            if (useTex == 0 && GetUniform1(Loc("uUseTex")) != 0) useTex = GetUniform1(Loc("uUseTex"));
            // Unset CSS fill is Vector4.Zero. If this draw is a rounded/border pass,
            // uColor is the fill (transparent) and the visible edge is uBorderColor.
            if (useTex < 0.5f && a <= 0f)
            {
                float bw = GetUniform1(Loc("uBorderWidth"));
                GetUniform4(Loc("uBorderColor"), out float br, out float bg, out float bb, out float ba);
                if (bw > 0f && ba > 0f)
                {
                    r = br; g = bg; b = bb; a = ba;
                }
                else
                {
                    return;
                }
            }
            _draws.Add(new DrawOp(packed, packed.Length, (uint)(packed.Length / 4), r, g, b, a, useTex, _boundTexture, ViewportX, ViewportY, ViewportWidth, ViewportHeight, _scX, _scY, _scW > 0 ? _scW : ViewportWidth, _scH > 0 ? _scH : ViewportHeight, _scissorOn));
        }


        void RecordWorld(byte[] vb, int stride, uint indexCount, bool indexed)
        {
            if (stride < 12) stride = 12;
            int posOff = _posOff;
            int uvOff = _uvOff;
            if (uvOff < 8) uvOff = stride >= 32 ? 24 : -1;
            int[] idx = BuildIndices(indexed, indexCount, vb.Length / stride);
            if (idx == null || idx.Length < 3) return;
            GetMatrix("uModel", out var model);
            GetMatrix("uView", out var view);
            GetMatrix("uProjection", out var proj);
            System.Numerics.Matrix4x4 mvp = model * view * proj;
            var dst = new float[idx.Length * 6];
            int o = 0;
            for (int i = 0; i < idx.Length; i++)
            {
                int s = idx[i] * stride + posOff;
                if (s + 12 > vb.Length) continue;
                float x = BitConverter.ToSingle(vb, s);
                float y = BitConverter.ToSingle(vb, s + 4);
                float z = BitConverter.ToSingle(vb, s + 8);
                var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(x, y, z, 1f), mvp);
                clip.Z = clip.Z * 0.5f + clip.W * 0.5f;
                float u = 0f, v = 0f;
                if (uvOff >= 0 && idx[i] * stride + uvOff + 8 <= vb.Length)
                {
                    u = BitConverter.ToSingle(vb, idx[i] * stride + uvOff);
                    v = BitConverter.ToSingle(vb, idx[i] * stride + uvOff + 4);
                }
                dst[o++] = clip.X; dst[o++] = clip.Y; dst[o++] = clip.Z; dst[o++] = clip.W;
                dst[o++] = u; dst[o++] = v;
            }
            if (o < 18) return;
            if (o != dst.Length)
            {
                var cut = new float[o];
                Array.Copy(dst, cut, o);
                dst = cut;
            }
            uint tex = _texUnit[0] != 0 ? _texUnit[0] : _boundTexture;
            float useTex = tex != 0 ? 1f : 0f;
            GetUniform4(Loc("uColor"), out float r, out float g, out float b, out float a);
            if (a <= 0f) { r = g = b = a = 1f; }
            if (GetUniform1(Loc("uHasTexture")) == 0 && tex == 0) useTex = 0f;
            _world.Add(new WorldDrawOp(dst, dst.Length, r, g, b, a, useTex, tex,
                ViewportX, ViewportY, ViewportWidth, ViewportHeight,
                _scX, _scY, _scW > 0 ? _scW : ViewportWidth, _scH > 0 ? _scH : ViewportHeight, _scissorOn));
        }

        int[] BuildIndices(bool indexed, uint indexCount, int vertCount)
        {
            if (indexed && _buffers.TryGetValue(_boundElement, out var ib) && ib != null && ib.Length >= 2)
            {
                int idxStride = (indexCount > 0 && ib.Length == (int)indexCount * 2) ? 2 : 4;
                int nIdx = Math.Min(idxStride == 2 ? ib.Length / 2 : ib.Length / 4, indexCount > 0 ? (int)indexCount : int.MaxValue);
                nIdx -= nIdx % 3;
                if (nIdx < 3) return null;
                var idx = new int[nIdx];
                int w = 0;
                for (int i = 0; i < nIdx; i++)
                {
                    int v = idxStride == 2 ? BitConverter.ToUInt16(ib, i * 2) : BitConverter.ToInt32(ib, i * 4);
                    if (v < 0 || v >= vertCount) continue;
                    idx[w++] = v;
                }
                if (w < 3) return null;
                if (w != idx.Length)
                {
                    var cut = new int[w - (w % 3)];
                    Array.Copy(idx, cut, cut.Length);
                    return cut.Length >= 3 ? cut : null;
                }
                return idx;
            }
            if (!indexed && indexCount >= 3)
            {
                int n = (int)indexCount;
                n -= n % 3;
                var idx = new int[n];
                for (int i = 0; i < n; i++) idx[i] = i;
                return idx;
            }
            return null;
        }

        void GetMatrix(string name, out System.Numerics.Matrix4x4 m)
        {
            m = System.Numerics.Matrix4x4.Identity;
            int loc = Loc(name);
            if (loc < 0 || !_uniforms.TryGetValue(loc, out var v) || v == null || v.Length < 16) return;
            m = new System.Numerics.Matrix4x4(
                v[0], v[1], v[2], v[3],
                v[4], v[5], v[6], v[7],
                v[8], v[9], v[10], v[11],
                v[12], v[13], v[14], v[15]);
        }

        float[] ExpandToTriangles(float[] packed, int packedVerts, bool indexed, uint indexCount)
        {
            void CopyVert(float[] src, int vi, float[] dst, ref int o)
            {
                int s = vi * 4;
                if (s + 3 >= src.Length) return;
                dst[o++] = src[s]; dst[o++] = src[s + 1]; dst[o++] = src[s + 2]; dst[o++] = src[s + 3];
            }
            if (indexed && _buffers.TryGetValue(_boundElement, out var ib) && ib != null && ib.Length >= 2 && indexCount >= 3)
            {
                int idxStride = (ib.Length == (int)indexCount * 2) ? 2 : 4;
                int nIdx = Math.Min(idxStride == 2 ? ib.Length / 2 : ib.Length / 4, (int)indexCount);
                nIdx -= nIdx % 3;
                if (nIdx < 3) return packed;
                var dst = new float[nIdx * 4];
                int o = 0;
                for (int i = 0; i < nIdx; i++)
                {
                    int vi = idxStride == 2 ? BitConverter.ToUInt16(ib, i * 2) : BitConverter.ToInt32(ib, i * 4);
                    if (vi < 0 || vi >= packedVerts) continue;
                    CopyVert(packed, vi, dst, ref o);
                }
                if (o < 12) return packed;
                if (o != dst.Length)
                {
                    var cut = new float[o];
                    Array.Copy(dst, cut, o);
                    return cut;
                }
                return dst;
            }
            // DrawArrays: 4 verts is a triangle fan / quad. 6+ verts already triangles (text).
            if (!indexed && packedVerts == 4)
            {
                var dst = new float[24];
                int o = 0;
                CopyVert(packed, 0, dst, ref o);
                CopyVert(packed, 1, dst, ref o);
                CopyVert(packed, 2, dst, ref o);
                CopyVert(packed, 0, dst, ref o);
                CopyVert(packed, 2, dst, ref o);
                CopyVert(packed, 3, dst, ref o);
                return dst;
            }
            return packed;
        }

        public bool IsVertexArray(uint array) => array != 0;
        public bool IsBuffer(uint buffer) => _buffers.ContainsKey(buffer);

        public void GenTextures(uint n, out uint textures)
        {
            textures = _nextTexture++;
            _textures[textures] = new CpuTexture();
        }
        public void BindTexture(int target, uint texture)
        {
            _boundTexture = texture;
            if (_activeTexUnit >= 0 && _activeTexUnit < _texUnit.Length)
                _texUnit[_activeTexUnit] = texture;
        }
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
                catch
                {
                }
            }
            _textures[_boundTexture] = tex;
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

        public void GenFramebuffers(uint n, out uint framebuffers) { framebuffers = _nextFbo++; }
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
