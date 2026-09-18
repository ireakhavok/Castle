// Folder: SiegeEngine/Core/GPU/Shaders/OpenGL
// File: SkyboxPreviewShader.cs
namespace SiegeEngine.Core.GPU.Shaders.OpenGL
{
    public static class SkyboxPreviewShader
    {
        public const string VertexShaderSource = @"#version 330 core

layout(location = 0) in vec3 aPosition;
out vec3 vDir;
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
    vDir = aPosition;
    gl_Position = Projection * View * Model * vec4(aPosition, 1.0);
}";

        public const string FragmentShaderSource = @"#version 330 core

in vec3 vDir;
out vec4 FragColor;
uniform samplerCube uSkybox;
void main() {
    FragColor = texture(uSkybox, normalize(vDir));
}";
    }
}
