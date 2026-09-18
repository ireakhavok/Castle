//SiegeEngine.Rendering/ShaderSetup.cs
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Shaders
{
    public static class ShaderSetup
    {
        public static (ShaderProgram pointShader, ShaderProgram waterShader, ShaderProgram gridShader, ShaderProgram modelShader, ShaderProgram animationShader) InitializeShaders(IRenderContext renderContext)
        {
            var pointShader = ShaderProgram.FromId(renderContext, ShaderId.Point);
            var waterShader = ShaderProgram.FromId(renderContext, ShaderId.Water);
            var gridShader = ShaderProgram.FromId(renderContext, ShaderId.Grid);
            var modelShader = ShaderProgram.FromId(renderContext, ShaderId.Model);
            var animationShader = ShaderProgram.FromId(renderContext, ShaderId.Animation);
            return (pointShader, waterShader, gridShader, modelShader, animationShader);
        }
    }
}
