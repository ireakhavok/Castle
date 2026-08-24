// Folder: SiegeEngine/Core/Rendering/Shaders
// File: SpriteShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class SpriteShader
    {
        public const string VertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec4 aColor;
            layout(location = 2) in vec2 aTexCoord;

            out vec4 vColor;
            out vec2 vTexCoord;

            uniform mat4 uModel;
            uniform mat4 uView;
            uniform mat4 uProjection;

            void main()
            {
                gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
                vColor = aColor;
                vTexCoord = aTexCoord;
            }";

        public const string FragmentShaderSource = @"
            #version 330 core
            in vec4 vColor;
            in vec2 vTexCoord;

            out vec4 FragColor;

            uniform sampler2D uTexture;

            void main()
            {
                vec4 texColor = texture(uTexture, vTexCoord);
                FragColor = texColor * vColor;
            }";
    }
}