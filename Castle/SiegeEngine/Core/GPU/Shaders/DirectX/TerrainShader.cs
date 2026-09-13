// Folder: SiegeEngine/Core/GPU/Shaders/DirectX
// File: TerrainShader.cs
namespace SiegeEngine.Core.GPU.Shaders.DirectX
{
    public static class TerrainShader
    {
        public const string VertexShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 uModel;
    row_major float4x4 uView;
    row_major float4x4 uProjection;
    float4 uLightDir;
    float4 uLightColor;
    float4 uAmbientColor;
    float uHasTexture;
    float uUnlit;
    float uLightIntensity;
    float uAmbientStrength;
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
    float3 vWorldPos : TEXCOORD1;
};
VSOut vs(VSIn i)
{
    VSOut o;
    float4 world = mul(float4(i.aPosition, 1.0), uModel);
    o.vWorldPos = world.xyz;
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
    float4 uLightDir;
    float4 uLightColor;
    float4 uAmbientColor;
    float uHasTexture;
    float uUnlit;
    float uLightIntensity;
    float uAmbientStrength;
};
Texture2D uTexture : register(t0);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float4 vColor : COLOR;
    float2 vUV : TEXCOORD;
    float3 vWorldPos : TEXCOORD1;
};
float4 ps(VSOut i) : SV_TARGET
{
    if (uUnlit > 0.5)
        return float4(0.486, 1.0, 0.796, 1.0);
    float4 albedo = i.vColor;
    if (uHasTexture > 0.5)
    {
        if (i.vUV.x < 0.0 || i.vUV.x > 1.0 || i.vUV.y < 0.0 || i.vUV.y > 1.0)
            discard;
        albedo = uTexture.Sample(Samp, i.vUV);
    }
    float3 dx = ddx(i.vWorldPos);
    float3 dy = ddy(i.vWorldPos);
    float3 normal = normalize(cross(dx, dy));
    if (dot(normal, normal) < 0.001)
        normal = float3(0, 0, 1);
    float3 ldir = uLightDir.xyz;
    if (dot(ldir, ldir) < 0.0001)
        ldir = float3(-0.85, 0.10, -0.52);
    float3 lightDir = normalize(-ldir);
    // D3D pixel-origin flips ddy vs GL. Two-sided NdotL keeps the heightmap
    // as bright as the GL one-sided path instead of lighting the underside.
    float diff = abs(dot(normal, lightDir));
    float amb = uAmbientStrength;
    if (amb <= 0.0) amb = 0.30;
    float3 ambientCol = uAmbientColor.xyz;
    if (dot(ambientCol, ambientCol) < 0.0001)
        ambientCol = float3(0.45, 0.45, 0.48);
    float3 lightCol = uLightColor.xyz;
    if (dot(lightCol, lightCol) < 0.0001)
        lightCol = float3(1, 1, 1);
    float intensity = uLightIntensity;
    if (intensity <= 0.0) intensity = 1.0;
    float3 lit = amb * albedo.rgb * ambientCol + diff * albedo.rgb * lightCol * intensity;
    return float4(lit, albedo.a);
}";
    }
}
