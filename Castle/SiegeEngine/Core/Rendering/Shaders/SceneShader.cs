// Folder: SiegeEngine.Core.Rendering.Shaders
// File: SceneShader.cs
using System;

namespace SiegeEngine.Core.Rendering.Shaders
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
            uniform sampler2D uTexture;        // color texture or splat
            uniform sampler2D uMat0;           // material 0 albedo
            uniform sampler2D uMat1;           // material 1 albedo
            uniform sampler2D uMat2;           // material 2 albedo
            uniform sampler2D uMat3;           // material 3 albedo
            uniform int uHasTexture;
            uniform int uIsSplat;              // 1 = use splat weights + 4 materials
            void main() {
                if (uHasTexture == 1) {
                    if (uIsSplat == 1) {
                        vec4 splat = texture(uTexture, vUV);
                        vec4 c0 = texture(uMat0, vUV);
                        vec4 c1 = texture(uMat1, vUV);
                        vec4 c2 = texture(uMat2, vUV);
                        vec4 c3 = texture(uMat3, vUV);
                        FragColor = splat.r * c0 + splat.g * c1 + splat.b * c2 + splat.a * c3;
                    } else {
                        if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
                            FragColor = texture(uTexture, vUV);
                        } else {
                            discard;
                        }
                    }
                } else {
                    FragColor = vColor;
                }
            }";
    }
}