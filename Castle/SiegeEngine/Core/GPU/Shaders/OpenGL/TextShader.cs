// Folder: SiegeEngine/Core/GPU/Shaders/OpenGL
// File: TextShader.cs
namespace SiegeEngine.Core.GPU.Shaders.OpenGL
{
    public static class TextShader
    {
        public const string VertexShaderSource = @"#version 330 core


layout(std140) uniform UiCB
{
    mat4 Transform;
    vec4 Color;
    vec4 Color1;
    vec4 Color2;
    vec4 BorderRadius;
    vec4 RectSize;
    vec4 BorderColor;
    float UseTexture;
    float UseGradient;
    float UseRounded;
    float GradientFlip;
    int GradientAxis;
    float BorderWidth;
    float Outline;
    float PadUi0;
};
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec4 aColor;
layout (location = 2) in vec2 aTexCoord;
out vec4 vColor;
out vec2 vTexCoord;
void main()
{
    gl_Position = Transform * vec4(aPosition, 0.0, 1.0);
    vColor = aColor;
    vTexCoord = aTexCoord;
}";
        public const string FragmentShaderSource = @"#version 330 core


layout(std140) uniform UiCB
{
    mat4 Transform;
    vec4 Color;
    vec4 Color1;
    vec4 Color2;
    vec4 BorderRadius;
    vec4 RectSize;
    vec4 BorderColor;
    float UseTexture;
    float UseGradient;
    float UseRounded;
    float GradientFlip;
    int GradientAxis;
    float BorderWidth;
    float Outline;
    float PadUi0;
};
in vec4 vColor;
in vec2 vTexCoord;
uniform sampler2D uTexture;
out vec4 FragColor;
void main()
{
    if (UseTexture > 0.5) {
        vec4 texColor = texture(uTexture, vTexCoord);
        FragColor = vColor * texColor;
    } else {
        FragColor = vColor;
    }
}";
    }
}
