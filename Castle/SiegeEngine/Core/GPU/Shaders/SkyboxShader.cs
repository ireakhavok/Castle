// Folder: SiegeEngine/Core/Rendering/Shaders
// File: SkyboxShader.cs
using System;

namespace SiegeEngine.Core.GPU.Shaders
{
    public static class SkyboxShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            out vec3 vTexCoord;
            uniform mat4 uView;
            uniform mat4 uProjection;
            void main() {
                vec4 pos = uProjection * uView * vec4(aPosition, 1.0);
                gl_Position = pos.xyww;
                vTexCoord = aPosition;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec3 vTexCoord;
            out vec4 FragColor;
            uniform samplerCube uSkybox;
            void main() {
                FragColor = texture(uSkybox, normalize(vTexCoord));
            }";
    }
}