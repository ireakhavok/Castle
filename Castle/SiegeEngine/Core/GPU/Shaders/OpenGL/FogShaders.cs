// Folder: SiegeEngine/Core/GPU/Shaders/OpenGL
// File: FogShaders.cs
namespace SiegeEngine.Core.GPU.Shaders.OpenGL
{
    public static class FogShaders
    {
        public const string FullscreenVertex = @"#version 330 core

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
    vec4 LightPos;
};
out vec2 vUv;
mat4 CascadeVPAt(int c) {
    if (c == 1) return CascadeVP1;
    if (c == 2) return CascadeVP2;
    if (c == 3) return CascadeVP3;
    return CascadeVP0;
}
void main() {
    vec2 pos = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
    vUv = pos;
    gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
}";

        public const string VolumetricFragment = @"#version 330 core

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
    vec4 LightPos;
};
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D Color;
uniform sampler2D uDepth;
uniform sampler2D uShadowAtlas;

float hash12(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

mat4 CascadeVPAt(int c) {
    if (c == 1) return CascadeVP1;
    if (c == 2) return CascadeVP2;
    if (c == 3) return CascadeVP3;
    return CascadeVP0;
}
float cascadeShadow(vec3 worldPos) {
    if (CascadeCount <= 0)
        return 1.0;
    for (int c = 0; c < 4; c++) {
        if (c >= CascadeCount) break;
        vec4 lightClip = CascadeVPAt(c) * vec4(worldPos, 1.0);
        vec3 proj = lightClip.xyz / max(lightClip.w, 0.0001);
        proj = proj * 0.5 + 0.5;
        if (proj.x > 0.001 && proj.x < 0.999 && proj.y > 0.001 && proj.y < 0.999 && proj.z > 0.0 && proj.z < 1.0) {
            float cell = 0.5;
            vec2 atlas = vec2(float(c - (c / 2) * 2), float(c / 2)) * cell + proj.xy * cell;
            float closest = texture(uShadowAtlas, atlas).r;
            return proj.z - 0.004 > closest ? 0.55 : 1.0;
        }
    }
    return 1.0;
}

vec3 reconstructWorld(vec2 uv, float depth) {
    vec4 clip = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 view = InvProjection * clip;
    view /= max(view.w, 0.0001);
    vec4 world = InvView * view;
    return world.xyz;
}

void main() {
    vec3 scene = texture(Color, vUv).rgb;
    if (HasDepth == 0 || Steps < 1 || Intensity <= 0.0) {
        FragColor = vec4(scene, 1.0);
        return;
    }
    float depth = texture(uDepth, vUv).r;
    vec3 camPos = (InvView * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
    vec3 farPoint = reconstructWorld(vUv, 0.995);
    vec3 rayDir = normalize(farPoint - camPos);
    vec3 worldEnd;
    if (depth >= 0.9995) {
        worldEnd = camPos + rayDir * 250.0;
    } else {
        worldEnd = reconstructWorld(vUv, depth);
    }
    vec3 delta = worldEnd - camPos;
    float dist = length(delta);
    if (dist < 0.05) {
        FragColor = vec4(scene, 1.0);
        return;
    }
    vec3 dir = delta / dist;
    dist = min(dist, 250.0);
    int steps = Steps;
    if (steps < 8) steps = 8;
    if (steps > 32) steps = 32;
    float stepLen = dist / float(steps);
    float jitter = hash12(vUv * 1.731 + InvResolution.xy * 17.0);
    vec3 pos = camPos + dir * stepLen * (0.25 + jitter * 0.75);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float densityScale = max(FogDensity, 0.00005) * max(Intensity, 0.0);
    vec3 lightCol = LightColor.xyz * max(LightIntensity, 0.2);
    for (int i = 0; i < 32; i++) {
        if (i >= steps) break;
        float travelled = distance(camPos, pos);
        if (travelled >= FogStart && travelled <= dist) {
            float heightFog = exp(-FogHeightFalloff * max(pos.z - FogHeight, 0.0));
            float density = densityScale * heightFog;
            float lit = cascadeShadow(pos);
            float scatter = 1.0 - exp(-density * stepLen);
            accum += transmittance * scatter * mix(0.55, 1.0, lit) * lightCol * FogColor.xyz;
            transmittance *= exp(-density * stepLen);
        }
        pos += dir * stepLen;
        if (transmittance < 0.03) break;
    }
    float dither = (hash12(vUv * 113.0) - 0.5) * 0.008;
    vec3 result = scene * transmittance + accum + dither;
    FragColor = vec4(result, 1.0);
}";
    }
}
