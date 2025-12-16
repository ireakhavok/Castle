namespace SiegeEngine.Core.Rendering.Shaders
{
    public static class WaterShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec4 aColor;
            uniform mat4 uProjection;
            uniform mat4 uView;
            uniform float uTime;
            out vec4 vColor;
            void main()
            {
                vec3 pos = aPosition;
                pos.y += sin(pos.x * 10.0 + uTime) * 0.05;
                gl_Position = uProjection * uView * vec4(pos, 1.0);
                vColor = vec4(0.0, 0.5, 1.0, 1.0);
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