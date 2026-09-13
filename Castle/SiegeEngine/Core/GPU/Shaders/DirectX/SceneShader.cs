// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: SceneShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class SceneShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uModel;
    row_major float4x4 uView;
    row_major float4x4 uProjection;
    float uHasTexture;
    float3 Pad;
};
struct VSIn
{
    float3 aPosition : POSITION;
    float4 aColor : COLOR;
    float2 aUV : TEXCOORD;
};
struct VSOut
{
    float4 pos : SV_POSITION;
    float4 vColor : COLOR;
    float2 vUV : TEXCOORD;
};
VSOut vs(VSIn i)
{
    VSOut o;
    float4 world = mul(float4(i.aPosition, 1.0), uModel);
    float4 viewPos = mul(world, uView);
    float4 clip = mul(viewPos, uProjection);
    clip.z = clip.z * 0.5 + clip.w * 0.5;
    o.pos = clip;
    o.vColor = i.aColor;
    o.vUV = i.aUV;
    return o;
}";

        public const string FragmentShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uModel;
    row_major float4x4 uView;
    row_major float4x4 uProjection;
    float uHasTexture;
    float3 Pad;
};
Texture2D uTexture : register(t0);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float4 vColor : COLOR;
    float2 vUV : TEXCOORD;
};
float4 ps(VSOut i) : SV_TARGET
{
    if (uHasTexture > 0.5)
    {
return uTexture.Sample(Samp, i.vUV);
    }
    return i.vColor.a > 0.001 ? i.vColor : float4(1, 1, 1, 1);
}";
    }
}
