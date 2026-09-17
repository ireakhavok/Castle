// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: OpenGLRenderContext.cs
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Collections.Generic;

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
        private readonly uint[] _uboSlots = new uint[8];
        private GpuHandle _boundPipeline;

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

        public void ReadPixels(int x, int y, uint width, uint height, int format, int type, void* data) =>
            _gl.ReadPixels(x, y, width, height, (PixelFormat)format, (PixelType)type, data);

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

        public uint FenceSync(int condition, uint flags)
        {
            return (uint)_gl.FenceSync((SyncCondition)condition, (SyncBehaviorFlags)flags);
        }

        public int ClientWaitSync(uint sync, uint flags, ulong timeout)
        {
            return (int)_gl.ClientWaitSync((nint)sync, flags, timeout);
        }

        public void DeleteSync(uint sync)
        {
            _gl.DeleteSync((nint)sync);
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
            return handle;
        }

        public GpuHandle CreateBuffer(in BufferDesc desc)
        {
            uint buffer = GenBuffer();
            if (desc.ByteSize > 0)
            {
                int target = desc.Target != 0 ? desc.Target : _enums.ArrayBuffer;
                int usage = desc.Usage != 0 ? desc.Usage : _enums.StaticDraw;
                BindBuffer(target, buffer);
                BufferData(target, (uint)desc.ByteSize, null, usage);
            }
            return Track(GpuResourceKind.Buffer, buffer);
        }

        public GpuHandle CreateTexture(in TextureDesc desc)
        {
            GenTextures(1, out uint texture);
            if (desc.Width > 0 && desc.Height > 0)
            {
                int target = desc.Target != 0 ? desc.Target : _enums.Texture2D;
                int internalFormat = desc.InternalFormat != 0 ? desc.InternalFormat : _enums.InternalRgba;
                BindTexture(target, texture);
                TexImage2D(target, 0, internalFormat, (uint)desc.Width, (uint)desc.Height, 0, _enums.PixelRgba, _enums.UnsignedByte, null);
            }
            return Track(GpuResourceKind.Texture, texture);
        }

        public void Destroy(GpuHandle handle)
        {
            if (!IsLive(handle))
                return;
            _live.Remove(Pack(handle.Kind, handle.Id));
            if (handle.Kind == GpuResourceKind.Pipeline)
            {
                _pipelinePrimitive.Remove(handle.Id);
                DeleteProgram(handle.Id);
            }
            else if (handle.Kind == GpuResourceKind.Buffer)
            {
                DeleteBuffer(handle.Id);
            }
            else if (handle.Kind == GpuResourceKind.Texture)
            {
                DeleteTexture(handle.Id);
            }
        }

        public void BindPipeline(GpuHandle pipeline)
        {
            if (!IsLive(pipeline) || pipeline.Kind != GpuResourceKind.Pipeline)
                return;
            UseProgram(pipeline.Id);
            _boundPipeline = pipeline;
        }

        public void BindVertexBuffer(GpuHandle buffer, int slot, int stride, int offset)
        {
            if (!IsLive(buffer) || buffer.Kind != GpuResourceKind.Buffer)
                return;
            BindBuffer(_enums.ArrayBuffer, buffer.Id);
        }

        public void BindIndexBuffer(GpuHandle buffer)
        {
            if (!IsLive(buffer) || buffer.Kind != GpuResourceKind.Buffer)
                return;
            BindBuffer(_enums.ElementArrayBuffer, buffer.Id);
        }

        public void BindTextureSlot(int slot, GpuHandle texture)
        {
            if (!IsLive(texture) || texture.Kind != GpuResourceKind.Texture)
                return;
            ActiveTexture(_enums.Texture0 + slot);
            BindTexture(_enums.Texture2D, texture.Id);
        }

        public void UpdateBuffer(GpuHandle buffer, ReadOnlySpan<byte> data, int offset = 0)
        {
            if (!IsLive(buffer) || buffer.Kind != GpuResourceKind.Buffer || data.Length == 0)
                return;
            BindBuffer(_enums.ArrayBuffer, buffer.Id);
            fixed (byte* ptr = data)
            {
                BufferSubData(_enums.ArrayBuffer, offset, (uint)data.Length, ptr);
            }
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
        }

        public void DrawIndexed(int indexCount)
        {
            int mode = _enums.Triangles;
            if (_boundPipeline.IsValid && _pipelinePrimitive.TryGetValue(_boundPipeline.Id, out int primitive))
                mode = primitive;
            DrawElements(mode, (uint)indexCount, _enums.UnsignedInt, null);
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
    }
}
