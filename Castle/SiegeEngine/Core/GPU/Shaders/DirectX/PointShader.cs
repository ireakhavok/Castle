// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: PointShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class PointShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uModel;
    row_major float4x4 uView;
    row_major float4x4 uProjection;
};
struct VSIn { float3 aPosition : POSITION; float4 aColor : COLOR; };
struct VSOut { float4 pos : SV_POSITION; float4 vColor : COLOR; };
VSOut vs(VSIn i)
{
    VSOut o;
    float4 world = mul(float4(i.aPosition, 1.0), uModel);
    float4 viewPos = mul(world, uView);
    float4 clip = mul(viewPos, uProjection);
    clip.z = clip.z * 0.5 + clip.w * 0.5;
    o.pos = clip;
    o.vColor = i.aColor.a > 0.001 ? i.aColor : float4(1, 1, 1, 1);
    return o;
}";

        public const string FragmentShaderSource = @"
struct VSOut { float4 pos : SV_POSITION; float4 vColor : COLOR; };
float4 ps(VSOut i) : SV_TARGET { return i.vColor; }";
    }
}
