// Folder: SiegeEngine.Core.GPU.Shaders
// File: PointShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class PointShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            out vec4 vColor;
            layout(std140) uniform FrameCB
            {
                mat4 View;
                mat4 Projection;
                vec4 ViewPos;
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
                float Pad2;
                float Pad3;
                float Pad4;
            };
            void main()
            {
                gl_Position = Projection * View * Model * vec4(aPosition, 1.0);
                vColor = aColor;
                gl_PointSize = PointSize;
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
