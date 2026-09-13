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
    row_major float4x4 CascadeVP;
    float ShadowsEnabled;
    float3 PadS;
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
    row_major float4x4 CascadeVP;
    float ShadowsEnabled;
    float3 PadS;
};
Texture2D uTexture : register(t0);
Texture2D uShadowAtlas : register(t2);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float4 vColor : COLOR;
    float2 vUV : TEXCOORD;
    float3 vWorldPos : TEXCOORD1;
};
float SampleSunShadow(float3 worldPos)
{
    if (ShadowsEnabled < 0.5) return 1.0;
    float4 clip = mul(float4(worldPos, 1.0), CascadeVP);
    float3 proj = clip.xyz / max(clip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    if (proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0) return 1.0;
    if (proj.z < 0.0 || proj.z > 1.0) return 1.0;
    float2 atlasUv = proj.xy * 0.5;
    atlasUv = clamp(atlasUv, 0.001, 0.499);
    float stored = uShadowAtlas.Sample(Samp, atlasUv).r;
    float dz = max(proj.z - stored, 0.0);
    float vis = exp(-40.0 * dz);
    return lerp(0.08, 1.0, saturate(vis));
}
float4 ps(VSOut i) : SV_TARGET
{
    if (uUnlit > 0.5)
        return float4(0.486, 1.0, 0.796, 1.0);
    float3 rgb = i.vColor.rgb;
    if (uHasTexture > 0.5)
        rgb = uTexture.Sample(Samp, i.vUV).rgb;
    float3 dx = ddx(i.vWorldPos);
    float3 dy = ddy(i.vWorldPos);
    float3 normal = normalize(cross(dx, dy));
    if (dot(normal, normal) < 0.001)
        normal = float3(0, 0, 1);
    float3 ldir = uLightDir.xyz;
    if (dot(ldir, ldir) < 0.0001)
        ldir = float3(-0.85, 0.10, -0.52);
    float3 lightDir = normalize(-ldir);
    float diff = abs(dot(normal, lightDir));
    float amb = uAmbientStrength > 0.0 ? uAmbientStrength : 0.30;
    float3 ambientCol = uAmbientColor.xyz;
    if (dot(ambientCol, ambientCol) < 0.0001)
        ambientCol = float3(0.45, 0.45, 0.48);
    float3 lightCol = uLightColor.xyz;
    if (dot(lightCol, lightCol) < 0.0001)
        lightCol = float3(1, 1, 1);
    float intensity = uLightIntensity > 0.0 ? uLightIntensity : 1.0;
    float shadow = SampleSunShadow(i.vWorldPos);
    float3 lit = amb * rgb * ambientCol + diff * rgb * lightCol * intensity * shadow;
    return float4(lit, 1.0);
}";
    }
}
