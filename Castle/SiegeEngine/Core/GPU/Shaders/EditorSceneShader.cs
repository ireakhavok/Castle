namespace SiegeEngine.Core.GPU.Shaders
{
    public static class EditorSceneShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            out vec4 vColor;
            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProjection;
            uniform float uOutline;
            void main() {
                vec3 pos = aPosition;
                if (uOutline > 0.5) pos += 0.05 * normalize(pos);
                gl_Position = uProjection * uView * uModel * vec4(pos, 1.0);
                vColor = aColor;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec4 vColor;
            out vec4 FragColor;
            uniform float uOutline;
            void main() {
                if (uOutline > 0.5) FragColor = vec4(0.0, 1.0, 0.0, 1.0);
                else FragColor = vColor;
            }";
    }
}