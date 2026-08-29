// Folder: SiegeEngine.Core.GPU.Shaders
// File: AnimationShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AnimationShader
    {
        public const string VertexShaderSource = @"#version 330 core
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
out vec3 vTangent;
out vec4 vViewPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform mat4 uNormalMatrix;
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
    mat3 nMat = mat3(uNormalMatrix);
    if (dot(nMat[0], nMat[0]) < 0.0001)
        nMat = mat3(uModel);
    Normal = normalize(nMat * totalNormal);
    vTangent = normalize(nMat * totalTangent);
    FragPos = vec3(uModel * totalPosition);
    TexCoord = aTexCoord;
    MaterialIndex = aMaterialIndex;
    vViewPos = uView * vec4(FragPos, 1.0);
    gl_Position = uProjection * uView * uModel * totalPosition;
}";

        public const string FragmentShaderSource = @"#version 330 core
in vec2 TexCoord;
in vec3 Normal;
in vec3 FragPos;
in float MaterialIndex;
in vec3 vTangent;
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
uniform float uFogStart;
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
uniform float uShadowStrength;
uniform int uShadowPcfRadius;
uniform samplerCube uPointShadowCube;
uniform int uPointShadowsEnabled;
uniform float uPointShadowFar;
uniform float uPointShadowStrength;

vec3 SampleAlbedo(int matIdx, vec2 uv) {
    if (matIdx == 1) return texture(uAlbedoMap[1], uv).rgb;
    if (matIdx == 2) return texture(uAlbedoMap[2], uv).rgb;
    if (matIdx == 3) return texture(uAlbedoMap[3], uv).rgb;
    return texture(uAlbedoMap[0], uv).rgb;
}

vec3 SampleNormalMap(int matIdx, vec2 uv) {
    if (matIdx == 1) return texture(uNormalMap[1], uv).rgb;
    if (matIdx == 2) return texture(uNormalMap[2], uv).rgb;
    if (matIdx == 3) return texture(uNormalMap[3], uv).rgb;
    return texture(uNormalMap[0], uv).rgb;
}

float SampleMetallic(int matIdx, vec2 uv) {
    if (matIdx == 1) return texture(uMetallicMap[1], uv).r;
    if (matIdx == 2) return texture(uMetallicMap[2], uv).r;
    if (matIdx == 3) return texture(uMetallicMap[3], uv).r;
    return texture(uMetallicMap[0], uv).r;
}

int NormalWidth(int matIdx) {
    if (matIdx == 1) return textureSize(uNormalMap[1], 0).x;
    if (matIdx == 2) return textureSize(uNormalMap[2], 0).x;
    if (matIdx == 3) return textureSize(uNormalMap[3], 0).x;
    return textureSize(uNormalMap[0], 0).x;
}

int MetallicWidth(int matIdx) {
    if (matIdx == 1) return textureSize(uMetallicMap[1], 0).x;
    if (matIdx == 2) return textureSize(uMetallicMap[2], 0).x;
    if (matIdx == 3) return textureSize(uMetallicMap[3], 0).x;
    return textureSize(uMetallicMap[0], 0).x;
}

float SampleCascadeAt(int cascade, vec3 worldPos, vec3 normal) {
    float ndotl = max(dot(normal, normalize(-uLightDir)), 0.0);
    float slope = 1.0 - ndotl;
    vec3 samplePos = worldPos + normal * (uShadowNormalBias * (1.0 + slope));
    vec4 sampleClip = uCascadeVP[cascade] * vec4(samplePos, 1.0);
    vec3 sampleProj = sampleClip.xyz / max(sampleClip.w, 0.0001);
    sampleProj = sampleProj * 0.5 + 0.5;
    vec4 myClip = uCascadeVP[cascade] * vec4(worldPos, 1.0);
    vec3 myProj = myClip.xyz / max(myClip.w, 0.0001);
    myProj = myProj * 0.5 + 0.5;
    if (sampleProj.x <= 0.001 || sampleProj.x >= 0.999 || sampleProj.y <= 0.001 || sampleProj.y >= 0.999)
        return -1.0;
    if (myProj.z <= 0.0 || myProj.z >= 1.0)
        return -1.0;
    sampleProj = clamp(sampleProj, vec3(0.001, 0.001, 0.0), vec3(0.999, 0.999, 1.0));
    float cell = 0.5;
    vec2 atlasOrigin = vec2(float(cascade - (cascade / 2) * 2), float(cascade / 2)) * cell;
    vec2 atlasUv = atlasOrigin + sampleProj.xy * cell;
    float shadow = 0.0;
    float texel = 1.0 / max(uShadowAtlasSize, 1.0);
    int kernel = uShadowPcfRadius;
    if (kernel < 1) kernel = 1;
    if (kernel > 7) kernel = 7;
    int taps = 0;
    float umbra = uShadowStrength;
    if (umbra < 0.0) umbra = 0.08;
    float plane = abs(dFdx(myProj.z)) + abs(dFdy(myProj.z));
    for (int x = -7; x <= 7; x++) {
        if (abs(x) > kernel) continue;
        for (int y = -7; y <= 7; y++) {
            if (abs(y) > kernel) continue;
            vec2 uv = atlasUv + vec2(float(x), float(y)) * texel * cell;
            uv = clamp(uv, atlasOrigin + vec2(0.001), atlasOrigin + vec2(cell - 0.001));
            float firstHit = texture(uShadowAtlas, uv).r;
            if (firstHit <= 0.0001 || firstHit >= 0.999)
                shadow += 1.0;
            else
                shadow += (firstHit + plane < myProj.z) ? umbra : 1.0;
            taps++;
        }
    }
    return shadow / float(max(taps, 1));
}

