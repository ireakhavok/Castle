// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: WaterShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class WaterShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uView;
    row_major float4x4 uProjection;
    float uTime;
    float3 Pad;
};
struct VSIn { float3 aPosition : POSITION; float4 aColor : COLOR; };
struct VSOut { float4 pos : SV_POSITION; float4 vColor : COLOR; };
VSOut vs(VSIn i)
{
    VSOut o;
    float3 pos = i.aPosition;
    pos.y += sin(pos.x * 10.0 + uTime) * 0.05;
    float4 viewPos = mul(float4(pos, 1.0), uView);
    float4 clip = mul(viewPos, uProjection);
    clip.z = clip.z * 0.5 + clip.w * 0.5;
    o.pos = clip;
    o.vColor = float4(0.0, 0.5, 1.0, 1.0);
    return o;
}";

        public const string FragmentShaderSource = @"
struct VSOut { float4 pos : SV_POSITION; float4 vColor : COLOR; };
float4 ps(VSOut i) : SV_TARGET { return i.vColor; }";
    }
}
