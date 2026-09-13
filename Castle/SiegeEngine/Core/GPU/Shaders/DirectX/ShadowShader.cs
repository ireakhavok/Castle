// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: ShadowShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class ShadowShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uLightVP;
    row_major float4x4 uModel;
};
struct VSIn { float3 aPosition : POSITION; };
struct VSOut { float4 pos : SV_POSITION; float depth : TEXCOORD; };
VSOut vs(VSIn i)
{
    VSOut o;
    float4 world = mul(float4(i.aPosition, 1.0), uModel);
    float4 clip = mul(world, uLightVP);
    o.pos = clip;
    o.depth = clip.z / max(clip.w, 1e-5);
    return o;
}";

        public const string FragmentShaderSource = @"
struct VSOut { float4 pos : SV_POSITION; float depth : TEXCOORD; };
float4 ps(VSOut i) : SV_TARGET
{
    float stored = saturate(i.depth * 0.5 + 0.5);
    return float4(stored, stored, stored, 1);
}";
    }
}
