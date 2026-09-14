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
    row_major float4x4 Model;
    float4 LightDir;
    float4 LightColor;
    float4 AmbientColor;
    float LightIntensity;
    float AmbientStrength;
    float HasOpacity;
    float OpacitySlots;
    row_major float4x4 CascadeVP;
    float ShadowsEnabled;
    float HasBones;
    float2 PadS;
};
cbuffer Bones : register(b1)
{
    row_major float4x4 uBones[64];
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
    float vMaterialIndex : TEXCOORD1;
    float3 vWorldPos : TEXCOORD2;
};
VSOut vs(VSIn i)
{
    VSOut o;
    float4 skinned = float4(i.aPosition, 1.0);
    float3 nrm = i.aNormal;
    if (HasBones > 0.5)
    {
        float4 acc = float4(0, 0, 0, 0);
        float3 accN = float3(0, 0, 0);
        [unroll] for (int b = 0; b < 4; b++)
        {
            int id = (int)i.aBoneIDs[b];
            if (id < 0 || id >= 64) continue;
            float w = i.aBoneWeights[b];
            acc += mul(float4(i.aPosition, 1.0), uBones[id]) * w;
            accN += mul(i.aNormal, (float3x3)uBones[id]) * w;
        }
        if (dot(acc, acc) > 0.0001) skinned = acc;
        if (dot(accN, accN) > 0.0001) nrm = accN;
    }
    float4 c = mul(skinned, Mvp);
    c.z = c.z * 0.5 + c.w * 0.5;
    o.pos = c;
    o.vTexCoord = i.aTexCoord;
    o.vNormal = mul(nrm, (float3x3)Model);
    o.vMaterialIndex = i.aMaterialIndex;
    o.vWorldPos = mul(skinned, Model).xyz;
    return o;
}";

        public const string FragmentShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 Mvp;
    row_major float4x4 Model;
    float4 LightDir;
    float4 LightColor;
    float4 AmbientColor;
    float LightIntensity;
    float AmbientStrength;
    float HasOpacity;
    float OpacitySlots;
    row_major float4x4 CascadeVP;
    float ShadowsEnabled;
    float HasBones;
    float2 PadS;
};
Texture2D uAlbedoMap0 : register(t0);
Texture2D uOpacityMap : register(t1);
Texture2D uShadowAtlas : register(t2);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float2 vTexCoord : TEXCOORD;
    float3 vNormal : NORMAL;
    float vMaterialIndex : TEXCOORD1;
    float3 vWorldPos : TEXCOORD2;
};
cbuffer Cascades : register(b2)
{
    row_major float4x4 uCascadeVP4[4];
    float uCascadeCount;
    float3 PadCas;
};
float SampleCascadeAt(int cascade, float3 worldPos)
{
    float4 clip = mul(float4(worldPos, 1.0), uCascadeVP4[cascade]);
    float3 proj = clip.xyz / max(clip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    if (proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0)
        return -1.0;
    if (proj.z < 0.0 || proj.z > 1.0)
        return -1.0;
    float cell = 0.5;
    float2 origin = float2(float(cascade - (cascade / 2) * 2), float(cascade / 2)) * cell;
    float2 atlasUv = origin + proj.xy * cell;
    atlasUv = clamp(atlasUv, origin + float2(0.001, 0.001), origin + float2(cell - 0.001, cell - 0.001));
    float stored = uShadowAtlas.Sample(Samp, atlasUv).r;
    float dz = max(proj.z - stored, 0.0);
    float vis = exp(-40.0 * dz);
    return lerp(0.08, 1.0, saturate(vis));
}
float SampleSunShadow(float3 worldPos)
{
    if (ShadowsEnabled < 0.5) return 1.0;
    int n = (int)uCascadeCount;
    if (n < 1) n = 1;
    if (n > 4) n = 4;
    for (int i = 0; i < 4; i++)
    {
        if (i >= n) break;
        float s = SampleCascadeAt(i, worldPos);
        if (s >= 0.0) return s;
    }
    return 1.0;
}
float4 ps(VSOut i) : SV_TARGET
{
    int matIdx = (int)i.vMaterialIndex;
    if (matIdx < 0) matIdx = 0;
    if (matIdx > 3) matIdx = 3;
    int slots = (int)OpacitySlots;
    if (HasOpacity > 0.5 && slots > 0 && slots < 15 && ((slots >> matIdx) & 1) == 1)
    {
        float mask = uOpacityMap.Sample(Samp, i.vTexCoord).r;
        if (mask <= 0.0) discard;
    }
    float4 albedo = uAlbedoMap0.Sample(Samp, i.vTexCoord);
    float3 n = normalize(i.vNormal);
    if (dot(n, n) < 0.001) n = float3(0, 0, 1);
    float3 ldir = LightDir.xyz;
    if (dot(ldir, ldir) < 0.001) ldir = float3(-0.85, 0.10, -0.52);
    float3 lightDir = normalize(-ldir);
    float diff = max(dot(n, lightDir), 0.0);
    float amb = AmbientStrength > 0.0 ? AmbientStrength : 0.16;
    float3 ambientCol = AmbientColor.xyz;
    if (dot(ambientCol, ambientCol) < 0.001) ambientCol = float3(0.45, 0.45, 0.48);
    float3 lightCol = LightColor.xyz;
    if (dot(lightCol, lightCol) < 0.001) lightCol = float3(1, 1, 1);
    float intensity = LightIntensity > 0.0 ? LightIntensity : 1.0;
    float shadow = SampleSunShadow(i.vWorldPos);
    float3 lit = amb * albedo.rgb * ambientCol + diff * albedo.rgb * lightCol * intensity * shadow;
    return float4(lit, 1.0);
}";
    }
}
