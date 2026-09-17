// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticIdShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticIdShader
    {
        public const string VertexSource = @"#version 330 core


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
layout(location = 0) in vec3 aPosition;
layout(location = 1) in int aTriangleIndex;
flat out int vTriangleIndex;
void main()
{
    gl_Position = Projection * View * vec4(aPosition, 1.0);
    vTriangleIndex = aTriangleIndex;
}";

        public const string FragmentSource = @"#version 330 core


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
flat in int vTriangleIndex;
out uvec4 FragColor;
void main()
{
    // Store triangle index + 1 so 0 means empty
    FragColor = uvec4(uint(vTriangleIndex + 1), 0u, 0u, 1u);
}";
    }
}