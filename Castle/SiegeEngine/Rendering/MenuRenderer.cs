using System;
using System.Numerics;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

namespace SiegeEngine.Rendering
{
    public unsafe class MenuRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Glfw _glfw;
        private readonly WindowHandle* _window;
        private uint _vao, _vbo, _shaderProgram;
        private bool _disposed;

        // Button positions and sizes (screen space: 0 to 1)
        private readonly float[][] buttonRects = new float[][]
        {
            new float[] { 0.35f, 0.60f, 0.30f, 0.10f }, // Single Player (x, y, width, height)
            new float[] { 0.35f, 0.45f, 0.30f, 0.10f }, // Multiplayer
            new float[] { 0.35f, 0.30f, 0.30f, 0.10f }, // Settings
            new float[] { 0.35f, 0.15f, 0.30f, 0.10f }  // Exit
        };
        private readonly string[] buttonLabels = new[] { "Single Player", "Multiplayer", "Settings", "Exit" };

        public MenuRenderer(Glfw glfw, GL gl, WindowHandle* window)
        {
            _glfw = glfw;
            _gl = gl;
            _window = window;
            Initialize();
        }

        private void Initialize()
        {
            // Vertex shader (2D screen space)
            string vertexShaderSource = @"
                #version 330 core
                layout(location = 0) in vec2 aPosition;
                void main() {
                    gl_Position = vec4(aPosition, 0.0, 1.0);
                }";

            // Fragment shader (solid color)
            string fragmentShaderSource = @"
                #version 330 core
                out vec4 FragColor;
                uniform vec4 uColor;
                void main() {
                    FragColor = uColor;
                }";

            uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vertexShader, vertexShaderSource);
            _gl.CompileShader(vertexShader);
            CheckShaderCompile(vertexShader);

            uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fragmentShader, fragmentShaderSource);
            _gl.CompileShader(fragmentShader);
            CheckShaderCompile(fragmentShader);

            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, vertexShader);
            _gl.AttachShader(_shaderProgram, fragmentShader);
            _gl.LinkProgram(_shaderProgram);
            CheckProgramLink(_shaderProgram);

            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            // Quad vertices for buttons (reused for all)
            float[] quadVertices = new float[]
            {
                0.0f, 0.0f, // Bottom-left
                0.0f, 1.0f, // Top-left
                1.0f, 0.0f, // Bottom-right
                1.0f, 1.0f  // Top-right
            };

            _gl.GenVertexArrays(1, out _vao);
            _gl.BindVertexArray(_vao);

            _gl.GenBuffers(1, out _vbo);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* ptr = quadVertices)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(quadVertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }

        public void Render()
        {
            _gl.UseProgram(_shaderProgram);
            _gl.BindVertexArray(_vao);

            // Draw each button
            for (int i = 0; i < buttonRects.Length; i++)
            {
                float x = buttonRects[i][0];
                float y = buttonRects[i][1];
                float w = buttonRects[i][2];
                float h = buttonRects[i][3];

                // Transform to OpenGL coordinates (-1 to 1)
                float left = x * 2.0f - 1.0f;
                float right = (x + w) * 2.0f - 1.0f;
                float bottom = y * 2.0f - 1.0f;
                float top = (y + h) * 2.0f - 1.0f;

                // Button color (gray, hover effect in HandleInput)
                _gl.Uniform4(_gl.GetUniformLocation(_shaderProgram, "uColor"), 1.0f, 0.0f, 0.0f, 1.0f); // Bright red

                // Update vertex data for this button
                float[] vertices = new float[]
                {
                    left, bottom,
                    left, top,
                    right, bottom,
                    right, top
                };
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
                fixed (float* ptr = vertices)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
                }

                _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            }

            _gl.BindVertexArray(0);
        }

        public int HandleInput()
        {
            _glfw.GetCursorPos(_window, out double mouseX, out double mouseY);
            int width, height;
            _glfw.GetWindowSize(_window, out width, out height);
            float mx = (float)mouseX / width;
            float my = 1.0f - (float)mouseY / height; // Flip Y-axis (top is 1.0)

            bool clicked = _glfw.GetMouseButton(_window, (int)MouseButton.Left).Equals(InputAction.Press);

            for (int i = 0; i < buttonRects.Length; i++)
            {
                float x = buttonRects[i][0];
                float y = buttonRects[i][1];
                float w = buttonRects[i][2];
                float h = buttonRects[i][3];

                bool hovered = mx >= x && mx <= x + w && my >= y && my <= y + h;
                if (hovered && clicked)
                {
                    return i; // 0: Single Player, 1: Multiplayer, 2: Settings, 3: Exit
                }
            }
            return -1; // No button clicked
        }

        private void CheckShaderCompile(uint shader)
        {
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int success);
            if (success == 0) throw new Exception($"Shader compilation failed: {_gl.GetShaderInfoLog(shader)}");
        }

        private void CheckProgramLink(uint program)
        {
            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int success);
            if (success == 0) throw new Exception($"Program linking failed: {_gl.GetProgramInfoLog(program)}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteProgram(_shaderProgram);
            _disposed = true;
        }
    }
}