// Folder: SiegeEngine/Core/GPU/Shaders/OpenGL
// File: TerrainShader.cs
// Extracted from Renderers/TerrainRenderer.cs. Renderer now references these constants.
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class TerrainShader
    {
        public const string VertexShaderSource = @"#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColor;
layout(location = 2) in vec2 aUV;
out vec4 vColor;
out vec2 vUV;
out vec3 vWorldPos;
out vec4 vViewPos;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
void main() {
    vec4 world = uModel * vec4(aPosition, 1.0);
    vWorldPos = world.xyz;
    vViewPos = uView * world;
    gl_Position = uProjection * vViewPos;
    vColor = aColor;
    vUV = aUV;
}";

        public const string FragmentShaderSource = @"#version 330 core
in vec4 vColor;
in vec2 vUV;
in vec3 vWorldPos;
in vec4 vViewPos;
out vec4 FragColor;
uniform sampler2D uTexture;
uniform int uHasTexture;
uniform float uPolyFactor;
uniform float uPolyUnits;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uAmbientColor;
uniform float uAmbientStrength;
uniform int uShadowsEnabled;
uniform int uReceiveShadows;
uniform int uCascadeCount;
uniform mat4 uCascadeVP[4];
uniform vec4 uCascadeSplits;
uniform sampler2D uShadowAtlas;
uniform float uShadowBias;
uniform float uShadowAtlasSize;
uniform float uShadowStrength;
uniform int uShadowSmooth;
uniform vec4 uCascadeZRange;
uniform samplerCube uPointShadowCube;
uniform int uPointShadowsEnabled;
uniform float uPointShadowFar;
uniform float uPointShadowStrength;
uniform int uFogMode;
uniform vec3 uFogColor;
uniform float uFogDensity;
uniform float uFogStart;
uniform float uFogHeight;
uniform float uFogHeightFalloff;
uniform int uUnlit;
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
float SampleCascadeAt(int cascade, vec3 worldPos, vec3 normal) {
    vec4 clip = uCascadeVP[cascade] * vec4(worldPos, 1.0);
    vec3 proj = clip.xyz / max(clip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    if (proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0)
        return -1.0;
    if (proj.z < 0.0 || proj.z > 1.0)
        return -1.0;

    float cell = 0.5;
    vec2 atlasOrigin = vec2(float(cascade - (cascade / 2) * 2), float(cascade / 2)) * cell;
    vec2 atlasUv = atlasOrigin + proj.xy * cell;
    atlasUv = clamp(atlasUv, atlasOrigin + vec2(0.001), atlasOrigin + vec2(cell - 0.001));

    float stored = texture(uShadowAtlas, atlasUv).r;
    if (uShadowSmooth > 0) {
        // Box-filter the depth map, then one ESM compare.
        // Not PCF: we do not test this Z against neighbor rays.
        float texel = cell / max(uShadowAtlasSize * 0.5, 1.0);
        float acc = 0.0;
        int taps = 0;
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                vec2 uv = atlasUv + vec2(float(x), float(y)) * texel;
                uv = clamp(uv, atlasOrigin + vec2(0.001), atlasOrigin + vec2(cell - 0.001));
                acc += texture(uShadowAtlas, uv).r;
                taps++;
            }
        }
        stored = acc / float(max(taps, 1));
    }

    float umbra = uShadowStrength;
    if (umbra < 0.0) umbra = 0.08;

    float zRange = 1.0;
    if (cascade == 0) zRange = uCascadeZRange.x;
    else if (cascade == 1) zRange = uCascadeZRange.y;
    else if (cascade == 2) zRange = uCascadeZRange.z;
    else zRange = uCascadeZRange.w;
    zRange = max(zRange, 1.0);

    float kWorld = uShadowBias;
    if (kWorld < 1.0) kWorld = 40.0;
    float k = kWorld * zRange;
    float dz = max(proj.z - stored, 0.0);
    float vis = exp(-k * dz);
    return mix(umbra, 1.0, clamp(vis, 0.0, 1.0));
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
vec3 PointLighting(vec3 albedo, vec3 norm) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 4; i++) {
        if (i >= uPointCount) break;
        vec3 toLight = uPointPos[i] - vWorldPos;
        float dist = length(toLight);
        float range = max(uPointRange[i], 0.01);
        if (dist > range) continue;
        vec3 L = toLight / max(dist, 0.0001);
        float att = 1.0 - clamp(dist / range, 0.0, 1.0);
        att *= att;
        float diff = max(dot(norm, L), 0.0);
        float shadow = (i == 0) ? SamplePointShadow(vWorldPos, uPointPos[i], range) : 1.0;
        sum += diff * albedo * uPointColor[i] * uPointIntensity[i] * att * shadow;
    }
    return sum;
}
vec3 SpotLighting(vec3 albedo, vec3 norm) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 2; i++) {
        if (i >= uSpotCount) break;
        vec3 toLight = uSpotPos[i] - vWorldPos;
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
void main() {
    vec4 albedo = vColor;
    if (uHasTexture == 1) {
        if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
            albedo = texture(uTexture, vUV);
        } else {
            discard;
        }
    }
    if (uUnlit == 1) {
        FragColor = vec4(0.486, 1.0, 0.796, 1.0);
        gl_FragDepth = gl_FragCoord.z;
        return;
    }
    vec3 dx = dFdx(vWorldPos);
    vec3 dy = dFdy(vWorldPos);
    vec3 normal = normalize(cross(dx, dy));
    if (dot(normal, normal) < 0.001)
        normal = vec3(0.0, 0.0, 1.0);
    vec3 lightDir = normalize(-uLightDir);
    float shadow = SampleCascadeShadow(vWorldPos, normal);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 ambient = uAmbientStrength * albedo.rgb * uAmbientColor;
    vec3 lit = ambient + diff * albedo.rgb * uLightColor * uLightIntensity * shadow;
    lit += PointLighting(albedo.rgb, normal);
    lit += SpotLighting(albedo.rgb, normal);
    if (uFogMode != 0 && uFogMode != 3) {
        float dist = length(vViewPos.xyz);
        float fogFactor = exp(-uFogDensity * dist);
        if (uFogMode == 2) {
            float heightTerm = exp(-uFogHeightFalloff * max(vWorldPos.z - uFogHeight, 0.0));
            fogFactor = exp(-uFogDensity * dist * heightTerm);
        }
        lit = mix(uFogColor, lit, clamp(fogFactor, 0.0, 1.0));
    }
    FragColor = vec4(lit, albedo.a);
    float dz = max(abs(dFdx(gl_FragCoord.z)), abs(dFdy(gl_FragCoord.z)));
    gl_FragDepth = clamp(gl_FragCoord.z + uPolyFactor * dz + uPolyUnits / 16777216.0, 0.0, 1.0);
}";
    }
}
