using System;
using Silk.NET.GLFW;
using System.Numerics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using SiegeEngine.Rendering.Definitions;
using SiegeEngine.Interfaces;

namespace SiegeEngine.Rendering
{
    public unsafe class UIRenderingLayer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly Glfw _glfw;
        private readonly WindowHandle* _window;
        private ShaderProgram _shaderProgram;
        private uint _buttonVao, _buttonVbo;
        private readonly BackgroundRenderer _backgroundRenderer;
        private readonly TextRenderer _textRenderer;
        private uint _iconVao, _iconVbo;
        private Dictionary<string, uint> _iconTextures;
        private bool _disposed;

        public UIRenderingLayer(Glfw glfw, IRenderContext renderContext, WindowHandle* window)
        {
            _renderContext = renderContext;
            _glfw = glfw;
            _window = window;
            _iconTextures = new Dictionary<string, uint>();
            _backgroundRenderer = new BackgroundRenderer(glfw, renderContext, window);
            _textRenderer = new TextRenderer(glfw, renderContext, window);
        }

        public void Initialize(string backgroundPath, Dictionary<string, int> iconIndices)
        {
            if (_shaderProgram == null)
            {
                string vertexShaderSource = @"
                    #version 330 core
                    layout(location = 0) in vec2 aPosition;
                    layout(location = 1) in vec2 aTexCoord;
                    uniform mat4 uTransform;
                    out vec2 vTexCoord;
                    void main() {
                        gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
                        vTexCoord = aTexCoord;
                    }";
                string fragmentShaderSource = @"
                    #version 330 core
                    in vec2 vTexCoord;
                    out vec4 FragColor;
                    uniform vec4 uColor;
                    uniform sampler2D uTexture;
                    uniform bool uUseTexture;
                    void main() {
                        if (uUseTexture) {
                            FragColor = texture(uTexture, vTexCoord) * uColor;
                        } else {
                            FragColor = uColor;
                        }
                    }";
                _shaderProgram = new ShaderProgram(_renderContext, vertexShaderSource, fragmentShaderSource);
            }

            _renderContext.GenVertexArrays(1, out _buttonVao);
            _renderContext.GenBuffers(1, out _buttonVbo);
            _renderContext.BindVertexArray(_buttonVao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _buttonVbo);
            float[] buttonVertices = new float[]
            {
                0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 1.0f, 0.0f,
                1.0f, 1.0f, 1.0f, 1.0f,
                0.0f, 1.0f, 0.0f, 1.0f
            };
            fixed (float* ptr = buttonVertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(buttonVertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _renderContext.BindVertexArray(0);

            _renderContext.GenVertexArrays(1, out _iconVao);
            _renderContext.GenBuffers(1, out _iconVbo);
            _renderContext.BindVertexArray(_iconVao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _iconVbo);
            float[] iconVertices = new float[]
            {
                0.0f, 0.0f, 0.0f, 1.0f,
                1.0f, 0.0f, 1.0f, 1.0f,
                1.0f, 1.0f, 1.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f
            };
            fixed (float* ptr = iconVertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(iconVertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _renderContext.BindVertexArray(0);

            _backgroundRenderer.Initialize(backgroundPath, _shaderProgram);
            _textRenderer.Initialize(_shaderProgram);
            LoadIcons(iconIndices);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(nint handle);

        private void LoadIcons(Dictionary<string, int> iconIndices)
        {
            try
            {
                foreach (var kvp in iconIndices)
                {
                    string name = kvp.Key;
                    int iconIndex = kvp.Value;
                    if (iconIndex == 0)
                        continue;

                    nint hIconLarge, hIconSmall;
                    uint result = ExtractIconEx(Environment.SystemDirectory + "\\shell32.dll", iconIndex, out hIconLarge, out hIconSmall, 1);
                    if (result == 0 || hIconLarge == nint.Zero)
                        continue;

                    try
                    {
                        using (Icon icon = Icon.FromHandle(hIconLarge))
                        using (Bitmap bitmap = new Bitmap(icon.ToBitmap(), 32, 32))
                        {
                            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            int bytesPerPixel = 4;
                            byte[] pixelData = new byte[bitmap.Width * bitmap.Height * bytesPerPixel];
                            nint ptr = data.Scan0;
                            for (int y = 0; y < bitmap.Height; y++)
                            {
                                nint row = nint.Add(ptr, y * data.Stride);
                                Marshal.Copy(row, pixelData, y * bitmap.Width * bytesPerPixel, bitmap.Width * bytesPerPixel);
                            }

                            uint iconTexture;
                            _renderContext.GenTextures(1, out iconTexture);
                            _renderContext.BindTexture(TextureTarget.Texture2D, iconTexture);
                            _renderContext.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                            fixed (byte* pixelPtr = pixelData)
                            {
                                _renderContext.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)bitmap.Width, (uint)bitmap.Height, 0, GLEnum.Bgra, GLEnum.UnsignedByte, pixelPtr);
                            }
                            _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
                            _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
                            _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
                            _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
                            _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                            _iconTextures[name] = iconTexture;

                            bitmap.UnlockBits(data);
                        }
                    }
                    finally
                    {
                        if (hIconLarge != nint.Zero) DestroyIcon(hIconLarge);
                        if (hIconSmall != nint.Zero) DestroyIcon(hIconSmall);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UIRenderingLayer: Failed to load icons: {ex.Message}");
            }
        }

        public void BeginRender()
        {
            _shaderProgram.Use();
            _renderContext.Disable(EnableCap.DepthTest);
            _renderContext.Enable(EnableCap.Blend);
            _renderContext.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            int width, height;
            _glfw.GetWindowSize(_window, out width, out height);
            if (width <= 0 || height <= 0)
            {
                width = 1280;
                height = 720;
            }

            // Validate aspect ratio for ultrawide (32:9 = 3.555...)
            float currentRatio = (float)width / height;
            float expectedRatio = 32.0f / 9.0f;
            if (Math.Abs(currentRatio - expectedRatio) > 0.1f && width >= 3840)
            {
                height = (int)(width / expectedRatio);
                Console.WriteLine($"UIRenderingLayer: Adjusted viewport to maintain 32:9, new size: {width}x{height}");
            }

            _renderContext.Viewport(0, 0, (uint)width, (uint)height);

            _renderContext.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            _renderContext.Clear(ClearBufferMask.ColorBufferBit);

            RenderBackgroundPane(width, height);

            _backgroundRenderer.Render();
        }

        private void RenderBackgroundPane(int width, int height)
        {
            float topY = (1 - (-0.2235f)) / 2 * height;
            float bottomY = (1 - 0.0894f) / 2 * height;
            float leftX = width * 0.35f;
            float rightX = width * 0.65f;

            float left = 2.0f * leftX / width - 1.0f;
            float right = 2.0f * rightX / width - 1.0f;
            float top = 1.0f - 2.0f * bottomY / height;
            float bottom = 1.0f - 2.0f * topY / height;

            float[] vertices = new float[]
            {
                left, bottom, 0.0f, 0.0f,
                right, bottom, 1.0f, 0.0f,
                right, top, 1.0f, 1.0f,
                left, top, 0.0f, 1.0f
            };

            _renderContext.BindVertexArray(_buttonVao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _buttonVbo);
            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));

            Matrix4x4 transform = Matrix4x4.Identity;
            _shaderProgram.SetMatrix4("uTransform", transform);
            _shaderProgram.SetUniform("uUseTexture", 0.0f);
            _shaderProgram.SetUniform("uColor", 0.0f, 0.3f, 0.3f, 0.7f);
            _renderContext.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            _renderContext.BindVertexArray(0);
        }

        public void RenderElement(object element, Vector2 pos, int width, int height)
        {
            if (element == null)
                return;

            if (width <= 0 || height <= 0)
            {
                width = 1280;
                height = 720;
            }

            switch (element)
            {
                case Button button:
                    RenderButton(pos, button.Size, button.IsHovered, width, height, button.BackgroundColor, button.HoverColor, button.BorderColor);
                    float startX = pos.X + 10;
                    float fontSize = button.TextStyle?.FontSize ?? 10.0f;
                    float startY = pos.Y + button.Size.Y / 2 - fontSize / 2;
                    Vector4 textColor = button.TextStyle?.Color?.ToVector4() ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                    //Console.WriteLine($"UIRenderingLayer: Rendering Button '{button.Text}' at ({startX}, {startY}), FontSize: {fontSize}, Color: ({textColor.X}, {textColor.Y}, {textColor.Z}, {textColor.W})");
                    RenderText(button.Text, startX, startY, width, height, fontSize, textColor);
                    break;

                case Dropdown dropdown:
                    RenderButton(pos, dropdown.Size, dropdown.IsHovered, width, height,
                        dropdown.ButtonStyle?.BackgroundColor?.ToVector4() ?? new Vector4(1, 1, 1, 0.9f),
                        dropdown.ButtonStyle?.HoverColor?.ToVector4() ?? new Vector4(0.9f, 0.9f, 0.9f, 0.9f),
                        dropdown.ButtonStyle?.BorderColor?.ToVector4() ?? new Vector4(0, 0, 0, 1));
                    startX = pos.X + 10;
                    fontSize = dropdown.TextStyle?.FontSize ?? 10.0f;
                    startY = pos.Y + dropdown.Size.Y / 2 - fontSize / 2;
                    string selectedOption = dropdown.SelectedIndex >= 0 && dropdown.SelectedIndex < dropdown.Options.Count ? dropdown.Options[dropdown.SelectedIndex] : "None";
                    textColor = dropdown.TextStyle?.Color?.ToVector4() ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                    //Console.WriteLine($"UIRenderingLayer: Rendering Dropdown '{selectedOption}' at ({startX}, {startY}), FontSize: {fontSize}, Color: ({textColor.X}, {textColor.Y}, {textColor.Z}, {textColor.W})");
                    _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                    _shaderProgram.SetUniform("uUseTexture", 1.0f);
                    RenderText(selectedOption, startX, startY, width, height, fontSize, textColor);

                    if (dropdown.IsExpanded)
                    {
                        double mouseX, mouseY;
                        _glfw.GetCursorPos(_window, out mouseX, out mouseY);
                        Vector2 mousePos = new Vector2((float)mouseX, (float)mouseY);
                        for (int i = 0; i < dropdown.Options.Count; i++)
                        {
                            Vector2 optionPos = new Vector2(pos.X + dropdown.Size.X + 5, pos.Y + dropdown.Size.Y * i);
                            bool optionHovered = dropdown.GetOptionIndexAt(mousePos, pos) == i;
                            Vector2 optionSize = new Vector2(dropdown.Size.X, dropdown.Size.Y);
                            RenderButton(optionPos, optionSize, optionHovered, width, height,
                                dropdown.ButtonStyle?.BackgroundColor?.ToVector4() ?? new Vector4(1, 1, 1, 0.9f),
                                dropdown.ButtonStyle?.HoverColor?.ToVector4() ?? new Vector4(0.9f, 0.9f, 0.9f, 0.9f),
                                dropdown.ButtonStyle?.BorderColor?.ToVector4() ?? new Vector4(0, 0, 0, 1));
                            float optStartX = optionPos.X + 10;
                            float optStartY = optionPos.Y + optionSize.Y / 2 - fontSize / 2;
                            //Console.WriteLine($"UIRenderingLayer: Rendering Option '{dropdown.Options[i]}' at ({optStartX}, {optStartY}), FontSize: {fontSize}, Color: ({textColor.X}, {textColor.Y}, {textColor.Z}, {textColor.W})");
                            _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                            _shaderProgram.SetUniform("uUseTexture", 1.0f);
                            RenderText(dropdown.Options[i], optStartX, optStartY, width, height, fontSize, textColor);
                        }
                    }
                    break;

                case Toggle toggle:
                    RenderButton(pos, toggle.Size, toggle.IsHovered, width, height,
                        toggle.ButtonStyle?.BackgroundColor?.ToVector4() ?? new Vector4(1, 0, 0, 0.8f),
                        toggle.ButtonStyle?.HoverColor?.ToVector4() ?? new Vector4(1, 1, 1, 0.8f),
                        toggle.ButtonStyle?.BorderColor?.ToVector4() ?? new Vector4(0, 0, 0, 1));
                    startX = pos.X + 10;
                    fontSize = toggle.TextStyle?.FontSize ?? 10.0f;
                    startY = pos.Y + toggle.Size.Y / 2 - fontSize / 2;
                    string toggleText = toggle.State ? "On" : "Off";
                    textColor = toggle.TextStyle?.Color?.ToVector4() ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                    //Console.WriteLine($"UIRenderingLayer: Rendering Toggle '{toggleText}' at ({startX}, {startY}), FontSize: {fontSize}, Color: ({textColor.X}, {textColor.Y}, {textColor.Z}, {textColor.W})");
                    RenderText(toggleText, startX, startY, width, height, fontSize, textColor);
                    break;

                case Label label:
                    fontSize = label.TextStyle?.FontSize ?? 10.0f;
                    float labelX = pos.X - 150;
                    float labelY = pos.Y + 50 / 2 - fontSize / 2;
                    textColor = label.TextStyle?.Color?.ToVector4() ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                    //Console.WriteLine($"UIRenderingLayer: Rendering Label '{label.Text}' at ({labelX}, {labelY}), FontSize: {fontSize}, Color: ({textColor.X}, {textColor.Y}, {textColor.Z}, {textColor.W})");
                    RenderText(label.Text, labelX, labelY, width, height, fontSize, textColor);
                    break;

                default:
                    break;
            }
        }

        public void RenderButton(Vector2 pos, Vector2 size, bool isHovered, int width, int height, Vector4 backgroundColor, Vector4 hoverColor, Vector4 borderColor)
        {
            if (size.X <= 0 || size.Y <= 0)
                return;

            _renderContext.BindVertexArray(_buttonVao);
            Vector4 color = isHovered ? hoverColor : backgroundColor;

            float borderSize = 2.0f;
            float leftBorder = 2.0f * (pos.X - borderSize) / width - 1.0f;
            float rightBorder = 2.0f * (pos.X + size.X + borderSize) / width - 1.0f;
            float topBorder = 1.0f - 2.0f * (pos.Y - borderSize) / height;
            float bottomBorder = 1.0f - 2.0f * (pos.Y + size.Y + borderSize) / height;
            float[] borderVertices = new float[]
            {
                leftBorder, bottomBorder, 0.0f, 0.0f,
                rightBorder, bottomBorder, 1.0f, 0.0f,
                rightBorder, topBorder, 1.0f, 1.0f,
                leftBorder, topBorder, 0.0f, 1.0f
            };
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _buttonVbo);
            fixed (float* ptr = borderVertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(borderVertices.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            Matrix4x4 transform = Matrix4x4.Identity;
            _shaderProgram.SetMatrix4("uTransform", transform);
            _shaderProgram.SetUniform("uUseTexture", 0.0f);
            _shaderProgram.SetUniform("uColor", borderColor.X, borderColor.Y, borderColor.Z, borderColor.W);
            _renderContext.DrawArrays(PrimitiveType.TriangleFan, 0, 4);

            float left = 2.0f * pos.X / width - 1.0f;
            float right = 2.0f * (pos.X + size.X) / width - 1.0f;
            float top = 1.0f - 2.0f * pos.Y / height;
            float bottom = 1.0f - 2.0f * (pos.Y + size.Y) / height;
            float[] vertices = new float[]
            {
                left, bottom, 0.0f, 0.0f,
                right, bottom, 1.0f, 0.0f,
                right, top, 1.0f, 1.0f,
                left, top, 0.0f, 1.0f
            };
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _buttonVbo);
            fixed (float* ptr = vertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _shaderProgram.SetMatrix4("uTransform", transform);
            _shaderProgram.SetUniform("uUseTexture", 0.0f);
            _shaderProgram.SetUniform("uColor", color.X, color.Y, color.Z, color.W);
            _renderContext.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            _renderContext.BindVertexArray(0);
        }

        public void RenderText(string text, float startX, float startY, int width, int height, float fontSize = 16.0f, Vector4? textColor = null)
        {
            _textRenderer.RenderText(text, startX, startY, width, height, fontSize, textColor);
        }

        public void EndRender()
        {
            _renderContext.BindVertexArray(0);
            _renderContext.UseProgram(0);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _renderContext.DeleteVertexArray(_buttonVao);
                _renderContext.DeleteBuffer(_buttonVbo);
                _renderContext.DeleteVertexArray(_iconVao);
                _renderContext.DeleteBuffer(_iconVbo);
                _backgroundRenderer.Dispose();
                _textRenderer.Dispose();
                foreach (var texture in _iconTextures.Values)
                {
                    _renderContext.DeleteTexture(texture);
                }
                _shaderProgram?.Dispose();
                _disposed = true;
            }
        }
    }
}