// Folder: SiegeEngine/Core/Rendering
// File: SkyboxRenderer.cs
using SiegeEngine.Core.Definitions;
using SiegeEngine.Core.GPU.ContextManagement;
using SiegeEngine.Core.GPU.Shaders;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SiegeEngine.Core.GPU.Renderers
{
    public unsafe class SkyboxRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private ShaderProgram _skyShader;
        private GpuHandle _skyPipeline;
        private VertexBuffer _cubeBuffer;
        private uint _cubemapTexture = 0;

        public SkyboxRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
        }

        public void Initialize()
        {
            _skyShader = new ShaderProgram(_renderContext, SkyboxShader.VertexShaderSource, SkyboxShader.FragmentShaderSource);
            _skyPipeline = _renderContext.CreatePipeline(ShaderCatalog.Describe(ShaderId.Skybox, _renderContext));
            _cubeBuffer = new VertexBuffer(_renderContext);
            BuildCubeMesh();
        }

        private void BuildCubeMesh()
        {
            var vertices = new List<float>();
            var indices = new List<uint>();
            float s = 50000f;
            vertices.Add(-s); vertices.Add(-s); vertices.Add(-s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(0); vertices.Add(0);
            vertices.Add(s); vertices.Add(-s); vertices.Add(-s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(0);
            vertices.Add(s); vertices.Add(s); vertices.Add(-s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1);
            vertices.Add(-s); vertices.Add(s); vertices.Add(-s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(0); vertices.Add(1);
            vertices.Add(-s); vertices.Add(-s); vertices.Add(s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(0); vertices.Add(0);
            vertices.Add(s); vertices.Add(-s); vertices.Add(s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(0);
            vertices.Add(s); vertices.Add(s); vertices.Add(s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1);
            vertices.Add(-s); vertices.Add(s); vertices.Add(s); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(1); vertices.Add(0); vertices.Add(1);
            indices.Add(0); indices.Add(1); indices.Add(2); indices.Add(2); indices.Add(3); indices.Add(0);
            indices.Add(4); indices.Add(5); indices.Add(6); indices.Add(6); indices.Add(7); indices.Add(4);
            indices.Add(0); indices.Add(4); indices.Add(7); indices.Add(7); indices.Add(3); indices.Add(0);
            indices.Add(1); indices.Add(5); indices.Add(6); indices.Add(6); indices.Add(2); indices.Add(1);
            indices.Add(3); indices.Add(2); indices.Add(6); indices.Add(6); indices.Add(7); indices.Add(3);
            indices.Add(0); indices.Add(1); indices.Add(5); indices.Add(5); indices.Add(4); indices.Add(0);
            _cubeBuffer.UpdateCustomWithUV(vertices, indices);
        }

        public void LoadSkybox(SkyboxData skybox)
        {
            if (skybox == null || !skybox.Enabled) return;
            TextureLoader.DeleteTexture(_renderContext, ref _cubemapTexture);
            if (skybox.Type == "Cubemap" && !string.IsNullOrEmpty(skybox.CubemapPath))
            {
                _cubemapTexture = TextureLoader.LoadCubemap(_renderContext, skybox.CubemapPath);
            }
            else if (skybox.Faces.Count == 6)
            {
                _cubemapTexture = TextureLoader.LoadSixFacesCubemap(_renderContext, skybox.Faces.ToArray());
            }
        }

        public void RenderSkybox(SkyboxData skybox, Matrix4x4 view, Matrix4x4 projection)
        {
            if (skybox == null || !skybox.Enabled || _cubemapTexture == 0) return;
            _renderContext.Disable(_renderContext.Enums.DepthTest);
            _renderContext.Disable(_renderContext.Enums.CullFace);
            Matrix4x4 viewNoTranslation = view;
            viewNoTranslation.M41 = 0; viewNoTranslation.M42 = 0; viewNoTranslation.M43 = 0;
            FrameCB frame = new FrameCB { View = viewNoTranslation, Projection = projection };
            ObjectCB obj = new ObjectCB
            {
                Model = Matrix4x4.CreateFromQuaternion(skybox.Orientation),
                VerticalOffset = skybox.VerticalOffset
            };
            _renderContext.BindPipeline(_skyPipeline);
            _renderContext.SetConstants(ConstantSlot.Frame, frame);
            _renderContext.SetConstants(ConstantSlot.Object, obj);
            _renderContext.ActiveTexture(0);
            _renderContext.BindTexture(_renderContext.Enums.TextureCubeMap, _cubemapTexture);
            _renderContext.BindVertexBuffer(_cubeBuffer.VertexHandle, 0, _cubeBuffer.Stride, 0);
            _renderContext.BindIndexBuffer(_cubeBuffer.IndexHandle);
            _renderContext.DrawIndexed((int)_cubeBuffer.GetIndexCount());
            _renderContext.Enable(_renderContext.Enums.DepthTest);
            _renderContext.Enable(_renderContext.Enums.CullFace);
        }

        public static void RenderPreviewCube(IRenderContext renderContext, uint cubemapTex, VertexBuffer cube, ShaderProgram shader, Matrix4x4 mvp, bool clearDepth)
        {
            if (renderContext == null || cubemapTex == 0 || cube == null || shader == null) return;
            renderContext.Enable(renderContext.Enums.DepthTest);
            renderContext.Disable(renderContext.Enums.CullFace);
            if (clearDepth)
                renderContext.Clear(renderContext.Enums.DepthBufferBit);
            shader.Use();
            shader.SetMatrix4("uMVP", mvp);
            renderContext.ActiveTexture(0);
            renderContext.BindTexture(renderContext.Enums.TextureCubeMap, cubemapTex);
            cube.Bind();
            renderContext.DrawElements(renderContext.Enums.Triangles, cube.GetIndexCount(), renderContext.Enums.UnsignedInt, null);
        }

        public void Dispose()
        {
            _skyShader?.Dispose();
            if (_skyPipeline.IsValid)
                _renderContext.Destroy(_skyPipeline);
            _cubeBuffer?.Dispose();
            TextureLoader.DeleteTexture(_renderContext, ref _cubemapTexture);
        }
    }
}