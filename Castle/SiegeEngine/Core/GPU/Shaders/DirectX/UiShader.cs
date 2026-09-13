// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: UiShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class UiShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    float4 Color;
    float UseTexture;
    float UseRounded;
    float BorderWidth;
    float Pad0;
    float4 BorderRadius;
    float4 RectSize;
    float4 BorderColor;
};
struct VSIn { float2 pos : POSITION; float2 uv : TEXCOORD; };
struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };
VSOut vs(VSIn i)
{
    VSOut o;
    o.pos = float4(i.pos, 0, 1);
    o.uv = i.uv;
    return o;
}";

        public const string FragmentShaderSource = @"
cbuffer CB : register(b0)
{
    float4 Color;
    float UseTexture;
    float UseRounded;
    float BorderWidth;
    float Pad0;
    float4 BorderRadius;
    float4 RectSize;
    float4 BorderColor;
};
Texture2D Tex : register(t0);
SamplerState Samp : register(s0);
struct VSOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD; };

float roundedRect(float2 p, float2 b, float4 r)
{
    r.xy = (p.x > 0.0) ? r.xy : r.zw;
    r.x = (p.y > 0.0) ? r.x : r.y;
    float2 q = abs(p) - b + r.x;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r.x;
}

float4 ps(VSOut i) : SV_TARGET
{
    float4 col;
    if (UseTexture > 0.5)
        col = Tex.Sample(Samp, i.uv) * Color;
    else
        col = Color;
    // Glyph atlas UVs are not 0-1 across the quad. Never run SDF on text.
    if (UseRounded > 0.5 && RectSize.x > 1.0 && RectSize.y > 1.0)
    {
        float2 p = (i.uv - float2(0.5, 0.5)) * RectSize.xy;
        float2 b = RectSize.xy * 0.5;
        float d = roundedRect(p, b, BorderRadius);
        if (d > 0.0) discard;
        if (BorderWidth > 0.0)
        {
            float4 inner_radius = max(float4(0, 0, 0, 0), BorderRadius - BorderWidth);
            float2 inner_b = max(float2(0, 0), b - BorderWidth);
            float inner_d = roundedRect(p, inner_b, inner_radius);
            if (inner_d > 0.0)
                col = BorderColor;
            else if (UseTexture < 0.5)
                col = Color;
        }
    }
    return col;
}";
    }
}
