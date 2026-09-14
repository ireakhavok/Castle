// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: SkyboxShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class SkyboxShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uView;
    row_major float4x4 uProjection;
    row_major float4x4 uOrientation;
    float uVerticalOffset;
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
    float3 vTexCoord : TEXCOORD;
};
VSOut vs(VSIn i)
{
    VSOut o;
    float3 pos = i.aPosition;
    pos.z += uVerticalOffset;
    float4x4 view = uView;
    view[3][0] = 0;
    view[3][1] = 0;
    view[3][2] = 0;
    float4 viewPos = mul(float4(pos, 1.0), view);
    float4 clip = mul(viewPos, uProjection);
    // GL writes clip.xyww (ndc z = 1). D3D depth-clips z >= w, so pull in one ulp.
    o.pos = float4(clip.xy, clip.w * 0.999f, clip.w);
    o.vTexCoord = mul(i.aPosition, (float3x3)uOrientation);
    return o;
}";

        public const string FragmentShaderSource = @"
TextureCube uSkybox : register(t0);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float3 vTexCoord : TEXCOORD;
};
float4 ps(VSOut i) : SV_TARGET
{
    return uSkybox.Sample(Samp, normalize(i.vTexCoord));
}";
    }
}
