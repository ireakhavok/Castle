namespace SiegeEngine.Rendering.Shaders
{
    public static class PointShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec4 aColor;
            uniform mat4 uProjection;
            uniform mat4 uView;
            out vec4 vColor;
            void main()
            {
                gl_Position = uProjection * uView * vec4(aPosition, 1.0);
                vColor = aColor;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec4 vColor;
            out vec4 FragColor;
            void main()
            {
                FragColor = vColor;
            }";
    }
}