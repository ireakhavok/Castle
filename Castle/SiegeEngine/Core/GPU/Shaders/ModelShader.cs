// Folder: SiegeEngine.Core.GPU.Shaders
// File: ModelShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class ModelShader
    {
        public const string VertexShaderSource = @"#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 2) in vec2 aTexCoord;
layout (location = 3) in vec3 aNormal;
layout (location = 4) in float aMaterialIndex;
layout (location = 5) in vec3 aTangent;
layout (location = 6) in vec4 aBoneIDs;
layout (location = 7) in vec4 aBoneWeights;

out vec2 vTexCoord;
out vec3 vNormal;
out vec3 vTangent;
out vec3 vPosition;
out vec3 vWorldPos;
out float vMaterialIndex;
out vec4 vViewPos;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform mat4 uNormalMatrix;
uniform mat4 uBoneTransforms[128];
uniform int uHasBones;

void main() {
    vTexCoord = aTexCoord;
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    if (uHasBones == 1) {
        for (int i = 0; i < 4; i++) {
            int id = int(aBoneIDs[i]);
            if (id < 0 || id >= 128) continue;
            mat4 boneMat = uBoneTransforms[id];
            vec4 localPos = boneMat * vec4(aPosition, 1.0);
            totalPosition += localPos * aBoneWeights[i];
            mat3 normalMat = mat3(boneMat);
            totalNormal += (normalMat * aNormal) * aBoneWeights[i];
            totalTangent += (normalMat * aTangent) * aBoneWeights[i];
        }
        if (dot(totalPosition, totalPosition) < 0.0001)
            totalPosition = vec4(aPosition, 1.0);
        totalNormal = normalize(totalNormal);
        totalTangent = normalize(totalTangent);
    } else {
        totalPosition = vec4(aPosition, 1.0);
        totalNormal = aNormal;
        totalTangent = aTangent;
    }
    mat3 nMat = mat3(uNormalMatrix);
    if (dot(nMat[0], nMat[0]) < 0.0001)
        nMat = mat3(uModel);
    vNormal = normalize(nMat * totalNormal);
    vTangent = normalize(nMat * totalTangent);
    vPosition = vec3(uModel * totalPosition);
    vWorldPos = vPosition;
    vMaterialIndex = aMaterialIndex;
    vViewPos = uView * vec4(vPosition, 1.0);
    gl_Position = uProjection * uView * uModel * totalPosition;
}";

        public const string FragmentShaderSource = @"#version 330 core
in vec2 vTexCoord;
in vec3 vNormal;
in vec3 vTangent;
in vec3 vPosition;
in vec3 vWorldPos;
in float vMaterialIndex;
in vec4 vViewPos;

out vec4 FragColor;

uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uViewPos;
uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform float uShininess;
uniform vec3 uAmbientColor;

uniform sampler2D uAlbedoMap[4];
uniform sampler2D uNormalMap[4];
uniform sampler2D uMetallicMap[4];

uniform int uDebugTextureOnly;
uniform int uDebugMaterialIndex;

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

int AlbedoWidth(int matIdx) {
    if (matIdx == 1) return textureSize(uAlbedoMap[1], 0).x;
    if (matIdx == 2) return textureSize(uAlbedoMap[2], 0).x;
    if (matIdx == 3) return textureSize(uAlbedoMap[3], 0).x;
    return textureSize(uAlbedoMap[0], 0).x;
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

vec2 WorldPlanarUV(vec3 worldPos, vec3 normal, int axis, vec2 tiling, vec2 offset) {
    vec2 uv;
    if (axis == 0) uv = worldPos.yz;
    else if (axis == 1) uv = worldPos.xz;
    else uv = worldPos.xy;
    return uv * tiling + offset;
}

float SampleCascadeAt(int cascade, vec3 worldPos, vec3 normal) {
    vec3 offsetPos = worldPos + normal * uShadowNormalBias;
    vec4 lightClip = uCascadeVP[cascade] * vec4(offsetPos, 1.0);
    vec3 proj = lightClip.xyz / max(lightClip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    bool last = cascade == uCascadeCount - 1;
    if (!last && (proj.x <= 0.001 || proj.x >= 0.999 || proj.y <= 0.001 || proj.y >= 0.999 || proj.z <= 0.0 || proj.z >= 1.0))
        return -1.0;
    proj = clamp(proj, vec3(0.001, 0.001, 0.0), vec3(0.999, 0.999, 1.0));
    float cell = 0.5;
    vec2 atlasOrigin = vec2(float(cascade - (cascade / 2) * 2), float(cascade / 2)) * cell;
    vec2 atlasUv = atlasOrigin + proj.xy * cell;
    float shadow = 0.0;
    float texel = 1.0 / max(uShadowAtlasSize, 1.0);
    int kernel = uShadowPcfRadius;
    if (kernel < 1) kernel = 1;
    if (kernel > 3) kernel = 3;
    int taps = 0;
    float umbra = uShadowStrength;
    if (umbra < 0.0) umbra = 0.08;
    for (int x = -3; x <= 3; x++) {
        if (abs(x) > kernel) continue;
        for (int y = -3; y <= 3; y++) {
            if (abs(y) > kernel) continue;
            float closest = texture(uShadowAtlas, atlasUv + vec2(float(x), float(y)) * texel * cell).r;
            shadow += (proj.z - uShadowBias > closest) ? umbra : 1.0;
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
        vec3 toLight = uPointPos[i] - vPosition;
        float dist = length(toLight);
        float range = max(uPointRange[i], 0.01);
        if (dist > range) continue;
        vec3 L = toLight / max(dist, 0.0001);
        float att = 1.0 - clamp(dist / range, 0.0, 1.0);
        att *= att;
        float diff = max(dot(norm, L), 0.0);
        vec3 H = normalize(L + viewDir);
        float spec = pow(max(dot(norm, H), 0.0), max(uShininess, 1.0));
        float shadow = (i == 0) ? SamplePointShadow(vPosition, uPointPos[i], range) : 1.0;
        sum += (diff * albedo + spec * uSpecularStrength) * uPointColor[i] * uPointIntensity[i] * att * shadow;
    }
    return sum;
}

vec3 SpotLighting(vec3 albedo, vec3 norm, vec3 viewDir) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 2; i++) {
        if (i >= uSpotCount) break;
        vec3 toLight = uSpotPos[i] - vPosition;
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
        vec3 H = normalize(L + viewDir);
        float spec = pow(max(dot(norm, H), 0.0), max(uShininess, 1.0));
        sum += (diff * albedo + spec * uSpecularStrength) * uSpotColor[i] * uSpotIntensity[i] * att;
    }
    return sum;
}

vec3 ApplyFog(vec3 color) {
    // Volumetric (mode 3) is composited in FogPass. Do not also wash
    // the forward color toward fog gray -- that made sunlit models gray.
    if (uFogMode == 0 || uFogMode == 3) return color;
    float dist = length(uViewPos - vPosition);
    float fogFactor = exp(-uFogDensity * dist);
    if (uFogMode == 2) {
        float heightTerm = exp(-uFogHeightFalloff * max(vPosition.z - uFogHeight, 0.0));
        fogFactor = exp(-uFogDensity * dist * heightTerm);
    }
    fogFactor = clamp(fogFactor, 0.0, 1.0);
    return mix(uFogColor, color, fogFactor);
}

void main() {
    int matIdx = int(vMaterialIndex);
    if (matIdx < 0) matIdx = 0;
    if (matIdx > 3) matIdx = 3;

    if (uDebugMaterialIndex == 1) {
        if (matIdx == 0) FragColor = vec4(1.0, 0.0, 0.0, 1.0);
        else if (matIdx == 1) FragColor = vec4(0.0, 1.0, 0.0, 1.0);
        else if (matIdx == 2) FragColor = vec4(0.0, 0.0, 1.0, 1.0);
        else FragColor = vec4(1.0, 1.0, 0.0, 1.0);
        return;
    }

    vec3 materialDiffuse = vec3(1.0, 1.0, 1.0);
    if (AlbedoWidth(matIdx) > 0)
        materialDiffuse = SampleAlbedo(matIdx, vTexCoord);

    if (uDebugTextureOnly == 1) {
        FragColor = vec4(materialDiffuse, 1.0);
        return;
    }

    vec3 geoN = normalize(vNormal);
    if (dot(geoN, geoN) < 0.001)
        geoN = vec3(0.0, 0.0, 1.0);
    vec3 toCam = uViewPos - vPosition;
    if (dot(geoN, toCam) < 0.0)
        geoN = -geoN;
    vec3 norm = geoN;
    if (length(vTangent) > 0.001 && NormalWidth(matIdx) > 4) {
        vec3 T = normalize(vTangent);
        T = normalize(T - dot(T, geoN) * geoN);
        vec3 B = cross(geoN, T);
        vec3 tangentNormal = SampleNormalMap(matIdx, vTexCoord) * 2.0 - 1.0;
        tangentNormal.y = -tangentNormal.y;
        vec3 mapped = normalize(mat3(T, B, geoN) * tangentNormal);
        if (dot(mapped, geoN) < 0.0)
            mapped = -mapped;
        if (dot(mapped, geoN) > 0.15)
            norm = mapped;
    }

    float metallic = 0.0;
    if (MetallicWidth(matIdx) > 0)
        metallic = SampleMetallic(matIdx, vTexCoord);

    vec3 lightDir = normalize(-uLightDir);
    float shadow = SampleCascadeShadow(vWorldPos, norm);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor * uLightIntensity * materialDiffuse * shadow;
    vec3 ambient = uAmbientStrength * materialDiffuse * uAmbientColor;
    vec3 viewDir = normalize(uViewPos - vPosition);
    vec3 reflectDir = reflect(-lightDir, norm);
    float specStrength = uSpecularStrength * (1.0 - metallic);
    float shininess = max(uShininess * (1.0 - metallic), 1.0);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
    vec3 specular = specStrength * spec * uLightColor * uLightIntensity * (1.0 - metallic) * shadow;

    vec3 result = ambient + diffuse + specular;
    result += PointLighting(materialDiffuse, norm, viewDir);
    result += SpotLighting(materialDiffuse, norm, viewDir);
    result = ApplyFog(result);
    FragColor = vec4(result, 1.0);
}";
    }
}
