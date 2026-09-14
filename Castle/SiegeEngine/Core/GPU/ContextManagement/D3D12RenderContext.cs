using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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
            public readonly float UseRounded;
            public readonly float BorderWidth;
            public readonly float Br, Bg, Bb, Ba;
            public readonly float Rx, Ry, Rz, Rw;
            public readonly float RectW, RectH;
            public readonly int VpX, VpY, VpW, VpH;
            public readonly int ScX, ScY, ScW, ScH;
            public readonly bool ScissorOn;
            public readonly int Mode;
            public DrawOp(float[] verts, int vertFloats, uint indexCount, float r, float g, float b, float a, float useTex, uint tex,
                float useRounded, float borderWidth, float br, float bg, float bb, float ba,
                float rx, float ry, float rz, float rw, float rectW, float rectH,
                int vpX, int vpY, int vpW, int vpH, int scX, int scY, int scW, int scH, bool scissorOn, int mode)
            {
                Verts = verts; VertFloats = vertFloats; IndexCount = indexCount;
                R = r; G = g; B = b; A = a; UseTexture = useTex; Texture = tex;
                UseRounded = useRounded; BorderWidth = borderWidth;
                Br = br; Bg = bg; Bb = bb; Ba = ba;
                Rx = rx; Ry = ry; Rz = rz; Rw = rw; RectW = rectW; RectH = rectH;
                VpX = vpX; VpY = vpY; VpW = vpW; VpH = vpH;
                ScX = scX; ScY = scY; ScW = scW; ScH = scH; ScissorOn = scissorOn; Mode = mode;
            }
        }

        public readonly struct WorldDrawOp
        {
            public readonly uint Vbo;
            public readonly uint Ebo;
            public readonly int VtxStride;
            public readonly int UvOff;
            public readonly int IdxStride;
            public readonly uint IndexCount;
            public readonly float[] Mvp;
            public readonly float R, G, B, A;
            public readonly float UseTexture;
            public readonly uint Texture;
            public readonly uint ColorTarget;
            public readonly bool DepthOn;
            public readonly bool UseSky;
            public readonly uint Program;
            public readonly int Kind;
            public readonly float[] Model;
            public readonly float[] View;
            public readonly float[] Projection;
            public readonly float[] Orientation;
            public readonly float VerticalOffset;
            public readonly float Unlit;
            public readonly float HasTexture;
            public readonly float LightIntensity;
            public readonly float AmbientStrength;
            public readonly float[] LightDir;
            public readonly float[] LightColor;
            public readonly float[] AmbientColor;
            public readonly float HasOpacity;
            public readonly float OpacitySlots;
            public readonly uint OpacityTex;
            public readonly int VpX, VpY, VpW, VpH;
            public readonly int ScX, ScY, ScW, ScH;
            public readonly bool ScissorOn;
            public readonly int Mode;
            public WorldDrawOp(uint vbo, uint ebo, int vtxStride, int uvOff, int idxStride, uint indexCount, float[] mvp,
                float r, float g, float b, float a, float useTex, uint tex, uint colorTarget, bool depthOn, bool useSky,
                uint program, int kind,
                float[] model, float[] view, float[] projection, float[] orientation,
                float verticalOffset, float unlit, float hasTexture, float lightIntensity, float ambientStrength,
                float[] lightDir, float[] lightColor, float[] ambientColor,
                float hasOpacity, float opacitySlots, uint opacityTex,
                int vpX, int vpY, int vpW, int vpH, int scX, int scY, int scW, int scH, bool scissorOn, int mode)
            {
                Vbo = vbo; Ebo = ebo; VtxStride = vtxStride; UvOff = uvOff; IdxStride = idxStride; IndexCount = indexCount; Mvp = mvp;
                R = r; G = g; B = b; A = a; UseTexture = useTex; Texture = tex;
                ColorTarget = colorTarget; DepthOn = depthOn; UseSky = useSky; Program = program; Kind = kind;
                Model = model; View = view; Projection = projection; Orientation = orientation;
                VerticalOffset = verticalOffset; Unlit = unlit; HasTexture = hasTexture;
                LightIntensity = lightIntensity; AmbientStrength = ambientStrength;
                LightDir = lightDir; LightColor = lightColor; AmbientColor = ambientColor;
                HasOpacity = hasOpacity; OpacitySlots = opacitySlots; OpacityTex = opacityTex;
                VpX = vpX; VpY = vpY; VpW = vpW; VpH = vpH;
                ScX = scX; ScY = scY; ScW = scW; ScH = scH; ScissorOn = scissorOn; Mode = mode;
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
            public bool IsCubemap;
            public byte[][] Faces;
        }

        readonly List<DrawOp> _draws = new List<DrawOp>();
        readonly List<WorldDrawOp> _world = new List<WorldDrawOp>();
        readonly Dictionary<uint, VaoState> _vaos = new Dictionary<uint, VaoState>();
        readonly uint[] _texUnit = new uint[32];
        readonly Dictionary<uint, byte[]> _buffers = new Dictionary<uint, byte[]>();
        readonly Dictionary<uint, int> _bufGen = new Dictionary<uint, int>();
        readonly Dictionary<uint, uint> _fboColor = new Dictionary<uint, uint>();
        readonly Dictionary<uint, uint> _fboDepth = new Dictionary<uint, uint>();
        readonly Dictionary<uint, float[]> _fboClear = new Dictionary<uint, float[]>();
        readonly Dictionary<uint, CpuTexture> _textures = new Dictionary<uint, CpuTexture>();
        readonly Dictionary<uint, Dictionary<int, float[]>> _programUniforms = new Dictionary<uint, Dictionary<int, float[]>>();
        readonly Dictionary<string, int> _uniformNames = new Dictionary<string, int>(StringComparer.Ordinal);
        uint _nextBuffer = 1;
        uint _nextTexture = 1;
        uint _nextShader = 1;
        uint _nextProgram = 1;
        uint _boundArray;
        uint _boundElement;
        uint _boundTexture;
        uint _boundProgram;
        readonly Dictionary<uint, string> _shaderSource = new Dictionary<uint, string>();
        readonly Dictionary<uint, int> _shaderType = new Dictionary<uint, int>();
        readonly Dictionary<uint, int> _shaderStatus = new Dictionary<uint, int>();
        readonly Dictionary<uint, string> _shaderLog = new Dictionary<uint, string>();
        readonly Dictionary<uint, List<uint>> _programShaders = new Dictionary<uint, List<uint>>();
        readonly Dictionary<uint, string> _programVs = new Dictionary<uint, string>();
        readonly Dictionary<uint, string> _programFs = new Dictionary<uint, string>();
        readonly Dictionary<uint, int> _programKind = new Dictionary<uint, int>();
        readonly Dictionary<uint, int> _programStatus = new Dictionary<uint, int>();
        readonly Dictionary<uint, string> _programLog = new Dictionary<uint, string>();
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
        int _presentX, _presentY, _presentW, _presentH;

        public float ClearR, ClearG, ClearB, ClearA = 1f;
        public uint FrameShadowAtlas;
        public float FrameShadowsEnabled;
        public float[] FrameCascadeVP;
        public float FrameCascadeCount;
        public float[] FrameBones;
        public float FrameHasBones;
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
            _presentX = 0; _presentY = 0; _presentW = width; _presentH = height;
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
        public bool IsCubemap(uint id) => _textures.TryGetValue(id, out var t) && t != null && t.IsCubemap;
        public uint FirstCubemap()
        {
            foreach (var kv in _textures)
            {
                if (kv.Value != null && kv.Value.IsCubemap && kv.Key != 0)
                    return kv.Key;
            }
            return 0;
        }
        public bool TryGetBuffer(uint id, out byte[] bytes) => _buffers.TryGetValue(id, out bytes);
        public int GetBufferGeneration(uint id) => _bufGen.TryGetValue(id, out int g) ? g : 0;
        public uint GetFramebufferColor(uint fbo) => fbo != 0 && _fboColor != null && _fboColor.TryGetValue(fbo, out uint c) ? c : 0;
        public uint GetFramebufferDepth(uint fbo) => fbo != 0 && _fboDepth != null && _fboDepth.TryGetValue(fbo, out uint d) ? d : 0;
        public bool TryTakeFramebufferClear(uint colorTex, out float r, out float g, out float b, out float a)
        {
            r = g = b = 0; a = 1;
            if (colorTex == 0 || !_fboClear.TryGetValue(colorTex, out var c) || c == null || c.Length < 4)
                return false;
            r = c[0]; g = c[1]; b = c[2]; a = c[3];
            _fboClear.Remove(colorTex);
            return true;
        }
        public bool TextureHasCpuPixels(uint id)
        {
            if (!_textures.TryGetValue(id, out var tex) || tex == null)
                return false;
            if (tex.IsCubemap && tex.Faces != null)
            {
                for (int f = 0; f < tex.Faces.Length; f++)
                {
                    var face = tex.Faces[f];
                    if (face == null) continue;
                    int n = Math.Min(face.Length, 64);
                    for (int i = 0; i < n; i++)
                        if (face[i] != 0) return true;
                }
                return false;
            }
            if (tex.Rgba == null) return false;
            int m = Math.Min(tex.Rgba.Length, 64);
            for (int i = 0; i < m; i++)
                if (tex.Rgba[i] != 0) return true;
            return false;
        }

        public void ClearColor(float red, float green, float blue, float alpha)
        {
            ClearR = red; ClearG = green; ClearB = blue; ClearA = alpha;
        }

        public void Clear(int mask)
        {
            if (_boundFbo == 0) return;
            uint color = GetFramebufferColor(_boundFbo);
            if (color == 0) return;
            _fboClear[color] = new[] { ClearR, ClearG, ClearB, ClearA };
        }
        public void Viewport(int x, int y, uint width, uint height)
        {
            ViewportX = x;
            ViewportY = y;
            ViewportWidth = Math.Max(1, (int)width);
            ViewportHeight = Math.Max(1, (int)height);
            if (_boundFbo == 0)
            {
                _presentX = ViewportX;
                _presentY = ViewportY;
                _presentW = ViewportWidth;
                _presentH = ViewportHeight;
            }
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
        public void BindFramebuffer(int target, uint framebuffer)
        {
            _boundFbo = framebuffer;
            if (framebuffer == 0)
            {
                ViewportX = _presentX;
                ViewportY = _presentY;
                ViewportWidth = Math.Max(1, _presentW);
                ViewportHeight = Math.Max(1, _presentH);
            }
        }
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
            _bufGen.TryGetValue(id, out int gen);
            _bufGen[id] = gen + 1;
        }

        public void BufferSubData(int target, int offset, uint size, void* data)
        {
            uint id = target == Enums.ElementArrayBuffer ? _boundElement : _boundArray;
            if (id == 0 || data == null) return;
            if (!_buffers.TryGetValue(id, out var bytes) || bytes == null) return;
            int len = Math.Min((int)size, bytes.Length - offset);
            if (len <= 0) return;
            Marshal.Copy((nint)data, bytes, offset, len);
            _bufGen.TryGetValue(id, out int gen);
            _bufGen[id] = gen + 1;
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
            if (RecordFullscreenBlit(count))
                return;
            RecordDraw(count, indexed: false, mode: mode);
        }
        public void DrawElements(int mode, uint count, int type, void* indices)
        {
            RecordDraw(count, indexed: true, mode: mode);
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

        bool RecordFullscreenBlit(uint count)
        {
            if (count != 3 || _boundFbo != 0) return false;
            uint tex = _texUnit[0];
            if (tex == 0) return false;
            if (_boundArray != 0 && _buffers.TryGetValue(_boundArray, out var have) && have != null && have.Length >= 8)
                return false;
            float[] packed = { -1f, -1f, 0f, 1f,  3f, -1f, 2f, 1f,  -1f, 3f, 0f, -1f };
            _draws.Add(new DrawOp(packed, packed.Length, 3, 1f, 1f, 1f, 1f, 1f, tex,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                ViewportX, ViewportY, ViewportWidth, ViewportHeight,
                _scX, _scY, _scW > 0 ? _scW : ViewportWidth, _scH > 0 ? _scH : ViewportHeight, _scissorOn, 4));
            return true;
        }

        void RecordDraw(uint indexCount, bool indexed, int mode = 4)
        {
            if (_boundVao != 0)
            {
                var vao = EnsureVao(_boundVao);
                if (vao.Array != 0) _boundArray = vao.Array;
                if (vao.Element != 0) _boundElement = vao.Element;
                if (vao.Stride[0] > 0) _stride = vao.Stride[0];
                if (vao.Size[0] > 0) _posSize = vao.Size[0];
                _posOff = vao.Offset[0];
                if (vao.Size[2] >= 2) _uvOff = vao.Offset[2];
            }
            if (!_buffers.TryGetValue(_boundArray, out var vb) || vb == null || vb.Length < 8) return;
            int stride = _stride <= 0 ? 16 : _stride;
            if (_posSize >= 3 || stride > 16)
            {
                RecordWorld(vb, stride, indexCount, indexed, mode);
                return;
            }
            if (_boundFbo != 0)
                return;
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
            float useRounded = GetUniform1(Loc("uUseRounded"));
            float borderWidth = GetUniform1(Loc("uBorderWidth"));
            GetUniform4(Loc("uBorderColor"), out float br, out float bg, out float bb, out float ba);
            GetUniform4(Loc("uBorderRadius"), out float rx, out float ry, out float rz, out float rw);
            GetUniform4(Loc("uRectSize"), out float rectW, out float rectH, out _, out _);
            // Text/atlas draws never author rounded-rect uniforms. Do not inherit them.
            if (useTex > 0.5f)
            {
                useRounded = 0f;
                borderWidth = 0f;
            }
            if (useTex < 0.5f && a <= 0f && !(useRounded > 0.5f || (borderWidth > 0f && ba > 0f)))
                return;
            _draws.Add(new DrawOp(packed, packed.Length, (uint)(packed.Length / 4), r, g, b, a, useTex, _boundTexture,
                useRounded, borderWidth, br, bg, bb, ba, rx, ry, rz, rw, rectW, rectH,
                ViewportX, ViewportY, ViewportWidth, ViewportHeight, _scX, _scY, _scW > 0 ? _scW : ViewportWidth, _scH > 0 ? _scH : ViewportHeight, _scissorOn, mode));
        }


        void RecordWorld(byte[] vb, int stride, uint indexCount, bool indexed, int mode = 4)
        {
            if (_boundArray == 0) return;
            bool isLine = mode == Enums.Lines || mode == 1 || mode == 3;
            if (!isLine && indexCount < 3) return;
            if (isLine && indexCount < 2) return;
            // Shadow / cube FBOs are square. Keep sky and the sun-atlas pass.
            int recKind = GetProgramKind(_boundProgram);
            if (recKind != 1 && recKind != 5 && _boundFbo != 0 && ViewportWidth == ViewportHeight && ViewportWidth >= 256)
                return;
            if (stride < 12) stride = 12;
            int uvOff = _uvOff;
            if (uvOff < 8)
                uvOff = stride == 36 ? 28 : (stride >= 32 ? 24 : (stride >= 28 ? 28 : 12));
            GetMatrix("uModel", out var model);
            GetMatrix("uView", out var view);
            GetMatrix("uProjection", out var proj);
            var mvpMat = model * view * proj;
            var mvp = new float[]
            {
                mvpMat.M11, mvpMat.M12, mvpMat.M13, mvpMat.M14,
                mvpMat.M21, mvpMat.M22, mvpMat.M23, mvpMat.M24,
                mvpMat.M31, mvpMat.M32, mvpMat.M33, mvpMat.M34,
                mvpMat.M41, mvpMat.M42, mvpMat.M43, mvpMat.M44
            };
            int idxStride = 4;
            if (_buffers.TryGetValue(_boundElement, out var ib) && ib != null && indexCount > 0 && ib.Length == (int)indexCount * 2)
                idxStride = 2;
            uint tex = _texUnit[0] != 0 ? _texUnit[0] : _boundTexture;
            float useTex = tex != 0 ? 1f : 0f;
            if (GetUniform1(Loc("uHasTexture")) == 0 && useTex > 0.5f && GetUniform1(Loc("uUseTexture")) == 0 && GetUniform1(Loc("uUseTex")) == 0)
            {
                // terrain sets uHasTexture; models bind albedo without that flag — keep tex
            }
            GetUniform4(Loc("uColor"), out float r, out float g, out float b, out float a);
            if (a <= 0f) { r = g = b = a = 1f; }
            int kind = GetProgramKind(_boundProgram);
            bool useSky = kind == 1;
            float hasTex = GetUniform1(Loc("uHasTexture"));
            if (hasTex == 0 && useTex > 0.5f) hasTex = 1f;
            float unlit = GetUniform1(Loc("uUnlit"));
            float vertical = GetUniform1(Loc("uVerticalOffset"));
            float lightIntensity = GetUniform1(Loc("uLightIntensity"));
            float ambientStrength = GetUniform1(Loc("uAmbientStrength"));
            GetUniform3(Loc("uLightDir"), out float ldx, out float ldy, out float ldz);
            GetUniform3(Loc("uLightColor"), out float lcx, out float lcy, out float lcz);
            GetUniform3(Loc("uAmbientColor"), out float acx, out float acy, out float acz);
            GetMatrix("uOrientation", out var orient);
            uint colorTarget = GetFramebufferColor(_boundFbo);
            int kindNow = GetProgramKind(_boundProgram);
            if (kindNow == 5 && colorTarget == 0)
            {
                uint depthTex = GetFramebufferDepth(_boundFbo);
                if (depthTex != 0) colorTarget = depthTex;
            }
            int vpX, vpY, vpW, vpH;
            if (colorTarget != 0)
            {
                vpX = ViewportX; vpY = ViewportY; vpW = ViewportWidth; vpH = ViewportHeight;
            }
            else
            {
                vpX = _presentW > 0 ? _presentX : ViewportX;
                vpY = _presentW > 0 ? _presentY : ViewportY;
                vpW = _presentW > 0 ? _presentW : ViewportWidth;
                vpH = _presentH > 0 ? _presentH : ViewportHeight;
            }
            float hasOpacity = GetUniform1(Loc("uHasOpacity"));
            float opacitySlots = GetUniform1(Loc("uOpacitySlots"));
            if (opacitySlots >= 15f) { opacitySlots = 0f; hasOpacity = 0f; }
            uint opacityTex = 0;
            int opacUnit = (int)GetUniform1(Loc("uOpacityMap"));
            if (opacUnit >= 0 && opacUnit < _texUnit.Length && _texUnit[opacUnit] != 0)
                opacityTex = _texUnit[opacUnit];
            else if (_texUnit.Length > 15 && _texUnit[15] != 0)
                opacityTex = _texUnit[15];
            if (_texUnit.Length > 12 && _texUnit[12] != 0)
                FrameShadowAtlas = _texUnit[12];
            if (GetUniform1(Loc("uShadowsEnabled")) > 0.5f) FrameShadowsEnabled = 1f;
            if (FrameCascadeVP == null || FrameCascadeVP.Length < 64)
                FrameCascadeVP = new float[64];
            bool anyCascade = false;
            var packedCascades = new float[64];
            for (int ci = 0; ci < 4; ci++)
            {
                GetMatrix("uCascadeVP[" + ci + "]", out var cvi);
                if (ci == 0 && cvi == System.Numerics.Matrix4x4.Identity)
                    GetMatrix("uCascadeVP", out cvi);
                var pm = PackMatrix(cvi);
                Array.Copy(pm, 0, packedCascades, ci * 16, 16);
                if (cvi != System.Numerics.Matrix4x4.Identity)
                    anyCascade = true;
            }
            if (anyCascade)
            {
                FrameCascadeVP = packedCascades;
                float cc = GetUniform1(Loc("uCascadeCount"));
                FrameCascadeCount = cc > 0.5f ? cc : 4f;
            }
            FrameHasBones = GetUniform1(Loc("uHasBones"));
            if (FrameHasBones > 0.5f)
            {
                var bones = new float[64 * 16];
                for (int bi = 0; bi < 64; bi++)
                {
                    GetMatrix("uBoneTransforms[" + bi + "]", out var bm);
                    var packed = PackMatrix(bm);
                    Array.Copy(packed, 0, bones, bi * 16, 16);
                }
                FrameBones = bones;
            }
            if (kind == 5)
            {
                GetMatrix("uLightVP", out var lightVp);
                view = lightVp;
                proj = lightVp;
            }
            _world.Add(new WorldDrawOp(_boundArray, _boundElement, stride, uvOff, idxStride, indexCount, mvp,
                r, g, b, a, useTex, tex, colorTarget, _depthOn, useSky,
                _boundProgram, kind,
                PackMatrix(model), PackMatrix(view), PackMatrix(proj), PackMatrix(orient),
                vertical, unlit, hasTex, lightIntensity, ambientStrength,
                new[] { ldx, ldy, ldz }, new[] { lcx, lcy, lcz }, new[] { acx, acy, acz },
                hasOpacity, opacitySlots, opacityTex,
                vpX, vpY, vpW, vpH,
                vpX, vpY, vpW, vpH, true, mode));
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

        static float[] PackMatrix(System.Numerics.Matrix4x4 m)
        {
            return new[]
            {
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44
            };
        }

        Dictionary<int, float[]> ActiveUniforms()
        {
            if (!_programUniforms.TryGetValue(_boundProgram, out var bag))
            {
                bag = new Dictionary<int, float[]>();
                _programUniforms[_boundProgram] = bag;
            }
            return bag;
        }

        void GetUniform3(int loc, out float x, out float y, out float z)
        {
            x = y = z = 0;
            if (loc < 0 || !ActiveUniforms().TryGetValue(loc, out var v) || v == null) return;
            if (v.Length > 0) x = v[0];
            if (v.Length > 1) y = v[1];
            if (v.Length > 2) z = v[2];
        }

        void GetMatrix(string name, out System.Numerics.Matrix4x4 m)
        {
            m = System.Numerics.Matrix4x4.Identity;
            int loc = Loc(name);
            if (loc < 0 || !ActiveUniforms().TryGetValue(loc, out var v) || v == null || v.Length < 16) return;
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
            if (texture != 0 && target == Enums.TextureCubeMap)
            {
                if (!_textures.TryGetValue(texture, out var ct) || ct == null)
                    ct = new CpuTexture();
                ct.IsCubemap = true;
                if (ct.Faces == null) ct.Faces = new byte[6][];
                _textures[texture] = ct;
            }
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
            int cubeBase = Enums.TextureCubeMapPositiveX;
            int face = target - cubeBase;
            bool cubeFace = cubeBase != 0 && face >= 0 && face < 6;
            bool cubeTarget = target == Enums.TextureCubeMap || cubeFace;
            if (cubeTarget)
            {
                if (prev != null && prev.IsCubemap && prev.Faces != null)
                {
                    tex.IsCubemap = true;
                    tex.Faces = prev.Faces;
                    if (prev.Width > 0) { tex.Width = prev.Width; tex.Height = prev.Height; }
                }
                else
                {
                    tex.IsCubemap = true;
                    tex.Faces = new byte[6][];
                }
                if (cubeFace)
                    tex.Faces[face] = tex.Rgba;
                else if (tex.Faces[0] == null)
                    tex.Faces[0] = tex.Rgba;
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

        public uint CreateProgram()
        {
            uint id = _nextProgram++;
            _programShaders[id] = new List<uint>();
            _programStatus[id] = 1;
            _programLog[id] = "";
            return id;
        }
        public uint CreateShader(int type)
        {
            uint id = _nextShader++;
            _shaderType[id] = type;
            _shaderStatus[id] = 0;
            _shaderLog[id] = "";
            return id;
        }
        public void ShaderSource(uint shader, string source)
        {
            _shaderSource[shader] = source ?? "";
        }
        [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
        static extern int D3DCompile(
            byte[] pSrcData, nint srcDataSize, string pSourceName, nint pDefines,
            nint pInclude, string pEntryPoint, string pTarget,
            uint flags1, uint flags2, out nint ppCode, out nint ppErrorMsgs);

        public void CompileShader(uint shader)
        {
            if (!_shaderSource.TryGetValue(shader, out string src) || string.IsNullOrEmpty(src))
            {
                _shaderStatus[shader] = 0;
                _shaderLog[shader] = "No shader source.";
                return;
            }
            bool glsl = src.IndexOf("#version", StringComparison.Ordinal) >= 0;
            bool hlsl = src.IndexOf("SV_POSITION", StringComparison.OrdinalIgnoreCase) >= 0
                || src.IndexOf("SV_TARGET", StringComparison.OrdinalIgnoreCase) >= 0
                || src.IndexOf("register(b", StringComparison.Ordinal) >= 0;
            if (glsl && !hlsl)
            {
                _shaderStatus[shader] = 1;
                _shaderLog[shader] = "";
                return;
            }
            bool isVs = _shaderType.TryGetValue(shader, out int ty) && ty == Enums.VertexShader;
            string entry = isVs ? "vs" : "ps";
            string target = isVs ? "vs_5_0" : "ps_5_0";
            byte[] bytes = Encoding.ASCII.GetBytes(src);
            int hr = D3DCompile(bytes, (nint)bytes.Length, "world.hlsl", nint.Zero, nint.Zero, entry, target, 0, 0, out nint blob, out nint err);
            if (hr < 0)
            {
                string msg = "";
                if (err != nint.Zero)
                {
                    try
                    {
                        var ptrFn = (BlobPtrFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(err, 3), typeof(BlobPtrFn));
                        var lenFn = (BlobLenFn)Marshal.GetDelegateForFunctionPointer(ComVtable.Slot(err, 4), typeof(BlobLenFn));
                        nint p = ptrFn(err);
                        ulong n = lenFn(err);
                        if (p != nint.Zero && n > 0) msg = Marshal.PtrToStringAnsi(p) ?? "";
                    }
                    catch { }
                    Marshal.Release(err);
                }
                if (blob != nint.Zero) Marshal.Release(blob);
                _shaderStatus[shader] = 0;
                _shaderLog[shader] = string.IsNullOrEmpty(msg) ? ("D3DCompile HRESULT " + hr.ToString("X8")) : msg;
                Console.Error.WriteLine("D3DCompile failed (" + entry + " " + target + "): " + _shaderLog[shader]);
                return;
            }
            if (err != nint.Zero) Marshal.Release(err);
            if (blob != nint.Zero) Marshal.Release(blob);
            _shaderStatus[shader] = 1;
            _shaderLog[shader] = "";
        }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate nint BlobPtrFn(nint self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate ulong BlobLenFn(nint self);
        public void GetShader(uint shader, int param, out int value)
        {
            if (param == Enums.CompileStatus)
            {
                value = _shaderStatus.TryGetValue(shader, out int s) ? s : 0;
                return;
            }
            value = 0;
        }
        public string GetShaderInfoLog(uint shader)
        {
            return _shaderLog.TryGetValue(shader, out string log) ? log ?? "" : "";
        }
        public void AttachShader(uint program, uint shader)
        {
            if (!_programShaders.TryGetValue(program, out var list) || list == null)
            {
                list = new List<uint>();
                _programShaders[program] = list;
            }
            if (!list.Contains(shader)) list.Add(shader);
        }
        public void DetachShader(uint program, uint shader)
        {
            if (_programShaders.TryGetValue(program, out var list) && list != null)
                list.Remove(shader);
        }
        public void LinkProgram(uint program)
        {
            string vs = "";
            string fs = "";
            if (_programShaders.TryGetValue(program, out var list) && list != null)
            {
                foreach (uint sid in list)
                {
                    if (!_shaderSource.TryGetValue(sid, out string src)) continue;
                    int type = _shaderType.TryGetValue(sid, out int t) ? t : 0;
                    if (type == Enums.VertexShader) vs = src;
                    else if (type == Enums.FragmentShader) fs = src;
                }
            }
            _programVs[program] = vs;
            _programFs[program] = fs;
            _programKind[program] = ClassifyProgram(vs, fs);
            _programStatus[program] = 1;
            _programLog[program] = "";
        }
        public void GetProgram(uint program, int prop, out int value)
        {
            if (prop == Enums.LinkStatus)
            {
                value = _programStatus.TryGetValue(program, out int s) ? s : 0;
                return;
            }
            value = 0;
        }
        public string GetProgramInfoLog(uint program)
        {
            return _programLog.TryGetValue(program, out string log) ? log ?? "" : "";
        }
        public void DeleteShader(uint shader)
        {
            _shaderSource.Remove(shader);
            _shaderType.Remove(shader);
            _shaderStatus.Remove(shader);
            _shaderLog.Remove(shader);
        }
        public void DeleteProgram(uint program)
        {
            _programShaders.Remove(program);
            _programVs.Remove(program);
            _programFs.Remove(program);
            _programKind.Remove(program);
            _programStatus.Remove(program);
            _programLog.Remove(program);
            _programUniforms.Remove(program);
        }
        public void UseProgram(uint program) { _boundProgram = program; }

        public bool TryGetProgramSources(uint program, out string vs, out string fs)
        {
            vs = _programVs.TryGetValue(program, out var v) ? v : "";
            fs = _programFs.TryGetValue(program, out var f) ? f : "";
            return !string.IsNullOrEmpty(vs) && !string.IsNullOrEmpty(fs);
        }
        public int GetProgramKind(uint program) => _programKind.TryGetValue(program, out int k) ? k : 0;

        static int ClassifyProgram(string vs, string fs)
        {
            string a = (vs ?? "") + (fs ?? "");
            if (a.IndexOf("uSkybox", StringComparison.Ordinal) >= 0 || a.IndexOf("uOrientation", StringComparison.Ordinal) >= 0
                || a.IndexOf("TextureCube", StringComparison.Ordinal) >= 0)
                return 1;
            if (a.IndexOf("uLightVP", StringComparison.Ordinal) >= 0)
                return 5;
            if (a.IndexOf("uAlbedoMap", StringComparison.Ordinal) >= 0 || a.IndexOf("BLENDWEIGHT", StringComparison.Ordinal) >= 0)
                return 2;
            if (a.IndexOf("uUnlit", StringComparison.Ordinal) >= 0)
                return 3;
            return 0;
        }
        public int GetUniformLocation(uint program, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            if (_uniformNames.TryGetValue(name, out int loc)) return loc;
            loc = _nextUniformLoc++;
            _uniformNames[name] = loc;
            return loc;
        }
        public void Uniform1(int location, float value) { ActiveUniforms()[location] = new[] { value }; }
        public void Uniform1(int location, int value) { ActiveUniforms()[location] = new[] { (float)value }; }
        public void Uniform2(int location, float x, float y) { ActiveUniforms()[location] = new[] { x, y }; }
        public void Uniform3(int location, float x, float y, float z) { ActiveUniforms()[location] = new[] { x, y, z }; }
        public void Uniform4(int location, float x, float y, float z, float w) { ActiveUniforms()[location] = new[] { x, y, z, w }; }
        public void UniformMatrix4(int location, uint count, bool transpose, float* value)
        {
            int n = (int)(count <= 0 ? 16 : count * 16);
            var m = new float[n];
            if (value != null) Marshal.Copy((nint)value, m, 0, n);
            ActiveUniforms()[location] = m;
        }
        public void UniformMatrix3(int location, uint count, bool transpose, float* value) { }
        public void GetProgramInterface(uint program, int programInterface, int pname, out int param) { param = 0; }
        public int GetProgramResourceLocation(uint program, int programInterface, string name) => GetUniformLocation(program, name);

        int Loc(string name) => _uniformNames.TryGetValue(name, out int loc) ? loc : -1;
        float GetUniform1(int loc)
        {
            if (loc < 0 || !ActiveUniforms().TryGetValue(loc, out var v) || v == null || v.Length == 0) return 0;
            return v[0];
        }
        void GetUniform4(int loc, out float x, out float y, out float z, out float w)
        {
            x = y = z = 0; w = 1;
            if (loc < 0 || !ActiveUniforms().TryGetValue(loc, out var v) || v == null) return;
            if (v.Length > 0) x = v[0];
            if (v.Length > 1) y = v[1];
            if (v.Length > 2) z = v[2];
            if (v.Length > 3) w = v[3];
        }

        public void GenFramebuffers(uint n, out uint framebuffers) { framebuffers = _nextFbo++; }
        public void DeleteFramebuffers(uint n, uint* framebuffers) { }
        public void FramebufferTexture2D(int target, int attachment, int textarget, uint texture, int level)
        {
            if (_boundFbo == 0 || texture == 0) return;
            if (attachment == Enums.DepthAttachment)
            {
                if (_fboDepth != null) _fboDepth[_boundFbo] = texture;
                if (_fboColor != null && !_fboColor.ContainsKey(_boundFbo))
                    _fboColor[_boundFbo] = texture;
                return;
            }
            if (attachment == Enums.ColorAttachment0 && _fboColor != null)
                _fboColor[_boundFbo] = texture;
        }
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
