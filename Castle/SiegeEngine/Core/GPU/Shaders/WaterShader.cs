namespace SiegeEngine.Core.GPU.Shaders
{
    public static class WaterShader
    {
        public const string VertexShaderSource = @"            #version 330 core

            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec4 aColor;
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
            out vec4 vColor;
            void main()
            {
                vec3 pos = aPosition;
                pos.y += sin(pos.x * 10.0 + Time) * 0.05;
                gl_Position = Projection * View * vec4(pos, 1.0);
                vColor = vec4(0.0, 0.5, 1.0, 1.0);
            }";

        public const string FragmentShaderSource = @"            #version 330 core

            in vec4 vColor;
            out vec4 FragColor;
            void main()
            {
                FragColor = vColor;
            }";
    }
}
