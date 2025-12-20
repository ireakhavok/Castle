// Folder: SiegeEngine
// File: TextShader.cs
namespace SiegeEngine.Core.Rendering.Shaders
{
    public static class TextShader
    {
        public const string VertexShaderSource = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec4 aColor;
layout (location = 2) in vec2 aTexCoord;
uniform mat4 uTransform;
out vec4 vColor;
out vec2 vTexCoord;
void main()
{
    gl_Position = uTransform * vec4(aPosition, 0.0, 1.0);
    vColor = aColor;
    vTexCoord = aTexCoord;
}";
        public const string FragmentShaderSource = @"
#version 330 core
in vec4 vColor;
in vec2 vTexCoord;
uniform sampler2D uTexture;
uniform float uUseTexture;
out vec4 FragColor;
void main()
{
    if (uUseTexture > 0.5) {
        vec4 texColor = texture(uTexture, vTexCoord);
        FragColor = vColor * texColor;
    } else {
        FragColor = vColor;
    }
}";
    }
}