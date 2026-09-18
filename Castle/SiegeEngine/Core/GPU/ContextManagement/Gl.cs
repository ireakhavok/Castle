// Folder: SiegeEngine/Core/GPU/ContextManagement
// File: Gl.cs
using System;

namespace SiegeEngine.Core.GPU.ContextManagement
{
    public static class Gl
    {
        public static OpenGLRenderContext Of(IRenderContext context)
        {
            if (context is OpenGLRenderContext gl)
                return gl;
            throw new InvalidOperationException("This path requires the OpenGL backend.");
        }
    }
}
