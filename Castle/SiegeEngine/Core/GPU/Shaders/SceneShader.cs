// Folder: SiegeEngine/Core/Rendering/Shaders
// File: SceneShader.cs
using System;

namespace SiegeEngine.Core.GPU.Shaders
{
    public static class SceneShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            layout(location = 2) in vec2 aUV;
            out vec4 vColor;
            out vec2 vUV;
            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProjection;
            void main() {
                gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
                vColor = aColor;
                vUV = aUV;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec4 vColor;
            in vec2 vUV;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            uniform int uHasTexture;
            void main() {
                if (uHasTexture == 1) {
                    if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
                        FragColor = texture(uTexture, vUV);
                    } else {
                        discard; // NO rusty fill, only geo subset gets skin
                    }
                } else {
                    FragColor = vColor; // cyan wireframe
                }
            }";
    }
}