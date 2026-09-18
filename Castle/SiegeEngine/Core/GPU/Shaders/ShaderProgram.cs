// Folder: SiegeEngine.Core.GPU
// File: ShaderProgram.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Shaders
{
    public class ShaderProgram : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly OpenGLRenderContext _gl;
        private readonly GpuHandle _pipeline;
        private readonly uint _program;
        private bool _disposed;
        private readonly Dictionary<string, int> _uniformLocations = new Dictionary<string, int>();
        private float[] _mat4Scratch = new float[16];
        private float[] _mat3Scratch = new float[9];

        public ShaderId ShaderId { get; private set; }

        public ShaderProgram(IRenderContext renderContext, string vertexShaderSource, string fragmentShaderSource)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            if (string.IsNullOrEmpty(vertexShaderSource))
                throw new ArgumentNullException(nameof(vertexShaderSource));
            if (string.IsNullOrEmpty(fragmentShaderSource))
                throw new ArgumentNullException(nameof(fragmentShaderSource));

            _gl = _renderContext as OpenGLRenderContext;
            _pipeline = _renderContext.CreatePipeline(new PipelineDesc
            {
                VertexSource = vertexShaderSource,
                FragmentSource = fragmentShaderSource,
                State = new GpuRenderState { DepthTest = true, DepthWrite = true }
            });
            _program = _pipeline.Id;
                        BindConstantBlocks();
        }

        void BindConstantBlocks()
        {
            _renderContext.BindUniformBlocks();
            _renderContext.BindPipeline(_pipeline);
            BindSampler("uTexture", TextureSlot.Albedo);
            BindSampler("uAlbedoMap", TextureSlot.Albedo);
            BindSampler("uColor", TextureSlot.Color);
            BindSampler("uOpacityMap", TextureSlot.Opacity);
            BindSampler("uShadowAtlas", TextureSlot.ShadowAtlas);
            BindSampler("uPointShadowCube", TextureSlot.PointShadow);
            BindSampler("uSpotShadowMap", TextureSlot.SpotShadow);
        }

        void BindSampler(string name, int unit)
        {
            if (_gl == null) return;
            int loc = _gl.GetUniformLocation(_program, name);
            if (loc >= 0)
                _gl.Uniform1(loc, unit);
        }

        public static ShaderProgram FromId(IRenderContext renderContext, ShaderId id)
        {
            if (renderContext == null)
                throw new ArgumentNullException(nameof(renderContext));
            ShaderSourceSet src = ShaderCatalog.Get(id, renderContext.Backend);
            if (src.IsCompute)
                throw new InvalidOperationException($"ShaderId '{id}' is compute.");
            var program = new ShaderProgram(renderContext, src.Vertex, src.Fragment);
            program.ShaderId = id;
            return program;
        }

        public int FindUniform(string name) => GetLocation(name);

        private int GetLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out int loc))
                return loc;
            loc = _gl != null ? _gl.GetUniformLocation(_program, name) : -1;
            _uniformLocations[name] = loc;
            return loc;
        }

        public void Use()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ShaderProgram));
            OpenGLRenderContext gl = _renderContext as OpenGLRenderContext;
            if (gl != null)
                gl.UseProgram(_program);
            else
                _renderContext.BindPipeline(_pipeline);
        }

        public void SetUniform(string name, float value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            _gl.Uniform1(location, value);
        }

        public void SetUniform(string name, int value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            _gl.Uniform1(location, value);
        }

        public void SetUniform(string name, float x, float y)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            _gl.Uniform2(location, x, y);
        }

        public void SetUniform(string name, float x, float y, float z)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            _gl.Uniform3(location, x, y, z);
        }

        public void SetUniform(string name, float x, float y, float z, float w)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            _gl.Uniform4(location, x, y, z, w);
        }

        public unsafe void SetMatrix4(string name, Matrix4x4 matrix)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1)
            {
                WriteLegacyMatrix(name, matrix);
                return;
            }
            float[] matrixArray = _mat4Scratch;
            if (matrixArray.Length < 16)
            {
                matrixArray = new float[16];
                _mat4Scratch = matrixArray;
            }
            matrixArray[0] = matrix.M11; matrixArray[1] = matrix.M12; matrixArray[2] = matrix.M13; matrixArray[3] = matrix.M14;
            matrixArray[4] = matrix.M21; matrixArray[5] = matrix.M22; matrixArray[6] = matrix.M23; matrixArray[7] = matrix.M24;
            matrixArray[8] = matrix.M31; matrixArray[9] = matrix.M32; matrixArray[10] = matrix.M33; matrixArray[11] = matrix.M34;
            matrixArray[12] = matrix.M41; matrixArray[13] = matrix.M42; matrixArray[14] = matrix.M43; matrixArray[15] = matrix.M44;
            fixed (float* matrixPtr = matrixArray)
            {
                _gl.UniformMatrix4(location, 1, false, matrixPtr);
            }
        }

        public unsafe void SetMatrix4Array(string name, Matrix4x4[] matrices)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            int needed = matrices.Length * 16;
            float[] data = _mat4Scratch;
            if (data.Length < needed)
            {
                data = new float[needed];
                _mat4Scratch = data;
            }
            for (int i = 0; i < matrices.Length; i++)
            {
                data[i * 16 + 0] = matrices[i].M11;
                data[i * 16 + 1] = matrices[i].M12;
                data[i * 16 + 2] = matrices[i].M13;
                data[i * 16 + 3] = matrices[i].M14;
                data[i * 16 + 4] = matrices[i].M21;
                data[i * 16 + 5] = matrices[i].M22;
                data[i * 16 + 6] = matrices[i].M23;
                data[i * 16 + 7] = matrices[i].M24;
                data[i * 16 + 8] = matrices[i].M31;
                data[i * 16 + 9] = matrices[i].M32;
                data[i * 16 + 10] = matrices[i].M33;
                data[i * 16 + 11] = matrices[i].M34;
                data[i * 16 + 12] = matrices[i].M41;
                data[i * 16 + 13] = matrices[i].M42;
                data[i * 16 + 14] = matrices[i].M43;
                data[i * 16 + 15] = matrices[i].M44;
            }
            fixed (float* ptr = data)
            {
                _gl.UniformMatrix4(location, (uint)matrices.Length, false, ptr);
            }
        }

        public unsafe void SetMatrix3Array(string name, Matrix3x3[] matrices)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShaderProgram));
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            int location = GetLocation(name);
            if (location == -1) return;
            int needed = matrices.Length * 9;
            float[] data = _mat3Scratch;
            if (data.Length < needed)
            {
                data = new float[needed];
                _mat3Scratch = data;
            }
            for (int i = 0; i < matrices.Length; i++)
            {
                data[i * 9 + 0] = matrices[i].M11;
                data[i * 9 + 1] = matrices[i].M12;
                data[i * 9 + 2] = matrices[i].M13;
                data[i * 9 + 3] = matrices[i].M21;
                data[i * 9 + 4] = matrices[i].M22;
                data[i * 9 + 5] = matrices[i].M23;
                data[i * 9 + 6] = matrices[i].M31;
                data[i * 9 + 7] = matrices[i].M32;
                data[i * 9 + 8] = matrices[i].M33;
            }
            fixed (float* ptr = data)
            {
                _gl.UniformMatrix3(location, (uint)matrices.Length, false, ptr);
            }
        }

        void WriteLegacyMatrix(string name, Matrix4x4 matrix)
        {
            if (name == "uView" || name == "View")
            {
                FrameCB frame;
                if (!_renderContext.TryGetConstants(ConstantSlot.Frame, out frame))
                    frame = new FrameCB { View = Matrix4x4.Identity, Projection = Matrix4x4.Identity };
                frame.View = matrix;
                _renderContext.SetConstants(ConstantSlot.Frame, frame);
                return;
            }
            if (name == "uProjection" || name == "Projection")
            {
                FrameCB frame;
                if (!_renderContext.TryGetConstants(ConstantSlot.Frame, out frame))
                    frame = new FrameCB { View = Matrix4x4.Identity, Projection = Matrix4x4.Identity };
                frame.Projection = matrix;
                _renderContext.SetConstants(ConstantSlot.Frame, frame);
                return;
            }
            if (name == "uModel" || name == "Model")
            {
                ObjectCB obj;
                if (!_renderContext.TryGetConstants(ConstantSlot.Object, out obj))
                    obj = new ObjectCB { Model = Matrix4x4.Identity, NormalMatrix = Matrix4x4.Identity };
                obj.Model = matrix;
                _renderContext.SetConstants(ConstantSlot.Object, obj);
                return;
            }
            if (name == "uNormalMatrix" || name == "NormalMatrix")
            {
                ObjectCB obj;
                if (!_renderContext.TryGetConstants(ConstantSlot.Object, out obj))
                    obj = new ObjectCB { Model = Matrix4x4.Identity, NormalMatrix = Matrix4x4.Identity };
                obj.NormalMatrix = matrix;
                _renderContext.SetConstants(ConstantSlot.Object, obj);
                return;
            }
            if (name == "uTransform" || name == "Transform")
            {
                UiCB ui;
                if (!_renderContext.TryGetConstants(ConstantSlot.Ui, out ui))
                    ui = new UiCB { Transform = Matrix4x4.Identity, Color = Vector4.One };
                ui.Transform = matrix;
                _renderContext.SetConstants(ConstantSlot.Ui, ui);
                return;
            }
            if (name == "uPrevView" || name == "PrevView")
            {
                PatchPost(p => { p.PrevView = matrix; return p; });
                return;
            }
            if (name == "uPrevProjection" || name == "PrevProjection")
            {
                PatchPost(p => { p.PrevProjection = matrix; return p; });
                return;
            }
            if (name == "uInvView" || name == "InvView")
            {
                PatchPost(p => { p.InvView = matrix; return p; });
                return;
            }
            if (name == "uInvProjection" || name == "InvProjection")
            {
                PatchPost(p => { p.InvProjection = matrix; return p; });
                return;
            }
            if (name == "uMVP")
            {
                FrameCB frame;
                if (!_renderContext.TryGetConstants(ConstantSlot.Frame, out frame))
                    frame = new FrameCB { View = Matrix4x4.Identity, Projection = Matrix4x4.Identity };
                frame.View = Matrix4x4.Identity;
                frame.Projection = matrix;
                _renderContext.SetConstants(ConstantSlot.Frame, frame);
            }
        }

        void PatchPost(Func<PostCB, PostCB> patch)
        {
            PostCB post;
            if (!_renderContext.TryGetConstants(ConstantSlot.Post, out post))
                post = default;
            _renderContext.SetConstants(ConstantSlot.Post, patch(post));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try { if (_pipeline.IsValid) _renderContext.Destroy(_pipeline); }
                catch (Exception ex) { Console.WriteLine($"Error deleting shader program: {ex.Message}"); }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
