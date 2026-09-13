// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: ModelShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class ModelShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 Mvp;
    float4 Color;
    float UseTexture;
    float UseSky;
    float2 Pad;
};
struct VSIn
{
    float3 aPosition : POSITION;
    float3 aNormal : NORMAL;
    float2 aTexCoord : TEXCOORD;
    float aMaterialIndex : TEXCOORD1;
    float3 aTangent : TANGENT;
    float4 aBoneIDs : BLENDINDICES;
    float4 aBoneWeights : BLENDWEIGHT;
};
struct VSOut
{
    float4 pos : SV_POSITION;
    float2 vTexCoord : TEXCOORD;
    float3 vNormal : NORMAL;
};
VSOut vs(VSIn i)
{
    VSOut o;
    float4 c = mul(float4(i.aPosition, 1.0), Mvp);
    c.z = c.z * 0.5 + c.w * 0.5;
    o.pos = c;
    o.vTexCoord = i.aTexCoord;
    o.vNormal = i.aNormal;
    return o;
}";

        public const string FragmentShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 Mvp;
    float4 Color;
    float UseTexture;
    float UseSky;
    float2 Pad;
};
Texture2D uAlbedoMap0 : register(t0);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float2 vTexCoord : TEXCOORD;
    float3 vNormal : NORMAL;
};
float4 ps(VSOut i) : SV_TARGET
{
    float4 albedo = uAlbedoMap0.Sample(Samp, i.vTexCoord);
    if (dot(albedo.rgb, albedo.rgb) < 0.0001)
        albedo = float4(0.72, 0.72, 0.74, 1);
    float3 n = normalize(i.vNormal);
    float wrap = 0.35 + 0.65 * saturate(n.z * 0.5 + 0.5);
    float3 lit = albedo.rgb * wrap;
    return float4(lit, 1);
}";
    }
}
