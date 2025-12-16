namespace SiegeEngine.Core.Rendering.Shaders
{
    public static class SceneShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            out vec4 vColor;
            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProjection;
            void main() {
                gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
                vColor = aColor;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec4 vColor;
            out vec4 FragColor;
            void main() {
                FragColor = vColor;
            }";
    }
}