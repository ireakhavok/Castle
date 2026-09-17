namespace SiegeEngine.Core.GPU.Shaders
{
    public static class EditorSceneShader
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

            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            out vec4 vColor;
            void main() {
                vec3 pos = aPosition;
                if (Outline > 0.5) pos += 0.05 * normalize(pos);
                gl_Position = Projection * View * Model * vec4(pos, 1.0);
                vColor = aColor;
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
            out vec4 FragColor;
            void main() {
                if (Outline > 0.5) FragColor = vec4(0.0, 1.0, 0.0, 1.0);
                else FragColor = vColor;
            }";
    }
}