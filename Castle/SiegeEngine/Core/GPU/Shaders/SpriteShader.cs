// Folder: SiegeEngine/Core/Rendering/Shaders
// File: SpriteShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class SpriteShader
    {
        public const string VertexShaderSource = @"            #version 330 core
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

            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            layout(location = 2) in vec2 aTexCoord;

            out vec4 vColor;
            out vec2 vTexCoord;


            void main()
            {
                gl_Position = Projection * View * Model * vec4(aPosition, 1.0);
                vColor = aColor;
                vTexCoord = aTexCoord;
            }";

        public const string FragmentShaderSource = @"            #version 330 core
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

            in vec4 vColor;
            in vec2 vTexCoord;

            out vec4 FragColor;

            uniform sampler2D uTexture;

            void main()
            {
                vec4 texColor = texture(uTexture, vTexCoord);
                FragColor = texColor * vColor;
            }";
    }
}