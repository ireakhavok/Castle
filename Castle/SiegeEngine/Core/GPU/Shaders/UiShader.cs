// Folder: SiegeEngine.Rendering.Shaders
// File: UiShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class UiShader
    {
        public const string VertexSource = @"            #version 330 core
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

            layout(location = 0) in vec2 aPosition;
            layout(location = 1) in vec2 aTexCoord;
            out vec2 vTexCoord;
            void main() {
                gl_Position = Transform * vec4(aPosition, 0.0, 1.0);
                vTexCoord = aTexCoord;
            }";
        public const string FragmentSource = @"            #version 330 core
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

            in vec2 vTexCoord;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            float roundedRect(vec2 p, vec2 b, vec4 r) {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x = (p.y > 0.0) ? r.x : r.y;
                vec2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }
            void main() {
                vec4 col;
                if (UseTexture > 0.5) {
                    col = texture(uTexture, vTexCoord) * Color;
                } else {
                    if (UseGradient > 0.5) {
                        float t = (GradientAxis == 0 ? vTexCoord.y : vTexCoord.x);
                        if (GradientFlip > 0.5) t = 1.0 - t;
                        col = mix(Color1, Color2, t);
                    } else {
                        col = Color;
                    }
                }
                if (UseRounded > 0.5) {
                    vec2 p = (vTexCoord - vec2(0.5)) * RectSize.xy;
                    vec2 b = RectSize.xy * 0.5;
                    float d = roundedRect(p, b, BorderRadius);
                    if (d > 0.0) discard;
                    vec4 inner_radius = max(vec4(0.0), BorderRadius - BorderWidth);
                    vec2 inner_b = max(vec2(0.0), b - BorderWidth);
                    float inner_d = roundedRect(p, inner_b, inner_radius);
                    if (inner_d > 0.0) {
                        col = BorderColor;
                    } else {
                        col = Color;
                    }
                }
                FragColor = col;
            }";
    }
}