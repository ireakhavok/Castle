// Folder: SiegeEngine.Rendering.Shaders
// File: UiShader.cs
namespace SiegeEngine.Rendering.Shaders
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
                FragColor = col;
            }";
    }
}