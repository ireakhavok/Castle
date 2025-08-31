using Silk.NET.OpenGL;
using SiegeEngine.Rendering.Shaders;
using SiegeEngine.Interfaces;

namespace SiegeEngine.Rendering
{
    public static class ShaderSetup
    {
        public static (ShaderProgram pointShader, ShaderProgram waterShader, ShaderProgram gridShader, ShaderProgram modelShader) InitializeShaders(IRenderContext renderContext)
        {
            var pointShader = new ShaderProgram(renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            var waterShader = new ShaderProgram(renderContext, WaterShader.VertexShaderSource, WaterShader.FragmentShaderSource);
            var gridShader = new ShaderProgram(renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            var modelShader = new ShaderProgram(renderContext, ModelShader.VertexShaderSource, ModelShader.FragmentShaderSource);

            return (pointShader, waterShader, gridShader, modelShader);
        }
    }
}