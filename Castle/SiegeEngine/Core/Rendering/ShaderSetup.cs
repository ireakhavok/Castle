//SiegeEngine.Rendering/ShaderSetup.cs
using Silk.NET.OpenGL;
using SiegeEngine.Core.Rendering.Shaders;
using SiegeEngine.Core.Rendering.ContextManagement;

namespace SiegeEngine.Core.Rendering
{
    public static class ShaderSetup
    {
        public static (ShaderProgram pointShader, ShaderProgram waterShader, ShaderProgram gridShader, ShaderProgram modelShader, ShaderProgram animationShader) InitializeShaders(IRenderContext renderContext)
        {
            var pointShader = new ShaderProgram(renderContext, PointShader.VertexShaderSource, PointShader.FragmentShaderSource);
            var waterShader = new ShaderProgram(renderContext, WaterShader.VertexShaderSource, WaterShader.FragmentShaderSource);
            var gridShader = new ShaderProgram(renderContext, SceneShader.VertexShaderSource, SceneShader.FragmentShaderSource);
            var modelShader = new ShaderProgram(renderContext, ModelShader.VertexShaderSource, ModelShader.FragmentShaderSource);
            var animationShader = new ShaderProgram(renderContext, AnimationShader.VertexShaderSource, AnimationShader.FragmentShaderSource);
            return (pointShader, waterShader, gridShader, modelShader, animationShader);
        }
    }
}