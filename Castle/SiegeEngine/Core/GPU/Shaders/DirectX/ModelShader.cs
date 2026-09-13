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
    float4 LightDir;
    float4 LightColor;
    float4 AmbientColor;
    float LightIntensity;
    float AmbientStrength;
    float HasOpacity;
    float OpacitySlots;
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
};
VSOut vs(VSIn i)
{
    VSOut o;
    float4 c = mul(float4(i.aPosition, 1.0), Mvp);
    c.z = c.z * 0.5 + c.w * 0.5;
    o.pos = c;
    o.vTexCoord = i.aTexCoord;
    o.vNormal = i.aNormal;
    o.vMaterialIndex = i.aMaterialIndex;
    return o;
}";

        public const string FragmentShaderSource = @"
cbuffer CB : register(b0)
{
    row_major float4x4 Mvp;
    float4 LightDir;
    float4 LightColor;
    float4 AmbientColor;
    float LightIntensity;
    float AmbientStrength;
    float HasOpacity;
    float OpacitySlots;
};
Texture2D uAlbedoMap0 : register(t0);
Texture2D uOpacityMap : register(t1);
SamplerState Samp : register(s0);
struct VSOut
{
    float4 pos : SV_POSITION;
    float2 vTexCoord : TEXCOORD;
    float3 vNormal : NORMAL;
    float vMaterialIndex : TEXCOORD1;
};
float4 ps(VSOut i) : SV_TARGET
{
    int matIdx = (int)i.vMaterialIndex;
    if (matIdx < 0) matIdx = 0;
    if (matIdx > 3) matIdx = 3;
    if (HasOpacity > 0.5)
    {
        int slots = (int)OpacitySlots;
        if (((slots >> matIdx) & 1) == 1)
        {
            float mask = uOpacityMap.Sample(Samp, i.vTexCoord).r;
            if (mask <= 0.0) discard;
        }
    }
    float4 albedo = uAlbedoMap0.Sample(Samp, i.vTexCoord);
    if (HasOpacity < 0.5 && albedo.a < 0.15)
        discard;
    float3 n = normalize(i.vNormal);
    if (dot(n, n) < 0.001) n = float3(0, 0, 1);
    float3 ldir = LightDir.xyz;
    if (dot(ldir, ldir) < 0.001) ldir = float3(-0.85, 0.10, -0.52);
    float3 lightDir = normalize(-ldir);
    float diff = abs(dot(n, lightDir));
    float amb = AmbientStrength > 0.0 ? AmbientStrength : 0.16;
    float3 ambientCol = AmbientColor.xyz;
    if (dot(ambientCol, ambientCol) < 0.001) ambientCol = float3(0.45, 0.45, 0.48);
    float3 lightCol = LightColor.xyz;
    if (dot(lightCol, lightCol) < 0.001) lightCol = float3(1, 1, 1);
    float intensity = LightIntensity > 0.0 ? LightIntensity : 1.0;
    float3 lit = amb * albedo.rgb * ambientCol + diff * albedo.rgb * lightCol * intensity;
    return float4(lit, albedo.a);
}";
    }
}
