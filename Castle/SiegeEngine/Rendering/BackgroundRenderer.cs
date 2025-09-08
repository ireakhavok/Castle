using System;
using Silk.NET.GLFW;
using System.Numerics;
using System.Drawing;
using System.Drawing.Imaging;
using Silk.NET.OpenGL;
using SiegeEngine.ContextManagement;
namespace SiegeEngine.Rendering
{
    public unsafe class BackgroundRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly Glfw _glfw;
        private readonly WindowHandle* _window;
        private uint _bgVao, _bgVbo, _bgTexture;
        private ShaderProgram _shaderProgram;
        private const int TextureWidth = 1920;
        private const int TextureHeight = 895;
        public BackgroundRenderer(Glfw glfw, IRenderContext renderContext, WindowHandle* window)
        {
            _renderContext = renderContext;
            _glfw = glfw;
            _window = window;
        }
        public void Initialize(string backgroundPath, ShaderProgram shaderProgram)
        {
            _shaderProgram = shaderProgram;
            // Background VAO/VBO/Texture
            _renderContext.GenVertexArrays(1, out _bgVao);
            _renderContext.GenBuffers(1, out _bgVbo);
            _renderContext.GenTextures(1, out _bgTexture);
            _renderContext.BindVertexArray(_bgVao);
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _bgVbo);
            float[] bgVertices = new float[]
            {
                -1.0f, -1.0f, 0.0f, 1.0f,
                 1.0f, -1.0f, 1.0f, 1.0f,
                 1.0f, 1.0f, 1.0f, 0.0f,
                -1.0f, 1.0f, 0.0f, 0.0f
            };
            fixed (float* ptr = bgVertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(bgVertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _renderContext.BindVertexArray(0);
            // Load background texture
            Console.WriteLine($"Attempting to load background texture from: {backgroundPath}");
            try
            {
                using (var bitmap = new Bitmap(backgroundPath))
                {
                    Console.WriteLine($"Bitmap loaded: {bitmap.Width}x{bitmap.Height}, PixelFormat: {bitmap.PixelFormat}");
                    if (bitmap.Width != TextureWidth || bitmap.Height != TextureHeight)
                    {
                        throw new Exception($"Image dimensions must be {TextureWidth}x{TextureHeight}");
                    }
                    BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    Console.WriteLine($"Bitmap locked, Stride: {data.Stride}");
                    int bytesPerPixel = 3;
                    byte[] pixelData = new byte[bitmap.Width * bitmap.Height * bytesPerPixel];
                    nint ptr = data.Scan0;
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        nint row = nint.Add(ptr, y * data.Stride);
                        System.Runtime.InteropServices.Marshal.Copy(row, pixelData, y * bitmap.Width * bytesPerPixel, bitmap.Width * bytesPerPixel);
                    }
                    Console.WriteLine("Pixel data copied to managed array");
                    // Log some pixel data to verify
                    Console.WriteLine($"Sample pixel data (first 12 bytes): {BitConverter.ToString(pixelData, 0, 12)}");
                    _renderContext.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                    Console.WriteLine("Unpack alignment set to 1");
                    _renderContext.BindTexture(TextureTarget.Texture2D, _bgTexture);
                    Console.WriteLine("Texture bound");
                    fixed (byte* pixelPtr = pixelData)
                    {
                        _renderContext.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgb, (uint)bitmap.Width, (uint)bitmap.Height, 0, GLEnum.Rgb, GLEnum.UnsignedByte, pixelPtr);
                        Console.WriteLine("TexImage2D called");
                    }
                    GLEnum error = _renderContext.GetError();
                    if (error != GLEnum.NoError)
                    {
                        throw new Exception($"OpenGL error after TexImage2D: {error}");
                    }
                    _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
                    _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
                    _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
                    _renderContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
                    _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                    Console.WriteLine($"JPG texture loaded: {_bgTexture}");
                    bitmap.UnlockBits(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load JPG: {ex.Message}");
                _bgTexture = 0;
            }
        }
        public void Render()
        {
            if (_bgTexture == 0) return;
            _renderContext.BindVertexArray(_bgVao);
            int windowWidth, windowHeight;
            _glfw.GetWindowSize(_window, out windowWidth, out windowHeight);
            // Render the background at its exact resolution (1920x895), centered in the window
            float offsetX = (windowWidth - TextureWidth) / 2.0f;
            float offsetY = (windowHeight - TextureHeight) / 2.0f;
            // Map the texture directly to window coordinates (1:1 pixel mapping)
            float left = offsetX;
            float right = offsetX + TextureWidth;
            float top = offsetY;
            float bottom = offsetY + TextureHeight;
            // Convert to normalized device coordinates (NDC)
            float leftNDC = left / windowWidth * 2.0f - 1.0f;
            float rightNDC = right / windowWidth * 2.0f - 1.0f;
            float topNDC = 1.0f - top / windowHeight * 2.0f;
            float bottomNDC = 1.0f - bottom / windowHeight * 2.0f;
            float[] bgVertices = new float[]
            {
                leftNDC, bottomNDC, 0.0f, 1.0f,
                rightNDC, bottomNDC, 1.0f, 1.0f,
                rightNDC, topNDC, 1.0f, 0.0f,
                leftNDC, topNDC, 0.0f, 0.0f
            };
            _renderContext.BindBuffer(BufferTargetARB.ArrayBuffer, _bgVbo);
            fixed (float* ptr = bgVertices)
            {
                _renderContext.BufferData(BufferTargetARB.ArrayBuffer, (uint)(bgVertices.Length * sizeof(float)), ptr, BufferUsageARB.DynamicDraw);
            }
            _renderContext.BindTexture(TextureTarget.Texture2D, _bgTexture);
            _shaderProgram.SetUniform("uUseTexture", 1.0f);
            _shaderProgram.SetMatrix4("uTransform", Matrix4x4.Identity);
            _shaderProgram.SetUniform("uColor", 1.0f, 1.0f, 1.0f, 1.0f);
            _renderContext.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
            _renderContext.BindTexture(TextureTarget.Texture2D, 0);
            _renderContext.BindVertexArray(0);
        }
        public void Dispose()
        {
            _renderContext.DeleteVertexArray(_bgVao);
            _renderContext.DeleteBuffer(_bgVbo);
            _renderContext.DeleteTexture(_bgTexture);
        }
    }
}