float SampleCascadeShadow(vec3 worldPos, vec3 normal) {
    if (uShadowsEnabled == 0 || uReceiveShadows == 0 || uCascadeCount <= 0)
        return 1.0;
    for (int i = 0; i < 4; i++) {
        if (i >= uCascadeCount) break;
        float s = SampleCascadeAt(i, worldPos, normal);
        if (s >= 0.0) return s;
    }
    return 1.0;
}

float SamplePointShadow(vec3 worldPos, vec3 lightPos, float range) {
    if (uPointShadowsEnabled == 0)
        return 1.0;
    vec3 L = worldPos - lightPos;
    float dist = length(L);
    if (dist > range || dist < 0.02)
        return 1.0;
    vec3 dir = L / dist;
    vec3 up = abs(dir.z) < 0.99 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0);
    vec3 tangent = normalize(cross(up, dir));
    vec3 bitangent = cross(dir, tangent);
    float current = dist / max(uPointShadowFar, 0.001);
    float umbra = uPointShadowStrength;
    if (umbra <= 0.0) umbra = 0.15;
    float disk = 0.006;
    float shadow = 0.0;
    int live = 0;
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            vec3 sdir = normalize(dir + tangent * float(x) * disk + bitangent * float(y) * disk);
            float closest = texture(uPointShadowCube, sdir).r;
            if (closest > 0.0001 && closest < 0.999) {
                live++;
                shadow += current > closest + 0.003 ? umbra : 1.0;
            } else {
                shadow += 1.0;
            }
        }
    }
    if (live == 0)
        return 1.0;
    return shadow / 9.0;
}

vec3 PointLighting(vec3 albedo, vec3 norm, vec3 viewDir) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 4; i++) {
        if (i >= uPointCount) break;
        vec3 toLight = uPointPos[i] - FragPos;
        float dist = length(toLight);
        float range = max(uPointRange[i], 0.01);
        if (dist > range) continue;
        vec3 L = toLight / max(dist, 0.0001);
        float att = 1.0 - clamp(dist / range, 0.0, 1.0);
        att *= att;
        float diff = max(dot(norm, L), 0.0);
        float shadow = (i == 0) ? SamplePointShadow(FragPos, uPointPos[i], range) : 1.0;
        sum += diff * albedo * uPointColor[i] * uPointIntensity[i] * att * shadow;
    }
    return sum;
}

vec3 SpotLighting(vec3 albedo, vec3 norm, vec3 viewDir) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 2; i++) {
        if (i >= uSpotCount) break;
        vec3 toLight = uSpotPos[i] - FragPos;
        float dist = length(toLight);
        float range = max(uSpotRange[i], 0.01);
        if (dist > range) continue;
        vec3 L = toLight / max(dist, 0.0001);
        float theta = dot(L, normalize(-uSpotDir[i]));
        float epsilon = max(uSpotInner[i] - uSpotOuter[i], 0.001);
        float cone = clamp((theta - uSpotOuter[i]) / epsilon, 0.0, 1.0);
        float att = 1.0 - clamp(dist / range, 0.0, 1.0);
        att *= att * cone;
        float diff = max(dot(norm, L), 0.0);
        sum += diff * albedo * uSpotColor[i] * uSpotIntensity[i] * att;
    }
    return sum;
}

vec3 ApplyFog(vec3 color) {
    if (uFogMode == 0 || uFogMode == 3) return color;
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
    if (matIdx < 0) matIdx = 0;
    if (matIdx > 3) matIdx = 3;

    vec3 albedo = SampleAlbedo(matIdx, TexCoord);
    float metallic = 0.0;
    if (MetallicWidth(matIdx) > 1)
        metallic = SampleMetallic(matIdx, TexCoord);

    vec3 geoN = normalize(Normal);
    if (dot(geoN, geoN) < 0.001)
        geoN = vec3(0.0, 0.0, 1.0);
    vec3 toCam = uViewPos - FragPos;
    if (dot(geoN, toCam) < 0.0)
        geoN = -geoN;
    vec3 normal = geoN;
    if (length(vTangent) > 0.001 && NormalWidth(matIdx) > 4)
    {
        vec3 T = normalize(vTangent);
        T = normalize(T - dot(T, geoN) * geoN);
        vec3 B = cross(geoN, T);
        vec3 tangentNormal = SampleNormalMap(matIdx, TexCoord) * 2.0 - 1.0;
        tangentNormal.y = -tangentNormal.y;
        vec3 mapped = normalize(mat3(T, B, geoN) * tangentNormal);
        if (dot(mapped, geoN) < 0.0)
            mapped = -mapped;
        if (dot(mapped, geoN) > 0.15)
            normal = mapped;
    }

    vec3 ambient = uAmbientStrength * albedo * uAmbientColor;
    vec3 lightDir = normalize(-uLightDir);
    float shadow = SampleCascadeShadow(FragPos, geoN);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = diff * albedo * uLightColor * uLightIntensity * shadow;
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(normal, halfwayDir), 0.0), max(uShininess, 1.0));
    vec3 specular = uSpecularStrength * spec * uLightColor * uLightIntensity * metallic * shadow;
    vec3 color = ambient + diffuse + specular;
    color += PointLighting(albedo, normal, viewDir);
    color += SpotLighting(albedo, normal, viewDir);
    FragColor = vec4(ApplyFog(color), 1.0);
}";
    }
}
