// Folder: SiegeEngine.Rendering.Shaders
// File: UiShader.cs
namespace SiegeEngine.Core.Rendering.Shaders
{
    public static class UiShader
    {
        public const string VertexSource = @"
            #version 330 core
            layout(location = 0) in vec2 aPosition;
            layout(location = 1) in vec2 aTexCoord;
            out vec2 vTexCoord;
            uniform mat4 uTransform;
            void main() {
                gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
                vTexCoord = aTexCoord;
            }";
        public const string FragmentSource = @"
            #version 330 core
            in vec2 vTexCoord;
            out vec4 FragColor;
            uniform sampler2D uTexture;
            uniform vec4 uColor;
            uniform float uUseTexture;
            uniform float uUseGradient;
            uniform vec4 uColor1;
            uniform vec4 uColor2;
            uniform int uGradientAxis;
            uniform float uGradientFlip;
            uniform vec4 uBorderRadius;
            uniform vec4 uRectSize;
            uniform float uUseRounded;
            uniform float uBorderWidth;
            uniform vec4 uBorderColor;
            float roundedRect(vec2 p, vec2 b, vec4 r) {
                r.xy = (p.x > 0.0) ? r.xy : r.zw;
                r.x = (p.y > 0.0) ? r.x : r.y;
                vec2 q = abs(p) - b + r.x;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
            }
            void main() {
                vec4 col;
                if (uUseTexture > 0.5) {
                    col = texture(uTexture, vTexCoord) * uColor;
                } else {
                    if (uUseGradient > 0.5) {
                        float t = (uGradientAxis == 0 ? vTexCoord.y : vTexCoord.x);
                        if (uGradientFlip > 0.5) t = 1.0 - t;
                        col = mix(uColor1, uColor2, t);
                    } else {
                        col = uColor;
                    }
                }
                if (uUseRounded > 0.5) {
                    vec2 p = (vTexCoord - vec2(0.5)) * uRectSize.xy;
                    vec2 b = uRectSize.xy * 0.5;
                    float d = roundedRect(p, b, uBorderRadius);
                    if (d > 0.0) discard;
                    vec4 inner_radius = max(vec4(0.0), uBorderRadius - uBorderWidth);
                    vec2 inner_b = max(vec2(0.0), b - uBorderWidth);
                    float inner_d = roundedRect(p, inner_b, inner_radius);
                    if (inner_d > 0.0) {
                        col = uBorderColor;
                    } else {
                        col = uColor;
                    }
                }
                FragColor = col;
            }";
    }
}