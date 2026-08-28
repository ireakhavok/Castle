// Folder: SiegeEngine.Core.GPU.Shaders
// File: AnimationShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AnimationShader
    {
        public const string VertexShaderSource = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec3 aNormal;
layout(location = 4) in float aMaterialIndex;
layout(location = 5) in vec3 aTangent;
layout(location = 6) in vec4 aBoneIDs;
layout(location = 7) in vec4 aWeights;
out vec2 TexCoord;
out vec3 Normal;
out vec3 FragPos;
out float MaterialIndex;
out mat3 TBN;
out vec4 vViewPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform int uHasBones;
uniform mat4 uBoneMatrices[100];
uniform mat3 uNormalMatrices[100];
void main()
{
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    float sumWeights = 0.0;
    if (uHasBones == 1) {
        for (int i = 0; i < 4; i++) {
            int boneIndex = int(aBoneIDs[i]);
            if (boneIndex < 0 || boneIndex >= 100) continue;
            mat4 boneTransform = uBoneMatrices[boneIndex];
            vec4 localPosition = boneTransform * vec4(aPosition, 1.0);
            totalPosition += localPosition * aWeights[i];
            vec3 localNormal = uNormalMatrices[boneIndex] * aNormal;
            totalNormal += localNormal * aWeights[i];
            vec3 localTangent = uNormalMatrices[boneIndex] * aTangent;
            totalTangent += localTangent * aWeights[i];
            sumWeights += aWeights[i];
        }
        if (sumWeights > 0.001) {
            totalPosition /= sumWeights;
            totalNormal /= sumWeights;
            totalTangent /= sumWeights;
            totalNormal = normalize(totalNormal);
            totalTangent = normalize(totalTangent);
        } else {
            totalPosition = vec4(aPosition, 1.0);
            totalNormal = aNormal;
            totalTangent = aTangent;
        }
    } else {
        totalPosition = vec4(aPosition, 1.0);
        totalNormal = aNormal;
        totalTangent = aTangent;
    }
    mat3 normalMatrix = transpose(inverse(mat3(uModel)));
    Normal = normalize(normalMatrix * totalNormal);
    vec3 T = normalize(normalMatrix * totalTangent);
    vec3 N = Normal;
    T = normalize(T - dot(T, N) * N);
    vec3 B = cross(N, T);
    TBN = mat3(T, B, N);
    FragPos = vec3(uModel * totalPosition);
    TexCoord = aTexCoord;
    MaterialIndex = aMaterialIndex;
    vViewPos = uView * vec4(FragPos, 1.0);
    gl_Position = uProjection * uView * uModel * totalPosition;
}";
        public const string FragmentShaderSource = @"
#version 330 core
in vec2 TexCoord;
in vec3 Normal;
in vec3 FragPos;
in float MaterialIndex;
in mat3 TBN;
in vec4 vViewPos;
out vec4 FragColor;

uniform sampler2D uAlbedoMap[4];
uniform sampler2D uNormalMap[4];
uniform sampler2D uMetallicMap[4];

uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uViewPos;
uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform float uShininess;
uniform vec3 uAmbientColor;

uniform int uHasWorldAligned;
uniform int uMappingMode[4];
uniform vec2 uTiling[4];
uniform vec2 uOffset[4];
uniform float uRotation[4];
uniform float uBlendSharpness[4];

uniform int uPointCount;
uniform vec3 uPointPos[4];
uniform vec3 uPointColor[4];
uniform float uPointIntensity[4];
uniform float uPointRange[4];
uniform int uSpotCount;
uniform vec3 uSpotPos[2];
uniform vec3 uSpotDir[2];
uniform vec3 uSpotColor[2];
uniform float uSpotIntensity[2];
uniform float uSpotRange[2];
uniform float uSpotInner[2];
uniform float uSpotOuter[2];
uniform int uFogMode;
uniform vec3 uFogColor;
uniform float uFogDensity;
uniform float uFogHeight;
uniform float uFogHeightFalloff;
uniform int uShadowsEnabled;
uniform int uReceiveShadows;
uniform int uCascadeCount;
uniform mat4 uCascadeVP[4];
uniform vec4 uCascadeSplits;
uniform sampler2D uShadowAtlas;
uniform float uShadowBias;
uniform float uShadowNormalBias;
uniform float uShadowAtlasSize;

vec2 WorldPlanarUV(vec3 worldPos, vec3 normal, int axis, vec2 tiling, vec2 offset)
{
    vec2 uv;
    if (axis == 0) uv = worldPos.yz;
    else if (axis == 1) uv = worldPos.xz;
    else uv = worldPos.xy;
    return uv * tiling + offset;
}

vec3 TriplanarSample(sampler2D tex, vec3 worldPos, vec3 normal, vec2 tiling, vec2 offset, float sharpness)
{
    vec3 blend = abs(normal);
    blend /= blend.x + blend.y + blend.z + 0.0001;
    blend = pow(blend, vec3(sharpness));
    blend /= blend.x + blend.y + blend.z;
    vec2 uvX = worldPos.yz * tiling + offset;
    vec2 uvY = worldPos.xz * tiling + offset;
    vec2 uvZ = worldPos.xy * tiling + offset;
    vec3 colX = texture(tex, uvX).rgb;
    vec3 colY = texture(tex, uvY).rgb;
    vec3 colZ = texture(tex, uvZ).rgb;
    return colX * blend.x + colY * blend.y + colZ * blend.z;
}

float SampleCascadeShadow(vec3 worldPos, vec3 normal) {
    if (uShadowsEnabled == 0 || uReceiveShadows == 0 || uCascadeCount <= 0)
        return 1.0;
    float viewZ = abs(vViewPos.z);
    int cascade = 0;
    if (uCascadeCount > 1 && viewZ > uCascadeSplits.x) cascade = 1;
    if (uCascadeCount > 2 && viewZ > uCascadeSplits.y) cascade = 2;
    if (uCascadeCount > 3 && viewZ > uCascadeSplits.z) cascade = 3;
    vec3 offsetPos = worldPos + normal * uShadowNormalBias;
    vec4 lightClip = uCascadeVP[cascade] * vec4(offsetPos, 1.0);
    vec3 proj = lightClip.xyz / max(lightClip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    if (proj.x <= 0.0 || proj.x >= 1.0 || proj.y <= 0.0 || proj.y >= 1.0 || proj.z > 1.0)
        return 1.0;
    float cell = 0.5;
    vec2 atlasUv = vec2(float(cascade % 2), float(cascade / 2)) * cell + proj.xy * cell;
    float closest = texture(uShadowAtlas, atlasUv).r;
    return (proj.z - uShadowBias > closest) ? 0.35 : 1.0;
}

vec3 ApplyFog(vec3 color) {
    if (uFogMode == 0) return color;
    float dist = length(uViewPos - FragPos);
    float fogFactor = exp(-uFogDensity * dist);
    if (uFogMode == 2) {
        float heightTerm = exp(-uFogHeightFalloff * max(FragPos.z - uFogHeight, 0.0));
        fogFactor = exp(-uFogDensity * dist * heightTerm);
    }
    return mix(uFogColor, color, clamp(fogFactor, 0.0, 1.0));
}

void main()
{
    int matIdx = int(MaterialIndex);
    vec3 albedo = texture(uAlbedoMap[matIdx], TexCoord).rgb;
    float metallic = 0.0;
    if (textureSize(uMetallicMap[matIdx], 0).x > 1)
        metallic = texture(uMetallicMap[matIdx], TexCoord).r;

    vec3 normal = normalize(Normal);
    // FBX/DirectX normal maps store +Y up. OpenGL TBN expects +Y down, so
    // flip the green channel. Skip the map when the tangent basis is
    // degenerate (UV seams on a sphere) or no normal texture is bound —
    // otherwise TBN * (-1,-1,-1) paints a bright/black split across the head.
    if (length(TBN[0]) > 0.001 && textureSize(uNormalMap[matIdx], 0).x > 1)
    {
        vec3 tangentNormal = texture(uNormalMap[matIdx], TexCoord).rgb * 2.0 - 1.0;
        tangentNormal.y = -tangentNormal.y;
        vec3 mapped = TBN * tangentNormal;
        if (dot(mapped, mapped) > 0.001)
            normal = normalize(mapped);
    }

    if (uHasWorldAligned == 1 && uMappingMode[matIdx] != 0)
    {
        if (uMappingMode[matIdx] == 2)
        {
            albedo = TriplanarSample(uAlbedoMap[matIdx], FragPos, normal, uTiling[matIdx], uOffset[matIdx], uBlendSharpness[matIdx]);
        }
        else
        {
            int axis = abs(normal.x) > abs(normal.y) && abs(normal.x) > abs(normal.z) ? 0 : (abs(normal.y) > abs(normal.z) ? 1 : 2);
            vec2 worldUV = WorldPlanarUV(FragPos, normal, axis, uTiling[matIdx], uOffset[matIdx]);
            albedo = texture(uAlbedoMap[matIdx], worldUV).rgb;
        }
    }

    vec3 ambient = uAmbientStrength * albedo * uAmbientColor;
    vec3 lightDir = normalize(-uLightDir);
    float shadow = SampleCascadeShadow(FragPos, normal);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = diff * albedo * uLightColor * uLightIntensity * shadow;
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(normal, halfwayDir), 0.0), max(uShininess, 1.0));
    vec3 specular = uSpecularStrength * spec * uLightColor * uLightIntensity * metallic * shadow;
    vec3 color = ambient + diffuse + specular;
    FragColor = vec4(ApplyFog(color), 1.0);
}";
    }
}
