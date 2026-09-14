// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: AntiAliasingShaders.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class AntiAliasingShaders
    {
        public const string FullscreenVertex = @"
struct VSOut { float4 pos : SV_POSITION; float2 vUv : TEXCOORD; };
VSOut vs(uint id : SV_VertexID)
{
    VSOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.vUv = uv;
    o.pos = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}";

        public const string CopyFragment = @"
Texture2D uColor : register(t0);
SamplerState Samp : register(s0);
float4 ps(float4 pos : SV_POSITION, float2 vUv : TEXCOORD) : SV_TARGET
{
    return uColor.Sample(Samp, vUv);
}";

        public const string FxaaFragment = @"
cbuffer CB : register(b0) { float2 uInvResolution; float2 Pad; };
Texture2D uColor : register(t0);
SamplerState Samp : register(s0);
float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
float4 ps(float4 pos : SV_POSITION, float2 vUv : TEXCOORD) : SV_TARGET
{
    float2 rcp = uInvResolution;
    float3 rgbM = uColor.Sample(Samp, vUv).rgb;
    return float4(rgbM, 1);
}";

        public const string SmaaEdgeFragment = CopyFragment;
        public const string SmaaWeightFragment = CopyFragment;
        public const string SmaaBlendFragment = CopyFragment;
        public const string TaaFragment = CopyFragment;
    }
}
