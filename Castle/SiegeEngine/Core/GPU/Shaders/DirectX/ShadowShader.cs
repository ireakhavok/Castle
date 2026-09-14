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
    float ndcZ = clip.z / max(clip.w, 1e-5);
    // LightVP is GL-style Z in -1..1. Store 0..1 like GL gl_FragDepth.
    // Remap SV_POSITION.z to 0..w so DX does not clip the near half of the volume.
    float z01 = saturate(ndcZ * 0.5 + 0.5);
    o.depth = z01;
    clip.z = z01 * clip.w;
    o.pos = clip;
    return o;
}";

        public const string FragmentShaderSource = @"
struct VSOut { float4 pos : SV_POSITION; float depth : TEXCOORD; };
float4 ps(VSOut i) : SV_TARGET
{
    float stored = saturate(i.depth);
    return float4(stored, stored, stored, 1);
}";
    }
}
