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
            void main() {
                if (uUseTexture > 0.5) {
                    FragColor = texture(uTexture, vTexCoord) * uColor;
                } else {
                    FragColor = uColor;
                }
            }";
    }
}