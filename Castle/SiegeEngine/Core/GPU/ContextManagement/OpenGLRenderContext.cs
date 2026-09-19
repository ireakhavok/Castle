// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: OpenGLRenderContext.cs
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public unsafe class OpenGLRenderContext : IRenderContext
    {
        private readonly GL _gl;
        private readonly Glfw _glfw;
        private readonly AbstractRenderEnums _enums = new OpenGLEnums();
        private int _viewportWidth;
        private int _viewportHeight;
        private uint _generation;
        private readonly Dictionary<ulong, uint> _live = new Dictionary<ulong, uint>();
        private readonly Dictionary<uint, int> _pipelinePrimitive = new Dictionary<uint, int>();
        private readonly Dictionary<uint, VertexLayout> _pipelineLayout = new Dictionary<uint, VertexLayout>();
        private readonly Dictionary<uint, GpuRenderState> _pipelineState = new Dictionary<uint, GpuRenderState>();
        private uint _blitVao;
        private bool _blitVaoCreated;
        private readonly Dictionary<uint, uint> _pipelineVao = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, int> _textureTarget = new Dictionary<uint, int>();
        private readonly Dictionary<ulong, uint> _meshVao = new Dictionary<ulong, uint>();
        private readonly Dictionary<uint, uint> _rtFbo = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, GpuHandle> _rtDepth = new Dictionary<uint, GpuHandle>();
        private readonly Dictionary<uint, uint> _rtDepthRb = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, bool> _rtDepthOnly = new Dictionary<uint, bool>();
        private uint _meshFallbackVao;
        private readonly uint[] _uboSlots = new uint[8];
        private FrameCB _cachedFrame = new FrameCB { View = System.Numerics.Matrix4x4.Identity, Projection = System.Numerics.Matrix4x4.Identity };
        private ObjectCB _cachedObject = new ObjectCB { Model = System.Numerics.Matrix4x4.Identity, NormalMatrix = System.Numerics.Matrix4x4.Identity };
        private MaterialCB _cachedMaterial;
        private LightCB _cachedLight;
        private ShadowCB _cachedShadow;
        private UiCB _cachedUi;
        private PostCB _cachedPost;
        private bool _hasMaterial;
        private bool _hasLight;
        private bool _hasShadow;
        private bool _hasUi;
        private bool _hasPost;
        private GpuHandle _boundPipeline;
        private GpuHandle _boundRenderTarget;
        private readonly Dictionary<uint, int> _bufferTarget = new Dictionary<uint, int>();
        private static bool _layoutChecked;

        public AbstractRenderEnums Enums => _enums;
        public int ViewportWidth => _viewportWidth;
        public int ViewportHeight => _viewportHeight;
        public RenderBackend Backend => RenderBackend.OpenGL;

        public OpenGLRenderContext(Glfw glfw, GL gl)
        {
            _glfw = glfw ?? throw new ArgumentNullException(nameof(glfw));
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            if (_glfw.GetCurrentContext() == null)
                throw new InvalidOperationException("No current OpenGL context");
        }

        public uint GenVertexArray()
        {
            _gl.GenVertexArrays(1, out uint array);
            return array;
        }

        public void GenVertexArrays(uint n, out uint arrays) => _gl.GenVertexArrays(n, out arrays);

        public uint GenBuffer()
        {
            _gl.GenBuffers(1, out uint buffer);
            return buffer;
        }

        public void GenBuffers(uint n, out uint buffers) => _gl.GenBuffers(n, out buffers);

        public void BindVertexArray(uint array) => _gl.BindVertexArray(array);

        public void BindBuffer(int target, uint buffer) => _gl.BindBuffer((BufferTargetARB)target, buffer);

        public void BufferData(int target, uint size, void* data, int usage) =>
            _gl.BufferData((BufferTargetARB)target, size, data, (BufferUsageARB)usage);

        public void BufferSubData(int target, int offset, uint size, void* data) =>
            _gl.BufferSubData((BufferTargetARB)target, offset, size, data);

        public void EnableVertexAttribArray(uint index) => _gl.EnableVertexAttribArray(index);

        public void DisableVertexAttribArray(uint index) => _gl.DisableVertexAttribArray(index);

        public void VertexAttribPointer(uint index, int size, int type, bool normalized, uint stride, void* pointer) =>
            _gl.VertexAttribPointer(index, size, (VertexAttribPointerType)type, normalized, stride, pointer);

        public void VertexAttribIPointer(uint index, int size, int type, uint stride, void* pointer) =>
            _gl.VertexAttribIPointer(index, size, (VertexAttribIType)type, stride, pointer);

        public void DeleteVertexArray(uint array) => _gl.DeleteVertexArrays(1, ref array);

        public void DeleteBuffer(uint buffer) => _gl.DeleteBuffers(1, ref buffer);

        public void DeleteBuffers(uint n, uint* buffers) => _gl.DeleteBuffers(n, buffers);

        public void DrawArrays(int mode, int first, uint count) =>
            _gl.DrawArrays((PrimitiveType)mode, first, count);

        public void DrawElements(int mode, uint count, int type, void* indices) =>
            _gl.DrawElements((PrimitiveType)mode, count, (DrawElementsType)type, indices);

        public void Clear(int mask) => _gl.Clear((ClearBufferMask)mask);

        public void ClearColor(float red, float green, float blue, float alpha) =>
            _gl.ClearColor(red, green, blue, alpha);

        public void Viewport(int x, int y, uint width, uint height)
        {
            _viewportWidth = (int)width;
            _viewportHeight = (int)height;
            _gl.Viewport(x, y, width, height);
        }

        public void Enable(int cap) => _gl.Enable((EnableCap)cap);

        public void Disable(int cap) => _gl.Disable((EnableCap)cap);

        public void BlendFunc(int src, int dst) =>
            _gl.BlendFunc((BlendingFactor)src, (BlendingFactor)dst);

        public void DepthMask(bool mask) => _gl.DepthMask(mask);

        public void DepthFunc(int func) => _gl.DepthFunc((DepthFunction)func);

        public void ColorMask(bool r, bool g, bool b, bool a) => _gl.ColorMask(r, g, b, a);

        public void ActiveTexture(int unit) => _gl.ActiveTexture((TextureUnit)unit);

        public void BindFramebuffer(int target, uint framebuffer) =>
            _gl.BindFramebuffer((FramebufferTarget)target, framebuffer);

        public int CheckFramebufferStatus(int target) =>
            (int)_gl.CheckFramebufferStatus((FramebufferTarget)target);

        public void DrawBuffer(int mode) => _gl.DrawBuffer((DrawBufferMode)mode);

        public void ReadBuffer(int mode) => _gl.ReadBuffer((ReadBufferMode)mode);

        public void GenTextures(uint n, out uint textures) => _gl.GenTextures(n, out textures);

        public void BindTexture(int target, uint texture) =>
            _gl.BindTexture((TextureTarget)target, texture);

        public void TexImage2D(int target, int level, int internalformat, uint width, uint height, int border, int format, int type, void* pixels) =>
            _gl.TexImage2D((TextureTarget)target, level, (InternalFormat)internalformat, width, height, border, (GLEnum)format, (GLEnum)type, pixels);

        public void TexParameter(int target, int pname, int param) =>
            _gl.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);

        public void TexParameterf(int target, int pname, float param) =>
            _gl.TexParameter((TextureTarget)target, (TextureParameterName)pname, param);

        public void PixelStore(int pname, int param) => _gl.PixelStore((PixelStoreParameter)pname, param);

        public void DeleteTexture(uint texture) => _gl.DeleteTextures(1, ref texture);

        public void DeleteTextures(uint n, uint* textures) => _gl.DeleteTextures(n, textures);

        public uint CreateProgram() => _gl.CreateProgram();

        public uint CreateShader(int type) => _gl.CreateShader((ShaderType)type);

        public void ShaderSource(uint shader, string source) => _gl.ShaderSource(shader, source);

        public void CompileShader(uint shader) => _gl.CompileShader(shader);

        public void GetShader(uint shader, int param, out int value) =>
            _gl.GetShader(shader, (ShaderParameterName)param, out value);

        public string GetShaderInfoLog(uint shader) => _gl.GetShaderInfoLog(shader);

        public void AttachShader(uint program, uint shader) => _gl.AttachShader(program, shader);

        public void DetachShader(uint program, uint shader) => _gl.DetachShader(program, shader);

        public void LinkProgram(uint program) => _gl.LinkProgram(program);

        public void GetProgram(uint program, int prop, out int value) =>
            _gl.GetProgram(program, (ProgramPropertyARB)prop, out value);

        public string GetProgramInfoLog(uint program) => _gl.GetProgramInfoLog(program);

        public void DeleteShader(uint shader) => _gl.DeleteShader(shader);

        public void DeleteProgram(uint program) => _gl.DeleteProgram(program);

        public void UseProgram(uint program) => _gl.UseProgram(program);

        public int GetUniformLocation(uint program, string name) =>
            _gl.GetUniformLocation(program, name);

        public void Uniform1(int location, float value) => _gl.Uniform1(location, value);

        public void Uniform1(int location, int value) => _gl.Uniform1(location, value);

        public void Uniform2(int location, float x, float y) => _gl.Uniform2(location, x, y);

        public void Uniform3(int location, float x, float y, float z) =>
            _gl.Uniform3(location, x, y, z);

        public void Uniform4(int location, float x, float y, float z, float w) =>
            _gl.Uniform4(location, x, y, z, w);

        public void UniformMatrix4(int location, uint count, bool transpose, float* value) =>
            _gl.UniformMatrix4(location, count, transpose, value);

        public void UniformMatrix3(int location, uint count, bool transpose, float* value) =>
            _gl.UniformMatrix3(location, count, transpose, value);

        public int GetError() => (int)_gl.GetError();

        public bool IsVertexArray(uint array) => _gl.IsVertexArray(array);

        public bool IsBuffer(uint buffer) => _gl.IsBuffer(buffer);

        public bool IsTexture(uint texture) => _gl.IsTexture(texture);

        public void GenerateMipmap(int target) => _gl.GenerateMipmap((TextureTarget)target);

        public bool IsExtensionPresent(string extension) => _gl.IsExtensionPresent(extension);

        public void GetFloat(int pname, out float param) =>
            _gl.GetFloat((GetPName)pname, out param);

        public void Scissor(int x, int y, uint width, uint height) => _gl.Scissor(x, y, width, height);

        public void CullFace(int mode) => _gl.CullFace((GLEnum)mode);

        public void FrontFace(int mode) =>
            _gl.FrontFace((FrontFaceDirection)mode);

        public void LineWidth(float width) => _gl.LineWidth(width);

        public void GenFramebuffers(uint n, out uint framebuffers) =>
            _gl.GenFramebuffers(n, out framebuffers);

        public void DeleteFramebuffers(uint n, uint* framebuffers) =>
            _gl.DeleteFramebuffers(n, framebuffers);

        public void FramebufferTexture2D(int target, int attachment, int textarget, uint texture, int level) =>
            _gl.FramebufferTexture2D((FramebufferTarget)target, (FramebufferAttachment)attachment, (TextureTarget)textarget, texture, level);

        public void GenRenderbuffers(uint n, out uint renderbuffers) =>
            _gl.GenRenderbuffers(n, out renderbuffers);

        public void DeleteRenderbuffers(uint n, uint* renderbuffers) =>
            _gl.DeleteRenderbuffers(n, renderbuffers);

        public void BindRenderbuffer(int target, uint renderbuffer) =>
            _gl.BindRenderbuffer((RenderbufferTarget)target, renderbuffer);

        public void RenderbufferStorage(int target, int internalformat, uint width, uint height) =>
            _gl.RenderbufferStorage((RenderbufferTarget)target, (InternalFormat)internalformat, width, height);

        public void FramebufferRenderbuffer(int target, int attachment, int renderbuffertarget, uint renderbuffer) =>
            _gl.FramebufferRenderbuffer((FramebufferTarget)target, (FramebufferAttachment)attachment, (RenderbufferTarget)renderbuffertarget, renderbuffer);

        public void ReadPixels(int x, int y, uint width, uint height, int format, int type, void* data)
        {
            if (data != null)
                BindBuffer(_enums.PixelPackBuffer, 0);
            _gl.ReadPixels(x, y, width, height, (PixelFormat)format, (PixelType)type, data);
        }

        public void ClearBufferuiv(int buffer, int drawbuffer, uint* value) =>
            _gl.ClearBuffer((GLEnum)buffer, drawbuffer, value);

        public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ) =>
            _gl.DispatchCompute(numGroupsX, numGroupsY, numGroupsZ);

        public void MemoryBarrier(int barriers) =>
            _gl.MemoryBarrier((MemoryBarrierMask)barriers);

        public void BindBufferBase(int target, uint index, uint buffer) =>
            _gl.BindBufferBase((BufferTargetARB)target, index, buffer);

        public void BindBufferRange(int target, uint index, uint buffer, int offset, uint size) =>
            _gl.BindBufferRange((BufferTargetARB)target, index, buffer, offset, size);

        public void* MapBuffer(int target, int access) =>
            _gl.MapBuffer((BufferTargetARB)target, (BufferAccessARB)access);

        public void* MapBufferRange(int target, int offset, uint length, int access) =>
            _gl.MapBufferRange((BufferTargetARB)target, offset, length, (MapBufferAccessMask)access);

        public bool UnmapBuffer(int target) =>
            _gl.UnmapBuffer((BufferTargetARB)target);

        public void BindStorageBuffer(GpuHandle buffer, int slot)
        {
            if (!buffer.IsValid)
            {
                BindBufferBase(_enums.ShaderStorageBuffer, (uint)slot, 0);
                return;
            }
            BindBufferBase(_enums.ShaderStorageBuffer, (uint)slot, buffer.Id);
        }

        public void BindUniformBuffer(GpuHandle buffer, int slot)
        {
            if (!buffer.IsValid)
            {
                BindBufferBase(_enums.UniformBuffer, (uint)slot, 0);
                return;
            }
            BindBufferBase(_enums.UniformBuffer, (uint)slot, buffer.Id);
        }

        public void* Map(GpuHandle buffer, MapAccess access)
        {
            BindBuffer(buffer);
            return MapBuffer(TargetOf(buffer), ToGlAccess(access));
        }

        public void* Map(GpuHandle buffer, int offset, uint length, MapAccess access)
        {
            BindBuffer(buffer);
            return MapBufferRange(TargetOf(buffer), offset, length, ToGlMapMask(access));
        }

        public void Unmap(GpuHandle buffer)
        {
            BindBuffer(buffer);
            UnmapBuffer(TargetOf(buffer));
        }

        int TargetOf(GpuHandle buffer)
        {
            if (buffer.IsValid && _bufferTarget.TryGetValue(buffer.Id, out int target) && target != 0)
                return target;
            return _enums.ShaderStorageBuffer;
        }

        static int ToGlAccess(MapAccess access)
        {
            if (access == MapAccess.Read) return (int)BufferAccessARB.ReadOnly;
            if (access == MapAccess.Write) return (int)BufferAccessARB.WriteOnly;
            return (int)BufferAccessARB.ReadWrite;
        }

        int ToGlMapMask(MapAccess access)
        {
            if (access == MapAccess.Read) return _enums.MapReadBit;
            if (access == MapAccess.Write) return _enums.MapWriteBit;
            return _enums.MapReadBit | _enums.MapWriteBit;
        }


        public void GetInteger(int pname, out int data) =>
            _gl.GetInteger((GetPName)pname, out data);

        public void GetInteger(int pname, int* data) =>
            _gl.GetInteger((GetPName)pname, data);

        public void GetProgramInterface(uint program, int programInterface, int pname, out int param)
        {
            _gl.GetProgramInterface(program, (ProgramInterface)programInterface, (ProgramInterfacePName)pname, out param);
        }

        public int GetProgramResourceLocation(uint program, int programInterface, string name)
        {
            return _gl.GetProgramResourceLocation(program, (ProgramInterface)programInterface, name);
        }

        public nint FenceSync(int condition, uint flags)
        {
            return _gl.FenceSync((SyncCondition)condition, (SyncBehaviorFlags)flags);
        }

        public int ClientWaitSync(nint sync, uint flags, ulong timeout)
        {
            if (sync == 0)
                return _enums.AlreadySignaled;
            return (int)_gl.ClientWaitSync(sync, flags, timeout);
        }

        public void DeleteSync(nint sync)
        {
            if (sync == 0)
                return;
            _gl.DeleteSync(sync);
        }

        public GpuHandle CreatePipeline(in PipelineDesc desc)
        {
            string vertex = desc.VertexSource;
            string fragment = desc.FragmentSource;
            string compute = desc.ComputeSource;
            if (string.IsNullOrEmpty(vertex) && string.IsNullOrEmpty(compute))
            {
                ShaderSourceSet src = ShaderCatalog.Get(desc.ShaderId, RenderBackend.OpenGL);
                vertex = src.Vertex;
                fragment = src.Fragment;
                compute = src.Compute;
            }

            uint program;
            if (!string.IsNullOrEmpty(compute) && string.IsNullOrEmpty(vertex))
            {
                program = LinkStages(new[] { CompileStage(_enums.ComputeShader, compute) });
            }
            else
            {
                if (string.IsNullOrEmpty(vertex) || string.IsNullOrEmpty(fragment))
                    throw new ArgumentException("PipelineDesc requires vertex and fragment sources.");
                program = LinkStages(new[]
                {
                    CompileStage(_enums.VertexShader, vertex),
                    CompileStage(_enums.FragmentShader, fragment)
                });
            }

            GpuHandle handle = Track(GpuResourceKind.Pipeline, program);
            _pipelinePrimitive[program] = desc.State.Primitive;
            _pipelineLayout[program] = desc.Layout;
            _pipelineState[program] = desc.State;
            uint vao = GenVertexArray();
            _pipelineVao[program] = vao;
            BindUniformBlocks(program);
            BindSamplerUnits(program);
            return handle;
        }

        public GpuHandle CreateBuffer(in BufferDesc desc)
        {
            uint buffer = GenBuffer();
            int target = desc.Target != 0 ? desc.Target : _enums.ArrayBuffer;
            _bufferTarget[buffer] = target;
            if (desc.ByteSize > 0)
            {
                int usage = desc.Usage != 0 ? desc.Usage : _enums.StaticDraw;
                BindBuffer(target, buffer);
                BufferData(target, (uint)desc.ByteSize, null, usage);
            }
            return Track(GpuResourceKind.Buffer, buffer);
        }

        public GpuHandle CreateTexture(in TextureDesc desc)
        {
            GenTextures(1, out uint texture);
            int target = desc.Target != 0 ? desc.Target : _enums.Texture2D;
            if (desc.Width > 0 && desc.Height > 0)
            {
                int internalFormat = desc.InternalFormat != 0 ? desc.InternalFormat : _enums.InternalRgba;
                bool isDepth = internalFormat == _enums.DepthComponent || internalFormat == _enums.DepthComponent24;
                bool isCube = target == _enums.TextureCubeMap;
                bool isHdr = internalFormat == _enums.InternalRgba16f;
                bool isInteger = internalFormat == _enums.R32UI;
                int uploadFormat = isDepth ? _enums.DepthComponent : (isInteger ? _enums.RedInteger : _enums.PixelRgba);
                int uploadType = isDepth || isInteger ? _enums.UnsignedInt : (isHdr ? _enums.Float : _enums.UnsignedByte);
                if (isCube)
                {
                    BindTexture(_enums.TextureCubeMap, texture);
                    for (int face = 0; face < 6; face++)
                        TexImage2D(_enums.TextureCubeMapPositiveX + face, 0, internalFormat, (uint)desc.Width, (uint)desc.Height, 0, uploadFormat, uploadType, null);
                }
                else
                {
                    BindTexture(target, texture);
                    TexImage2D(target, 0, internalFormat, (uint)desc.Width, (uint)desc.Height, 0, uploadFormat, uploadType, null);
                    if (isInteger)
                    {
                        TexParameter(target, _enums.TextureMinFilter, _enums.Nearest);
                        TexParameter(target, _enums.TextureMagFilter, _enums.Nearest);
                    }
                }
            }
            GpuHandle handle = Track(GpuResourceKind.Texture, texture);
            _textureTarget[texture] = target;
            return handle;
        }

        public GpuHandle ImportTexture(uint id, int target)
        {
            if (id == 0)
                return default;
            if (target != 0)
                _textureTarget[id] = target;
            ulong key = Pack(GpuResourceKind.Texture, id);
            if (_live.TryGetValue(key, out uint gen))
                return new GpuHandle(id, gen, GpuResourceKind.Texture);
            return Track(GpuResourceKind.Texture, id);
        }

        public void Destroy(GpuHandle handle)
        {
            if (!handle.IsValid)
                return;
            if (_rtFbo.TryGetValue(handle.Id, out uint fbo))
            {
                if (_rtDepth.TryGetValue(handle.Id, out GpuHandle depth) && depth.IsValid && depth.Id != handle.Id)
                {
                    _rtDepth.Remove(handle.Id);
                    if (IsLive(depth))
                    {
                        _live.Remove(Pack(depth.Kind, depth.Id));
                        _textureTarget.Remove(depth.Id);
                        DeleteTexture(depth.Id);
                    }
                }
                else
                    _rtDepth.Remove(handle.Id);
                if (_rtDepthRb.TryGetValue(fbo, out uint rb) && rb != 0)
                {
                    DeleteRenderbuffers(1, &rb);
                    _rtDepthRb.Remove(fbo);
                }
                DeleteFramebuffers(1, &fbo);
                _rtFbo.Remove(handle.Id);
                _rtDepthOnly.Remove(handle.Id);
            }
            if (!IsLive(handle))
                return;
            _live.Remove(Pack(handle.Kind, handle.Id));
            if (handle.Kind == GpuResourceKind.Pipeline)
            {
                _pipelinePrimitive.Remove(handle.Id);
                _pipelineLayout.Remove(handle.Id);
                _pipelineState.Remove(handle.Id);
                if (_pipelineVao.TryGetValue(handle.Id, out uint vao))
                {
                    _pipelineVao.Remove(handle.Id);
                    DeleteVertexArray(vao);
                }
                DeleteProgram(handle.Id);
            }
            else if (handle.Kind == GpuResourceKind.Buffer)
            {
                _bufferTarget.Remove(handle.Id);
                DeleteBuffer(handle.Id);
            }
            else if (handle.Kind == GpuResourceKind.Texture)
            {
                _textureTarget.Remove(handle.Id);
                DeleteTexture(handle.Id);
            }
        }

        public void BindPipeline(GpuHandle pipeline)
        {
            if (!IsLive(pipeline) || pipeline.Kind != GpuResourceKind.Pipeline)
                return;
            UseProgram(pipeline.Id);
            _boundPipeline = pipeline;
            if (_pipelineState.TryGetValue(pipeline.Id, out GpuRenderState state))
                ApplyState(state);
            BindUniformBlocks(pipeline.Id);
        }

        public void BindUniformBlock(uint program, string blockName, int slot)
        {
            if (program == 0 || string.IsNullOrEmpty(blockName))
                return;
            BindBlock(program, blockName, slot);
        }

        public void BindUniformBlock(int slot)
        {
            if (_boundPipeline.Id == 0)
                return;
            BindUniformBlock(_boundPipeline.Id, ConstantSlot.BlockName(slot), slot);
        }

        public void BindUniformBlocks()
        {
            if (_boundPipeline.Id != 0)
                BindUniformBlocks(_boundPipeline.Id);
        }

        public void BindUniformBlocks(uint program)
        {
            if (program == 0)
                return;
            for (int slot = 0; slot < ConstantSlot.Count; slot++)
                BindBlock(program, ConstantSlot.BlockName(slot), slot);
        }

        public void BindVertexBuffer(GpuHandle buffer, int slot, int stride, int offset)
        {
            if (!IsLive(buffer) || buffer.Kind != GpuResourceKind.Buffer)
                return;
            if (_boundPipeline.IsValid && _pipelineVao.TryGetValue(_boundPipeline.Id, out uint vao))
                BindVertexArray(vao);
            BindBuffer(_enums.ArrayBuffer, buffer.Id);
            if (_boundPipeline.IsValid && _pipelineLayout.TryGetValue(_boundPipeline.Id, out VertexLayout layout) && layout != null)
            {
            int strideUse = stride > 0 ? stride : layout.Stride;
            for (int i = 0; i < layout.Attributes.Length; i++)
            {
                VertexAttribute attr = layout.Attributes[i];
                if (attr.Slot != slot)
                    continue;
                int loc = VertexLayout.Location(attr.Semantic);
                EnableVertexAttribArray((uint)loc);
                int type = attr.Type != 0 ? attr.Type : _enums.Float;
                if (type == _enums.Int)
                    VertexAttribIPointer((uint)loc, attr.Size, type, (uint)strideUse, (void*)(offset + attr.Offset));
                else
                    VertexAttribPointer((uint)loc, attr.Size, type, false, (uint)strideUse, (void*)(offset + attr.Offset));
            }
            }
            else
                ApplyStrideAttribs(stride);
        }

        public void BindIndexBuffer(GpuHandle buffer)
        {
            if (!IsLive(buffer) || buffer.Kind != GpuResourceKind.Buffer)
                return;
            BindBuffer(_enums.ElementArrayBuffer, buffer.Id);
        }

        public void BindTextureSlot(int slot, GpuHandle texture)
        {
            BindTextureSlot(slot, texture, null);
        }

        public void BindTextureSlot(int slot, GpuHandle texture, string samplerName)
        {
            if (texture.Kind == GpuResourceKind.RenderTarget && texture.IsValid)
            {
                GpuHandle color = GetRenderTargetColor(texture);
                if (color.IsValid && color.Kind == GpuResourceKind.Texture)
                    texture = color;
            }
            if (!texture.IsValid || !IsLive(texture) || texture.Kind != GpuResourceKind.Texture)
            {
                ActiveTexture(_enums.Texture0 + slot);
                BindTexture(_enums.Texture2D, 0);
            }
            else
            {
                ActiveTexture(_enums.Texture0 + slot);
                int target = _enums.Texture2D;
                if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                    target = stored;
                BindTexture(target, texture.Id);
            }
            if (!string.IsNullOrEmpty(samplerName) && _boundPipeline.IsValid)
            {
                int loc = GetUniformLocation(_boundPipeline.Id, samplerName);
                if (loc >= 0)
                    Uniform1(loc, slot);
            }
        }

        public void UpdateBuffer(GpuHandle buffer, ReadOnlySpan<byte> data, int offset = 0)
        {
            if (!IsLive(buffer) || buffer.Kind != GpuResourceKind.Buffer)
                return;
            int target = _enums.ArrayBuffer;
            if (_bufferTarget.TryGetValue(buffer.Id, out int stored) && stored != 0)
                target = stored;
            BindBuffer(target, buffer.Id);
            if (data.Length == 0)
                return;
            fixed (byte* ptr = data)
            {
                if (offset == 0)
                    BufferData(target, (uint)data.Length, ptr, _enums.DynamicDraw);
                else
                    BufferSubData(target, offset, (uint)data.Length, ptr);
            }
        }


        void ApplyStrideAttribs(int stride)
        {
            int useStride = stride > 0 ? stride : 28;
            if (useStride == 8)
            {
                EnableVertexAttribArray(0);
                VertexAttribPointer(0, 2, _enums.Float, false, 8, (void*)0);
                return;
            }
            if (useStride == 16)
            {
                EnableVertexAttribArray(0);
                VertexAttribPointer(0, 2, _enums.Float, false, 16, (void*)0);
                EnableVertexAttribArray(1);
                VertexAttribPointer(1, 2, _enums.Float, false, 16, (void*)(2 * sizeof(float)));
                return;
            }
            if (useStride == 12)
            {
                EnableVertexAttribArray(0);
                VertexAttribPointer(0, 3, _enums.Float, false, 12, (void*)0);
                return;
            }
            EnableVertexAttribArray(0);
            VertexAttribPointer(0, 3, _enums.Float, false, (uint)useStride, (void*)0);
            if (useStride == 80)
            {
                EnableVertexAttribArray(3);
                VertexAttribPointer(3, 3, _enums.Float, false, 80, (void*)(3 * sizeof(float)));
                EnableVertexAttribArray(2);
                VertexAttribPointer(2, 2, _enums.Float, false, 80, (void*)(6 * sizeof(float)));
                EnableVertexAttribArray(4);
                VertexAttribPointer(4, 1, _enums.Float, false, 80, (void*)(8 * sizeof(float)));
                EnableVertexAttribArray(5);
                VertexAttribPointer(5, 3, _enums.Float, false, 80, (void*)(9 * sizeof(float)));
                EnableVertexAttribArray(6);
                VertexAttribPointer(6, 4, _enums.Float, false, 80, (void*)(12 * sizeof(float)));
                EnableVertexAttribArray(7);
                VertexAttribPointer(7, 4, _enums.Float, false, 80, (void*)(16 * sizeof(float)));
                return;
            }
            if (useStride >= 28)
            {
                EnableVertexAttribArray(1);
                VertexAttribPointer(1, 4, _enums.Float, false, (uint)useStride, (void*)(3 * sizeof(float)));
            }
            if (useStride >= 36)
            {
                EnableVertexAttribArray(2);
                VertexAttribPointer(2, 2, _enums.Float, false, (uint)useStride, (void*)(7 * sizeof(float)));
            }
        }

        public void BindMesh(GpuHandle vertex, GpuHandle index, int stride)
        {
            if (!IsLive(vertex) || vertex.Kind != GpuResourceKind.Buffer)
                return;
            ulong key = ((ulong)vertex.Id << 32) | (index.IsValid ? index.Id : 0u);
            if (!_meshVao.TryGetValue(key, out uint vao))
            {
                vao = GenVertexArray();
                _meshVao[key] = vao;
            }
            BindVertexArray(vao);
            BindBuffer(_enums.ArrayBuffer, vertex.Id);
            ApplyStrideAttribs(stride);
            if (index.IsValid && IsLive(index))
                BindBuffer(_enums.ElementArrayBuffer, index.Id);
        }

        public void BindMesh(GpuHandle vertex, GpuHandle index, VertexLayout layout)
        {
            if (!IsLive(vertex) || vertex.Kind != GpuResourceKind.Buffer)
                return;
            ulong key = ((ulong)vertex.Id << 32) | (index.IsValid ? index.Id : 0u);
            if (!_meshVao.TryGetValue(key, out uint vao))
            {
                vao = GenVertexArray();
                _meshVao[key] = vao;
            }
            BindVertexArray(vao);
            BindBuffer(_enums.ArrayBuffer, vertex.Id);
            if (layout != null && layout.Attributes != null)
            {
                int strideUse = layout.Stride > 0 ? layout.Stride : 12;
                for (int i = 0; i < layout.Attributes.Length; i++)
                {
                    VertexAttribute attr = layout.Attributes[i];
                    int loc = VertexLayout.Location(attr.Semantic);
                    EnableVertexAttribArray((uint)loc);
                    int type = attr.Type != 0 ? attr.Type : _enums.Float;
                    if (type == _enums.Int)
                        VertexAttribIPointer((uint)loc, attr.Size, type, (uint)strideUse, (void*)attr.Offset);
                    else
                        VertexAttribPointer((uint)loc, attr.Size, type, false, (uint)strideUse, (void*)attr.Offset);
                }
            }
            if (index.IsValid && IsLive(index))
                BindBuffer(_enums.ElementArrayBuffer, index.Id);
        }

        public void BindBuffer(GpuHandle buffer)
        {
            if (!buffer.IsValid)
                return;
            int target = _enums.ArrayBuffer;
            if (_bufferTarget.TryGetValue(buffer.Id, out int stored) && stored != 0)
                target = stored;
            BindBuffer(target, buffer.Id);
        }

        public void UnbindBuffer(int target)
        {
            if (target != 0)
                BindBuffer(target, 0);
        }

        public void SetUniform(string name, int value)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name))
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc >= 0)
                Uniform1(loc, value);
        }

        public void SetUniform(string name, float value)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name))
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc >= 0)
                Uniform1(loc, value);
        }

        public void SetUniform(string name, float x, float y)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name))
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc >= 0)
                Uniform2(loc, x, y);
        }

        public void SetUniform(string name, float x, float y, float z)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name))
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc >= 0)
                Uniform3(loc, x, y, z);
        }

        public void SetUniform(string name, float x, float y, float z, float w)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name))
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc >= 0)
                Uniform4(loc, x, y, z, w);
        }

        public unsafe void SetUniformMatrix4(string name, in Matrix4x4 matrix)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name))
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc < 0)
                return;
            float* m = stackalloc float[16];
            m[0] = matrix.M11; m[1] = matrix.M12; m[2] = matrix.M13; m[3] = matrix.M14;
            m[4] = matrix.M21; m[5] = matrix.M22; m[6] = matrix.M23; m[7] = matrix.M24;
            m[8] = matrix.M31; m[9] = matrix.M32; m[10] = matrix.M33; m[11] = matrix.M34;
            m[12] = matrix.M41; m[13] = matrix.M42; m[14] = matrix.M43; m[15] = matrix.M44;
            UniformMatrix4(loc, 1, false, m);
        }

        public void UpdateTexture(GpuHandle texture, int width, int height, int format, int type, void* pixels)
        {
            if (!IsLive(texture) || texture.Kind != GpuResourceKind.Texture)
                return;
            int target = _enums.Texture2D;
            if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                target = stored;
            BindTexture(target, texture.Id);
            PixelStore(_enums.UnpackAlignment, 1);
            int internalFormat = _enums.InternalRgba;
            TexImage2D(target, 0, internalFormat, (uint)width, (uint)height, 0, format, type, pixels);
        }

        public void SetTextureParams(GpuHandle texture, int minFilter, int magFilter, int wrapS, int wrapT)
        {
            if (!IsLive(texture) || texture.Kind != GpuResourceKind.Texture)
                return;
            int target = _enums.Texture2D;
            if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                target = stored;
            BindTexture(target, texture.Id);
            if (minFilter != 0)
                TexParameter(target, _enums.TextureMinFilter, minFilter);
            if (magFilter != 0)
                TexParameter(target, _enums.TextureMagFilter, magFilter);
            if (wrapS != 0)
                TexParameter(target, _enums.TextureWrapS, wrapS);
            if (wrapT != 0)
                TexParameter(target, _enums.TextureWrapT, wrapT);
        }

        public GpuHandle CreateRenderTarget(in RenderTargetDesc desc)
        {
            GenFramebuffers(1, out uint fbo);
            BindFramebuffer(_enums.Framebuffer, fbo);
            GpuHandle sample = default;
            GpuHandle depth = default;
            bool depthOnly = desc.ColorTarget == 0 && desc.ColorFormat == 0 && (desc.DepthTexture || desc.DepthFormat != 0 || desc.Faces > 0);
            int w = desc.Width;
            int h = desc.Height;

            if (desc.ColorTarget != 0)
            {
                sample = ImportTexture((uint)desc.ColorTarget, _enums.Texture2D);
                FramebufferTexture2D(_enums.Framebuffer, _enums.ColorAttachment0, _enums.Texture2D, sample.Id, 0);
                DrawBuffer(_enums.ColorAttachment0);
            }
            else if (!depthOnly && w > 0 && h > 0)
            {
                sample = CreateTexture(new TextureDesc
                {
                    Target = _enums.Texture2D,
                    InternalFormat = desc.ColorFormat != 0 ? desc.ColorFormat : _enums.InternalRgba,
                    Width = w,
                    Height = h
                });
                FramebufferTexture2D(_enums.Framebuffer, _enums.ColorAttachment0, _enums.Texture2D, sample.Id, 0);
                DrawBuffer(_enums.ColorAttachment0);
            }
            else
            {
                DrawBuffer(_enums.None);
                ReadBuffer(_enums.None);
            }

            if (desc.DepthTarget != 0)
            {
                int dt = desc.Faces > 0 ? _enums.TextureCubeMap : _enums.Texture2D;
                depth = ImportTexture((uint)desc.DepthTarget, dt);
                int attach = desc.Faces > 0 ? _enums.TextureCubeMapPositiveX : _enums.Texture2D;
                FramebufferTexture2D(_enums.Framebuffer, _enums.DepthAttachment, attach, depth.Id, 0);
                if (!sample.IsValid)
                    sample = depth;
            }
            else if (desc.DepthTexture && w > 0 && h > 0)
            {
                int texTarget = desc.Faces > 0 ? _enums.TextureCubeMap : _enums.Texture2D;
                depth = CreateTexture(new TextureDesc
                {
                    Target = texTarget,
                    InternalFormat = desc.DepthFormat != 0 ? desc.DepthFormat : _enums.DepthComponent24,
                    Width = w,
                    Height = h
                });
                int attach = desc.Faces > 0 ? _enums.TextureCubeMapPositiveX : texTarget;
                FramebufferTexture2D(_enums.Framebuffer, _enums.DepthAttachment, attach, depth.Id, 0);
                if (!sample.IsValid)
                    sample = depth;
            }
            else if (desc.DepthFormat != 0 && w > 0 && h > 0)
            {
                uint rb;
                GenRenderbuffers(1, out rb);
                BindRenderbuffer(_enums.Renderbuffer, rb);
                RenderbufferStorage(_enums.Renderbuffer, desc.DepthFormat, (uint)w, (uint)h);
                FramebufferRenderbuffer(_enums.Framebuffer, _enums.DepthAttachment, _enums.Renderbuffer, rb);
                _rtDepthRb[fbo] = rb;
            }

            BindFramebuffer(_enums.Framebuffer, 0);
            if (!sample.IsValid)
                sample = Track(GpuResourceKind.Texture, fbo);
            _rtFbo[sample.Id] = fbo;
            if (depth.IsValid)
                _rtDepth[sample.Id] = depth;
            _rtDepthOnly[sample.Id] = depthOnly;
            return sample;
        }

        public GpuHandle GetRenderTargetColor(GpuHandle target)
        {
            if (!target.IsValid)
                return default;
            bool depthOnly;
            if (_rtDepthOnly.TryGetValue(target.Id, out depthOnly) && depthOnly)
                return default;
            return target;
        }

        public GpuHandle GetRenderTargetDepth(GpuHandle target)
        {
            if (target.IsValid && _rtDepth.TryGetValue(target.Id, out GpuHandle depth))
                return depth;
            bool depthOnly;
            if (target.IsValid && _rtDepthOnly.TryGetValue(target.Id, out depthOnly) && depthOnly)
                return target;
            return default;
        }

        public void BindRenderTargetFace(GpuHandle target, int face)
        {
            if (!target.IsValid)
                return;
            if (!_rtFbo.TryGetValue(target.Id, out uint fbo))
                return;
            GpuHandle depth = GetRenderTargetDepth(target);
            uint tex = depth.IsValid ? depth.Id : target.Id;
            BindFramebuffer(_enums.Framebuffer, fbo);
            _boundRenderTarget = target;
            FramebufferTexture2D(_enums.Framebuffer, _enums.DepthAttachment, _enums.TextureCubeMapPositiveX + face, tex, 0);
            DrawBuffer(_enums.None);
            ReadBuffer(_enums.None);
        }

        public void BindRenderTarget(GpuHandle target)
        {
            if (!target.IsValid)
            {
                BindFramebuffer(_enums.Framebuffer, 0);
                _boundRenderTarget = default;
                return;
            }
            _boundRenderTarget = target;
            uint fbo = target.Id;
            if (_rtFbo.TryGetValue(target.Id, out uint mapped))
                fbo = mapped;
            BindFramebuffer(_enums.Framebuffer, fbo);
            bool depthOnly;
            if (_rtDepthOnly.TryGetValue(target.Id, out depthOnly) && depthOnly)
            {
                DrawBuffer(_enums.None);
                ReadBuffer(_enums.None);
            }
            else if (_rtFbo.ContainsKey(target.Id))
            {
                DrawBuffer(_enums.ColorAttachment0);
                ReadBuffer(_enums.ColorAttachment0);
            }
        }

        public void BindDefaultRenderTarget()
        {
            BindFramebuffer(_enums.Framebuffer, 0);
            _boundRenderTarget = default;
        }

        public GpuHandle GetBoundRenderTarget()
        {
            return _boundRenderTarget;
        }

        public void SetConstants<T>(int slot, in T data) where T : unmanaged
        {
            if (slot < 0 || slot >= _uboSlots.Length)
                return;
            if (_uboSlots[slot] == 0)
                _uboSlots[slot] = GenBuffer();
            uint ubo = _uboSlots[slot];
            T local = data;
            uint size = (uint)sizeof(T);
            BindBuffer(_enums.UniformBuffer, ubo);
            BufferData(_enums.UniformBuffer, size, &local, _enums.DynamicDraw);
            BindBufferBase(_enums.UniformBuffer, (uint)slot, ubo);
            CacheConstants(slot, local);
        }

        public bool TryGetConstants<T>(int slot, out T data) where T : unmanaged
        {
            data = default;
            if (slot == ConstantSlot.Frame && sizeof(T) == sizeof(FrameCB))
            {
                FrameCB cached = _cachedFrame;
                data = *(T*)&cached;
                return true;
            }
            if (slot == ConstantSlot.Object && sizeof(T) == sizeof(ObjectCB))
            {
                ObjectCB cached = _cachedObject;
                data = *(T*)&cached;
                return true;
            }
            if (slot == ConstantSlot.Material && _hasMaterial && sizeof(T) == sizeof(MaterialCB))
            {
                MaterialCB cached = _cachedMaterial;
                data = *(T*)&cached;
                return true;
            }
            if (slot == ConstantSlot.Light && _hasLight && sizeof(T) == sizeof(LightCB))
            {
                LightCB cached = _cachedLight;
                data = *(T*)&cached;
                return true;
            }
            if (slot == ConstantSlot.Shadow && _hasShadow && sizeof(T) == sizeof(ShadowCB))
            {
                ShadowCB cached = _cachedShadow;
                data = *(T*)&cached;
                return true;
            }
            if (slot == ConstantSlot.Ui && _hasUi && sizeof(T) == sizeof(UiCB))
            {
                UiCB cached = _cachedUi;
                data = *(T*)&cached;
                return true;
            }
            if (slot == ConstantSlot.Post && _hasPost && sizeof(T) == sizeof(PostCB))
            {
                PostCB cached = _cachedPost;
                data = *(T*)&cached;
                return true;
            }
            return false;
        }

        public void BindCamera(in Matrix4x4 view, in Matrix4x4 projection, in Matrix4x4 model)
        {
            if (!_layoutChecked)
            {
                ConstantBufferLayout.Validate();
                _layoutChecked = true;
            }
            FrameCB frame;
            if (!TryGetConstants(ConstantSlot.Frame, out frame))
                frame = new FrameCB { View = Matrix4x4.Identity, Projection = Matrix4x4.Identity };
            frame.View = view;
            frame.Projection = projection;
            if (Matrix4x4.Invert(view, out Matrix4x4 invView))
                frame.ViewPos = new Vector4(invView.Translation, 1f);
            SetConstants(ConstantSlot.Frame, frame);
            ObjectCB obj;
            if (!TryGetConstants(ConstantSlot.Object, out obj))
                obj = new ObjectCB { Model = Matrix4x4.Identity, NormalMatrix = Matrix4x4.Identity };
            obj.Model = model;
            if (Matrix4x4.Invert(model, out Matrix4x4 invModel))
                obj.NormalMatrix = Matrix4x4.Transpose(invModel);
            else
                obj.NormalMatrix = Matrix4x4.Identity;
            SetConstants(ConstantSlot.Object, obj);
        }

        void CacheConstants<T>(int slot, in T data) where T : unmanaged
        {
            T local = data;
            if (slot == ConstantSlot.Frame && sizeof(T) == sizeof(FrameCB))
                _cachedFrame = *(FrameCB*)&local;
            else if (slot == ConstantSlot.Object && sizeof(T) == sizeof(ObjectCB))
                _cachedObject = *(ObjectCB*)&local;
            else if (slot == ConstantSlot.Material && sizeof(T) == sizeof(MaterialCB))
            {
                _cachedMaterial = *(MaterialCB*)&local;
                _hasMaterial = true;
            }
            else if (slot == ConstantSlot.Light && sizeof(T) == sizeof(LightCB))
            {
                _cachedLight = *(LightCB*)&local;
                _hasLight = true;
            }
            else if (slot == ConstantSlot.Shadow && sizeof(T) == sizeof(ShadowCB))
            {
                _cachedShadow = *(ShadowCB*)&local;
                _hasShadow = true;
            }
            else if (slot == ConstantSlot.Ui && sizeof(T) == sizeof(UiCB))
            {
                _cachedUi = *(UiCB*)&local;
                _hasUi = true;
            }
            else if (slot == ConstantSlot.Post && sizeof(T) == sizeof(PostCB))
            {
                _cachedPost = *(PostCB*)&local;
                _hasPost = true;
            }
        }

        public void DrawIndexed(int indexCount)
        {
            int mode = _enums.Triangles;
            if (_boundPipeline.IsValid && _pipelinePrimitive.TryGetValue(_boundPipeline.Id, out int primitive))
                mode = primitive;
            DrawElements(mode, (uint)indexCount, _enums.UnsignedInt, null);
        }

        public void Draw(int vertexCount)
        {
            int mode = _enums.Triangles;
            if (_boundPipeline.IsValid && _pipelinePrimitive.TryGetValue(_boundPipeline.Id, out int primitive))
                mode = primitive;
            DrawArrays(mode, 0, (uint)vertexCount);
        }

        public void Dispatch(uint groupsX, uint groupsY = 1, uint groupsZ = 1)
        {
            DispatchCompute(groupsX, groupsY, groupsZ);
        }

        uint CompileStage(int type, string source)
        {
            uint shader = CreateShader(type);
            ShaderSource(shader, source);
            CompileShader(shader);
            GetShader(shader, _enums.CompileStatus, out int status);
            if (status != 1)
            {
                string log = GetShaderInfoLog(shader);
                DeleteShader(shader);
                throw new Exception($"Shader compilation failed: {log}");
            }
            return shader;
        }

        uint LinkStages(uint[] stages)
        {
            uint program = CreateProgram();
            for (int i = 0; i < stages.Length; i++)
                AttachShader(program, stages[i]);
            LinkProgram(program);
            GetProgram(program, _enums.LinkStatus, out int status);
            if (status != 1)
            {
                string log = GetProgramInfoLog(program);
                for (int i = 0; i < stages.Length; i++)
                {
                    DetachShader(program, stages[i]);
                    DeleteShader(stages[i]);
                }
                DeleteProgram(program);
                throw new Exception($"Shader program linking failed: {log}");
            }
            for (int i = 0; i < stages.Length; i++)
            {
                DetachShader(program, stages[i]);
                DeleteShader(stages[i]);
            }
            return program;
        }

        GpuHandle Track(GpuResourceKind kind, uint id)
        {
            _generation++;
            if (_generation == 0)
                _generation = 1;
            _live[Pack(kind, id)] = _generation;
            return new GpuHandle(id, _generation, kind);
        }

        bool IsLive(GpuHandle handle)
        {
            if (!handle.IsValid)
                return false;
            return _live.TryGetValue(Pack(handle.Kind, handle.Id), out uint gen) && gen == handle.Generation;
        }

        static ulong Pack(GpuResourceKind kind, uint id)
        {
            return ((ulong)kind << 32) | id;
        }

        void BindBlock(uint program, string name, int binding)
        {
            if (string.IsNullOrEmpty(name))
                return;
            uint index = _gl.GetUniformBlockIndex(program, name);
            if (index == uint.MaxValue)
                return;
            _gl.UniformBlockBinding(program, index, (uint)binding);
        }

        void BindSamplerUnits(uint program)
        {
            if (program == 0)
                return;
            UseProgram(program);
            BindSampler(program, "uTexture", TextureSlot.Albedo);
            BindSampler(program, "uAlbedoMap", TextureSlot.Albedo);
            BindSampler(program, "uColor", TextureSlot.Albedo);
            BindSampler(program, "Color", TextureSlot.Albedo);
            for (int i = 0; i < 4; i++)
            {
                BindSampler(program, "uAlbedoMap[" + i + "]", i);
                BindSampler(program, "uNormalMap[" + i + "]", 4 + i);
                BindSampler(program, "uMetallicMap[" + i + "]", 8 + i);
            }
            BindSampler(program, "uDepth", TextureSlot.Depth);
            BindSampler(program, "uHistory", TextureSlot.History);
            BindSampler(program, "uEdges", 0);
            BindSampler(program, "uWeights", 1);
            BindSampler(program, "uBloom", 1);
            BindSampler(program, "uLow", 0);
            BindSampler(program, "uHigh", 1);
            BindSampler(program, "uAdaptedLuma", 2);
            BindSampler(program, "uCurrent", 0);
            BindSampler(program, "uPrevious", 1);
            BindSampler(program, "uOpacityMap", TextureSlot.Opacity);
            BindSampler(program, "uShadowAtlas", TextureSlot.ShadowAtlas);
            BindSampler(program, "uPointShadowCube", TextureSlot.PointShadow);
            BindSampler(program, "uSpotShadowMap", TextureSlot.SpotShadow);
            BindSampler(program, "uSkybox", 0);
        }

        void BindSampler(uint program, string name, int unit)
        {
            int loc = GetUniformLocation(program, name);
            if (loc >= 0)
                Uniform1(loc, unit);
        }

        void ApplyState(in GpuRenderState state)
        {
            if (state.DepthTest) Enable(_enums.DepthTest);
            else Disable(_enums.DepthTest);
            DepthMask(state.DepthWrite);
            if (state.Blend) Enable(_enums.Blend);
            else Disable(_enums.Blend);
            if (state.CullMode == _enums.None)
                Disable(_enums.CullFace);
            else
            {
                Enable(_enums.CullFace);
                CullFace(state.CullMode);
            }
        }

        public void UpdateCubeFace(GpuHandle texture, int face, int width, int height, int format, int type, void* pixels)
        {
            if (!texture.IsValid || texture.Id == 0)
                return;
            int target = _enums.TextureCubeMap;
            if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                target = stored;
            BindTexture(target, texture.Id);
            int faceTarget = _enums.TextureCubeMapPositiveX + face;
            TexImage2D(faceTarget, 0, _enums.InternalRgba, (uint)width, (uint)height, 0, format, type, pixels);
        }

        public void GenerateMipmaps(GpuHandle texture)
        {
            if (!texture.IsValid || texture.Id == 0)
                return;
            int target = _enums.Texture2D;
            if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                target = stored;
            BindTexture(target, texture.Id);
            GenerateMipmap(target);
        }

        public void DrawFullscreen()
        {
            Disable(_enums.DepthTest);
            DepthMask(false);
            Disable(_enums.CullFace);
            Disable(_enums.Blend);
            ColorMask(true, true, true, true);
            if (_boundPipeline.IsValid && _pipelineVao.TryGetValue(_boundPipeline.Id, out uint vao) && vao != 0)
                BindVertexArray(vao);
            else
            {
                if (!_blitVaoCreated)
                {
                    _blitVao = GenVertexArray();
                    _blitVaoCreated = true;
                }
                BindVertexArray(_blitVao);
            }
            DrawArrays(_enums.Triangles, 0, 3);
        }


        public void UpdateCubemapFace(GpuHandle texture, int faceTarget, int width, int height, int format, int type, void* pixels)
        {
            if (!texture.IsValid || texture.Id == 0)
                return;
            int target = _enums.TextureCubeMap;
            if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                target = stored;
            BindTexture(target, texture.Id);
            TexImage2D(faceTarget, 0, _enums.InternalRgba, (uint)width, (uint)height, 0, format, type, pixels);
        }

        public void SetTextureParam(GpuHandle texture, int pname, int param)
        {
            if (!texture.IsValid || texture.Id == 0)
                return;
            int target = _enums.Texture2D;
            if (_textureTarget.TryGetValue(texture.Id, out int stored) && stored != 0)
                target = stored;
            BindTexture(target, texture.Id);
            TexParameter(target, pname, param);
        }


        public void BindBuffer(int target, GpuHandle buffer)
        {
            if (!buffer.IsValid)
            {
                BindBuffer(target, 0);
                return;
            }
            BindBuffer(target, buffer.Id);
        }

        public unsafe void SetUniformMatrix4(string name, ReadOnlySpan<float> values, int count)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name) || values.Length == 0 || count <= 0)
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc < 0)
                return;
            fixed (float* ptr = values)
                UniformMatrix4(loc, (uint)count, false, ptr);
        }

        public unsafe void SetUniformMatrix3(string name, ReadOnlySpan<float> values, int count)
        {
            if (!_boundPipeline.IsValid || string.IsNullOrEmpty(name) || values.Length == 0 || count <= 0)
                return;
            int loc = GetUniformLocation(_boundPipeline.Id, name);
            if (loc < 0)
                return;
            fixed (float* ptr = values)
                UniformMatrix3(loc, (uint)count, false, ptr);
        }


    }
}
