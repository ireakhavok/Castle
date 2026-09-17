// Folder: SiegeEngine/Core/Rendering/Shaders
// File: SceneShader.cs
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
            layout(std140) uniform FrameCB
            {
                mat4 View;
                mat4 Projection;
                vec4 ViewPos;
                float Time;
                int HasTexture;
                float PadFrame0;
                float PadFrame1;
            };
            layout(std140) uniform ObjectCB
            {
                mat4 Model;
                mat4 NormalMatrix;
                int HasBones;
                int ReceiveShadows;
                int Pad0;
                int Pad1;
                float PointSize;
                float VerticalOffset;
                float Pad3;
                float Pad4;
            };
            void main() {
                gl_Position = Projection * View * Model * vec4(aPosition, 1.0);
                vColor = aColor;
                vUV = aUV;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec4 vColor;
            in vec2 vUV;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            layout(std140) uniform FrameCB
            {
                mat4 View;
                mat4 Projection;
                vec4 ViewPos;
                float Time;
                int HasTexture;
                float PadFrame0;
                float PadFrame1;
            };
            void main() {
                if (HasTexture == 1) {
                    if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
                        FragColor = texture(uTexture, vUV);
                    } else {
                        discard;
                    }
                } else {
                    FragColor = vColor;
                }
            }";
    }
}
