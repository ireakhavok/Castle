// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: ShadowShader.cs
// Depth-only cascade / point-shadow counterpart of Lighting/ShadowShaders.cs.
// Compiled when the shadow pass is flushed to a square depth RT.
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
struct VSIn { float3 aPosition : POSITION; float2 aTexCoord : TEXCOORD; };
struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
VSOut vs(VSIn i)
{
    VSOut o;
    float4 world = mul(float4(i.aPosition, 1.0), uModel);
    o.pos = mul(world, uLightVP);
    o.uv = i.aTexCoord;
    return o;
}";

        public const string FragmentShaderSource = @"
Texture2D uOpacityMap : register(t0);
SamplerState Samp : register(s0);
struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
float4 ps(VSOut i) : SV_TARGET { return float4(1, 1, 1, 1); }";
    }
}
