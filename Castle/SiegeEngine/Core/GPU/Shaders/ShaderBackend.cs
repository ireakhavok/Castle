// Folder: SiegeEngine/Core/GPU/Shaders
// File: ShaderBackend.cs
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Shaders
{
    /// <summary>
    /// Context-selected shader language. OpenGL keeps the GLSL constants on
    /// the existing shader classes. DirectX sources live under Shaders/DirectX/.
    /// </summary>
    public static class ShaderBackend
    {
        public static bool IsHlsl(IRenderContext ctx)
        {
            if (ctx == null) return false;
            string name = ctx.GetType().Name;
            return name.IndexOf("D3D", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("DirectX", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string Vertex(IRenderContext ctx, string glsl, string hlsl)
        {
            return IsHlsl(ctx) ? hlsl : glsl;
        }

        public static string Fragment(IRenderContext ctx, string glsl, string hlsl)
        {
            return IsHlsl(ctx) ? hlsl : glsl;
        }

        public static (string vs, string fs) Map(string vertexSource, string fragmentSource)
        {
            string vs = vertexSource ?? "";
            string fs = fragmentSource ?? "";
            // Do NOT key off samplerCube — terrain and model GLSL both declare
            // samplerCube for point-shadows. That remapped every world program
            // onto the skybox clip.xyww path (streaks, no terrain/meshes).
            if (vs.IndexOf("uLightVP", System.StringComparison.Ordinal) >= 0)
                return (DirectX.ShadowShader.VertexShaderSource, DirectX.ShadowShader.FragmentShaderSource);
            if (fs.IndexOf("uSkybox", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("uOrientation", System.StringComparison.Ordinal) >= 0)
                return (DirectX.SkyboxShader.VertexShaderSource, DirectX.SkyboxShader.FragmentShaderSource);
            if (vs.IndexOf("uAlbedoMap", System.StringComparison.Ordinal) >= 0
                || fs.IndexOf("uAlbedoMap", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("uBoneTransforms", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("uBoneMatrices", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("aBoneIDs", System.StringComparison.Ordinal) >= 0)
                return (DirectX.ModelShader.VertexShaderSource, DirectX.ModelShader.FragmentShaderSource);
            if (fs.IndexOf("uUnlit", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("uPolyFactor", System.StringComparison.Ordinal) >= 0
                || fs.IndexOf("uPolyUnits", System.StringComparison.Ordinal) >= 0)
                return (DirectX.TerrainShader.VertexShaderSource, DirectX.TerrainShader.FragmentShaderSource);
            if (fs.IndexOf("uUseRounded", System.StringComparison.Ordinal) >= 0
                || fs.IndexOf("uBorderRadius", System.StringComparison.Ordinal) >= 0)
                return (DirectX.UiShader.VertexShaderSource, DirectX.UiShader.FragmentShaderSource);
            if (vs.IndexOf("uPointSize", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("uOutline", System.StringComparison.Ordinal) >= 0)
                return (DirectX.PointShader.VertexShaderSource, DirectX.PointShader.FragmentShaderSource);
            if (vs.IndexOf("uTime", System.StringComparison.Ordinal) >= 0 && vs.IndexOf("sin(", System.StringComparison.Ordinal) >= 0)
                return (DirectX.WaterShader.VertexShaderSource, DirectX.WaterShader.FragmentShaderSource);
            if (fs.IndexOf("uHasTexture", System.StringComparison.Ordinal) >= 0)
                return (DirectX.SceneShader.VertexShaderSource, DirectX.SceneShader.FragmentShaderSource);
            if (vs.IndexOf("aTexCoord", System.StringComparison.Ordinal) >= 0 && fs.IndexOf("uTexture", System.StringComparison.Ordinal) >= 0)
                return (DirectX.SpriteShader.VertexShaderSource, DirectX.SpriteShader.FragmentShaderSource);
            return (vs, fs);
        }
    }
}
