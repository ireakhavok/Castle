using SiegeEngine.ContextManagement;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

namespace SiegeEngine.Rendering
{
    public unsafe class BackgroundRenderer : IDisposable
    {
        private readonly IRenderContext _renderContext;
        private readonly IControlContext _controlContext;
        private readonly IntPtr _window;
        private uint _bgVao, _bgVbo, _bgTexture;
        private ShaderProgram _shaderProgram;
        private const int TextureWidth = 1920;
        private const int TextureHeight = 895;
        public BackgroundRenderer(IControlContext controlContext, IntPtr window, IRenderContext renderContext)
        {
            _renderContext = renderContext;
            _controlContext = controlContext;
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
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _bgVbo);
            float[] bgVertices = new float[]
            {
                -1.0f, -1.0f, 0.0f, 1.0f,
                 1.0f, -1.0f, 1.0f, 1.0f,
                 1.0f, 1.0f, 1.0f, 0.0f,
                -1.0f, 1.0f, 0.0f, 0.0f
            };
            fixed (float* ptr = bgVertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(bgVertices.Length * sizeof(float)), ptr, _renderContext.Enums.StaticDraw);
            }
            _renderContext.EnableVertexAttribArray(0);
            _renderContext.VertexAttribPointer(0, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)0);
            _renderContext.EnableVertexAttribArray(1);
            _renderContext.VertexAttribPointer(1, 2, _renderContext.Enums.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, 0);
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
                    _renderContext.PixelStore(_renderContext.Enums.UnpackAlignment, 1);
                    Console.WriteLine("Unpack alignment set to 1");
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, _bgTexture);
                    Console.WriteLine("Texture bound");
                    fixed (byte* pixelPtr = pixelData)
                    {
                        _renderContext.TexImage2D(_renderContext.Enums.Texture2D, 0, _renderContext.Enums.InternalRgb, (uint)bitmap.Width, (uint)bitmap.Height, 0, _renderContext.Enums.PixelRgb, _renderContext.Enums.UnsignedByte, pixelPtr);
                        Console.WriteLine("TexImage2D called");
                    }
                    int error = _renderContext.GetError();
                    if (error != _renderContext.Enums.NoError)
                    {
                        throw new Exception($"OpenGL error after TexImage2D: {error}");
                    }
                    _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMinFilter, _renderContext.Enums.Nearest);
                    _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureMagFilter, _renderContext.Enums.Nearest);
                    _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapS, _renderContext.Enums.ClampToEdge);
                    _renderContext.TexParameter(_renderContext.Enums.Texture2D, _renderContext.Enums.TextureWrapT, _renderContext.Enums.ClampToEdge);
                    _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
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
            _controlContext.GetWindowSize(_window, out int windowWidth, out int windowHeight);
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
            _renderContext.BindBuffer(_renderContext.Enums.ArrayBuffer, _bgVbo);
            fixed (float* ptr = bgVertices)
            {
                _renderContext.BufferData(_renderContext.Enums.ArrayBuffer, (uint)(bgVertices.Length * sizeof(float)), ptr, _renderContext.Enums.DynamicDraw);
            }
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, _bgTexture);
            _shaderProgram.SetUniform("uUseTexture", 1.0f);
            _shaderProgram.SetMatrix4("uTransform", Matrix4x4.Identity);
            _shaderProgram.SetUniform("uColor", 1.0f, 1.0f, 1.0f, 1.0f);
            _renderContext.DrawArrays(_renderContext.Enums.TriangleFan, 0, 4);
            _renderContext.BindTexture(_renderContext.Enums.Texture2D, 0);
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