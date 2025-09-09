using System.Numerics;
using SiegeEngine.ContextManagement;
using SiegeEngine.Rendering.Shaders;

namespace SiegeEngine.Rendering
{
    public unsafe class UIQuadRenderer
    {
        private readonly IRenderContext _renderContext;
        private uint _vao, _vbo;
        private ShaderProgram _shader;

        public UIQuadRenderer(IRenderContext renderContext)
        {
            _renderContext = renderContext;
            Initialize();
        }

        private void Initialize()
        {
            _shader = new ShaderProgram(_renderContext, UiShader.VertexSource, UiShader.FragmentSource);

            _renderContext.GenVertexArrays(1, out _vao);
            _renderContext.BindVertexArray(_vao);

            _renderContext.GenBuffers(1, out _vbo);
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _vbo);

            float[] vertices = new float[]
            {
                0.0f, 0.0f,
                1.0f, 0.0f,
                1.0f, 1.0f,
                0.0f, 1.0f
            };

            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
            }

            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 2 * sizeof(float), (void*)0);

            _renderContext.BindVertexArray(0);
        }

        public void DrawQuad(Vector2 position, Vector2 size, Vector4 color, Matrix4x4 ortho)
        {
            _shader.Use();
            Matrix4x4 scale = Matrix4x4.CreateScale(size.X, size.Y, 1f);
            Matrix4x4 translate = Matrix4x4.CreateTranslation(position.X, position.Y, 0f);
            Matrix4x4 model = scale * translate;
            _shader.SetMatrix4("uTransform", ortho * model);
            _shader.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _shader.SetUniform("uUseTexture", 0.0f);

            _renderContext.BindVertexArray(_vao);
            _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
            _renderContext.BindVertexArray(0);
        }
    }
}