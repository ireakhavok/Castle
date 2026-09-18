// Folder: SiegeEngine/Core/GPU/Shaders/OpenGL
// File: TerrainShader.cs
namespace SiegeEngine.Core.GPU.Shaders.OpenGL
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
layout(std140) uniform FrameCB
{
    mat4 View;
    mat4 Projection;
    vec4 ViewPos;
    float Time;
    int HasTexture;
    float PadFrame0;
    float PadFrame1;
};
layout(std140) uniform ObjectCB
{
    mat4 Model;
    mat4 NormalMatrix;
    int HasBones;
    int ReceiveShadows;
    int Pad0;
    int Pad1;
    float PointSize;
    float VerticalOffset;
    float Pad3;
    float Pad4;
};
void main() {
    vec4 world = Model * vec4(aPosition, 1.0);
    vWorldPos = world.xyz;
    vViewPos = View * world;
    gl_Position = Projection * vViewPos;
    vColor = aColor;
    vUV = aUV;
}";

        public const string FragmentShaderSource = @"#version 330 core
in vec4 vColor;
in vec2 vUV;
in vec3 vWorldPos;
in vec4 vViewPos;
out vec4 FragColor;
layout(std140) uniform FrameCB
{
    mat4 View;
    mat4 Projection;
    vec4 ViewPos;
    float Time;
    int HasTexture;
    float PadFrame0;
    float PadFrame1;
};
layout(std140) uniform ObjectCB
{
    mat4 Model;
    mat4 NormalMatrix;
    int HasBones;
    int ReceiveShadows;
    int Pad0;
    int Pad1;
    float PointSize;
    float VerticalOffset;
    float Pad3;
    float Pad4;
};
layout(std140) uniform LightCB
{
    vec4 LightDir;
    vec4 LightColor;
    vec4 AmbientColor;
    vec4 LightViewPos;
    float LightIntensity;
    float AmbientStrength;
    float SpecularStrength;
    float Shininess;
    int PointCount;
    int SpotCount;
    int FogMode;
    int LightPad0;
    vec4 PointPos0;
    vec4 PointPos1;
    vec4 PointPos2;
    vec4 PointPos3;
    vec4 PointColor0;
    vec4 PointColor1;
    vec4 PointColor2;
    vec4 PointColor3;
    vec4 PointIntensityRange0;
    vec4 PointIntensityRange1;
    vec4 PointIntensityRange2;
    vec4 PointIntensityRange3;
    vec4 SpotPos0;
    vec4 SpotPos1;
    vec4 SpotDir0;
    vec4 SpotDir1;
    vec4 SpotColor0;
    vec4 SpotColor1;
    vec4 SpotIntensityRange0;
    vec4 SpotIntensityRange1;
    vec4 SpotCone0;
    vec4 SpotCone1;
    vec4 FogColor;
    float FogDensity;
    float FogStart;
    float FogHeight;
    float FogHeightFalloff;
};
layout(std140) uniform ShadowCB
{
    mat4 CascadeVP0;
    mat4 CascadeVP1;
    mat4 CascadeVP2;
    mat4 CascadeVP3;
    vec4 CascadeSplits;
    vec4 CascadeZRange;
    int ShadowsEnabled;
    int ShadowReceiveShadows;
    int CascadeCount;
    int ShadowSmooth;
    float ShadowBias;
    float ShadowAtlasSize;
    float ShadowStrength;
    int PointShadowsEnabled;
    float PointShadowFar;
    float PointShadowStrength;
    float ShadowPad0;
    float ShadowPad1;
};
layout(std140) uniform PostCB
{
    mat4 PrevView;
    mat4 PrevProjection;
    mat4 InvView;
    mat4 InvProjection;
    vec4 InvResolution;
    float Threshold;
    float Knee;
    float Exposure;
    float BloomIntensity;
    float Contrast;
    float Saturation;
    float Temperature;
    float TargetLuma;
    float Adapt;
    float AdaptedLuma;
    int HasHistory;
    int HasBloom;
    int HasPrev;
    int AutoExposure;
    int Tonemap;
    int Steps;
    float Intensity;
    int HasDepth;
    float Unlit;
    float PolyFactor;
    float PolyUnits;
    float LinearDepth;
    float FarPlane;
    float PadPost0;
    float PadPost1;
    float PadPost2;
    float PadPost3;
    float PadPost4;
    vec4 LightPos;
};
uniform sampler2D uTexture;
uniform sampler2D uShadowAtlas;
uniform samplerCube uPointShadowCube;
mat4 CascadeVPAt(int i) {
    if (i == 1) return CascadeVP1;
    if (i == 2) return CascadeVP2;
    if (i == 3) return CascadeVP3;
    return CascadeVP0;
}
vec3 PointPosAt(int i) {
    if (i == 1) return PointPos1.xyz;
    if (i == 2) return PointPos2.xyz;
    if (i == 3) return PointPos3.xyz;
    return PointPos0.xyz;
}
vec3 PointColorAt(int i) {
    if (i == 1) return PointColor1.xyz;
    if (i == 2) return PointColor2.xyz;
    if (i == 3) return PointColor3.xyz;
    return PointColor0.xyz;
}
vec4 PointIntensityRangeAt(int i) {
    if (i == 1) return PointIntensityRange1;
    if (i == 2) return PointIntensityRange2;
    if (i == 3) return PointIntensityRange3;
    return PointIntensityRange0;
}
vec3 SpotPosAt(int i) { return i == 1 ? SpotPos1.xyz : SpotPos0.xyz; }
vec3 SpotDirAt(int i) { return i == 1 ? SpotDir1.xyz : SpotDir0.xyz; }
vec3 SpotColorAt(int i) { return i == 1 ? SpotColor1.xyz : SpotColor0.xyz; }
vec4 SpotIntensityRangeAt(int i) { return i == 1 ? SpotIntensityRange1 : SpotIntensityRange0; }
vec4 SpotConeAt(int i) { return i == 1 ? SpotCone1 : SpotCone0; }
float SampleCascadeAt(int cascade, vec3 worldPos, vec3 normal) {
    vec4 clip = CascadeVPAt(cascade) * vec4(worldPos, 1.0);
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
    if (ShadowSmooth > 0) {
        // Box-filter the depth map, then one ESM compare.
        // Not PCF: we do not test this Z against neighbor rays.
        float texel = cell / max(ShadowAtlasSize * 0.5, 1.0);
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

    float umbra = ShadowStrength;
    if (umbra < 0.0) umbra = 0.08;

    float zRange = 1.0;
    if (cascade == 0) zRange = CascadeZRange.x;
    else if (cascade == 1) zRange = CascadeZRange.y;
    else if (cascade == 2) zRange = CascadeZRange.z;
    else zRange = CascadeZRange.w;
    zRange = max(zRange, 1.0);

    float kWorld = ShadowBias;
    if (kWorld < 1.0) kWorld = 40.0;
    float k = kWorld * zRange;
    float dz = max(proj.z - stored, 0.0);
    float vis = exp(-k * dz);
    return mix(umbra, 1.0, clamp(vis, 0.0, 1.0));
}

float SampleCascadeShadow(vec3 worldPos, vec3 normal) {
    if (ShadowsEnabled == 0 || ShadowReceiveShadows == 0 || CascadeCount <= 0)
        return 1.0;
    for (int i = 0; i < 4; i++) {
        if (i >= CascadeCount) break;
        float s = SampleCascadeAt(i, worldPos, normal);
        if (s >= 0.0) return s;
    }
    return 1.0;
}
float SamplePointShadow(vec3 worldPos, vec3 lightPos, float range) {
    if (PointShadowsEnabled == 0)
        return 1.0;
    vec3 L = worldPos - lightPos;
    float dist = length(L);
    if (dist > range || dist < 0.02)
        return 1.0;
    vec3 dir = L / dist;
    vec3 up = abs(dir.z) < 0.99 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0);
    vec3 tangent = normalize(cross(up, dir));
    vec3 bitangent = cross(dir, tangent);
    float current = dist / max(PointShadowFar, 0.001);
    float umbra = PointShadowStrength;
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
        if (i >= PointCount) break;
        vec3 toLight = PointPosAt(i) - vWorldPos;
        float dist = length(toLight);
        float range = max(PointIntensityRangeAt(i).y, 0.01);
        if (dist > range) continue;
        vec3 L = toLight / max(dist, 0.0001);
        float att = 1.0 - clamp(dist / range, 0.0, 1.0);
        att *= att;
        float diff = max(dot(norm, L), 0.0);
        float shadow = (i == 0) ? SamplePointShadow(vWorldPos, PointPosAt(i), range) : 1.0;
        sum += diff * albedo * PointColorAt(i) * PointIntensityRangeAt(i).x * att * shadow;
    }
    return sum;
}
vec3 SpotLighting(vec3 albedo, vec3 norm) {
    vec3 sum = vec3(0.0);
    for (int i = 0; i < 2; i++) {
        if (i >= SpotCount) break;
        vec3 toLight = SpotPosAt(i) - vWorldPos;
        float dist = length(toLight);
        float range = max(SpotIntensityRangeAt(i).y, 0.01);
        if (dist > range) continue;
        vec3 L = toLight / max(dist, 0.0001);
        float theta = dot(L, normalize(-SpotDirAt(i)));
        float epsilon = max(SpotConeAt(i).x - SpotConeAt(i).y, 0.001);
        float cone = clamp((theta - SpotConeAt(i).y) / epsilon, 0.0, 1.0);
        float att = 1.0 - clamp(dist / range, 0.0, 1.0);
        att *= att * cone;
        float diff = max(dot(norm, L), 0.0);
        sum += diff * albedo * SpotColorAt(i) * SpotIntensityRangeAt(i).x * att;
    }
    return sum;
}
void main() {
    vec4 albedo = vColor;
    if (HasTexture == 1) {
        if (vUV.x >= 0.0 && vUV.x <= 1.0 && vUV.y >= 0.0 && vUV.y <= 1.0) {
            albedo = texture(uTexture, vUV);
        } else {
            discard;
        }
    }
    if (int(Unlit) == 1) {
        FragColor = vec4(0.486, 1.0, 0.796, 1.0);
        gl_FragDepth = gl_FragCoord.z;
        return;
    }
    vec3 dx = dFdx(vWorldPos);
    vec3 dy = dFdy(vWorldPos);
    vec3 normal = normalize(cross(dx, dy));
    if (dot(normal, normal) < 0.001)
        normal = vec3(0.0, 0.0, 1.0);
    vec3 lightDir = normalize(-LightDir.xyz);
    float shadow = SampleCascadeShadow(vWorldPos, normal);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 ambient = AmbientStrength * albedo.rgb * AmbientColor.xyz;
    vec3 lit = ambient + diff * albedo.rgb * LightColor.xyz * LightIntensity * shadow;
    lit += PointLighting(albedo.rgb, normal);
    lit += SpotLighting(albedo.rgb, normal);
    if (FogMode != 0 && FogMode != 3) {
        float dist = length(vViewPos.xyz);
        float fogFactor = exp(-FogDensity * dist);
        if (FogMode == 2) {
            float heightTerm = exp(-FogHeightFalloff * max(vWorldPos.z - FogHeight, 0.0));
            fogFactor = exp(-FogDensity * dist * heightTerm);
        }
        lit = mix(FogColor.xyz, lit, clamp(fogFactor, 0.0, 1.0));
    }
    FragColor = vec4(lit, albedo.a);
    float dz = max(abs(dFdx(gl_FragCoord.z)), abs(dFdy(gl_FragCoord.z)));
    gl_FragDepth = clamp(gl_FragCoord.z + PolyFactor * dz + PolyUnits / 16777216.0, 0.0, 1.0);
}";
    }
}
