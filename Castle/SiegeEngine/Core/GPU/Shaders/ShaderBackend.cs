// Folder: SiegeEngine/Core/GPU/Shaders
// File: ShaderBackend.cs
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Shaders
{
    /// <summary>
    /// Single place renderers ask for shader source. OpenGL constants stay on
    /// the existing classes. DirectX constants live under Shaders/DirectX/.
    /// ShaderProgram.Map uses the same table so GLSL constructors still work.
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

        public static (string vs, string fs) Skybox(IRenderContext ctx)
        {
            return (
                Vertex(ctx, OpenGL.SkyboxShader.VertexShaderSource, DirectX.SkyboxShader.VertexShaderSource),
                Fragment(ctx, OpenGL.SkyboxShader.FragmentShaderSource, DirectX.SkyboxShader.FragmentShaderSource));
        }

        public static (string vs, string fs) Model(IRenderContext ctx)
        {
            return (
                Vertex(ctx, OpenGL.ModelShader.VertexShaderSource, DirectX.ModelShader.VertexShaderSource),
                Fragment(ctx, OpenGL.ModelShader.FragmentShaderSource, DirectX.ModelShader.FragmentShaderSource));
        }

        public static (string vs, string fs) Terrain(IRenderContext ctx)
        {
            // TerrainShader lives in the parent Shaders namespace (file is under OpenGL/).
            return (
                Vertex(ctx, TerrainShader.VertexShaderSource, DirectX.TerrainShader.VertexShaderSource),
                Fragment(ctx, TerrainShader.FragmentShaderSource, DirectX.TerrainShader.FragmentShaderSource));
        }

        public static (string vs, string fs) Ui(IRenderContext ctx)
        {
            return (
                Vertex(ctx, OpenGL.UiShader.VertexSource, DirectX.UiShader.VertexShaderSource),
                Fragment(ctx, OpenGL.UiShader.FragmentSource, DirectX.UiShader.FragmentShaderSource));
        }

        public static (string vs, string fs) Map(string vertexSource, string fragmentSource)
        {
            string vs = vertexSource ?? "";
            string fs = fragmentSource ?? "";
            if (AlreadyHlsl(vs) || AlreadyHlsl(fs))
                return (vs, fs);
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
            if (fs.IndexOf("uInvResolution", System.StringComparison.Ordinal) >= 0
                || vs.IndexOf("gl_VertexID", System.StringComparison.Ordinal) >= 0)
                return (DirectX.AntiAliasingShaders.FullscreenVertex, DirectX.AntiAliasingShaders.CopyFragment);
            if (fs.IndexOf("uFogDensity", System.StringComparison.Ordinal) >= 0
                || fs.IndexOf("uFogColor", System.StringComparison.Ordinal) >= 0)
                return (DirectX.FogShaders.FullscreenVertex, DirectX.FogShaders.VolumetricFragment);
            return (vs, fs);
        }

        static bool AlreadyHlsl(string src)
        {
            if (string.IsNullOrEmpty(src)) return false;
            return src.IndexOf("SV_POSITION", System.StringComparison.OrdinalIgnoreCase) >= 0
                || src.IndexOf("SV_TARGET", System.StringComparison.OrdinalIgnoreCase) >= 0
                || src.IndexOf("register(b", System.StringComparison.Ordinal) >= 0;
        }
    }
}
