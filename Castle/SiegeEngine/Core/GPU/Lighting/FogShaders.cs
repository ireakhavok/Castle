// Folder: SiegeEngine/Core/GPU/Lighting
// File: FogShaders.cs
namespace SiegeEngine.Core.GPU.Lighting
{
    public static class FogShaders
    {
        public const string FullscreenVertex = @"#version 330 core
out vec2 vUv;
void main() {
    vec2 pos = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
    vUv = pos;
    gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
}";

        public const string VolumetricFragment = @"#version 330 core
in vec2 vUv;
out vec4 FragColor;
uniform sampler2D uColor;
uniform sampler2D uDepth;
uniform sampler2D uShadowAtlas;
uniform mat4 uView;
uniform mat4 uInvView;
uniform mat4 uInvProjection;
uniform mat4 uCascadeVP[4];
uniform vec4 uCascadeSplits;
uniform int uCascadeCount;
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uFogColor;
uniform float uFogDensity;
uniform float uFogStart;
uniform float uFogHeight;
uniform float uFogHeightFalloff;
uniform float uIntensity;
uniform int uSteps;
uniform vec2 uInvResolution;
uniform int uHasDepth;

float hash12(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float cascadeShadow(vec3 worldPos) {
    if (uCascadeCount <= 0)
        return 1.0;
    for (int c = 0; c < 4; c++) {
        if (c >= uCascadeCount) break;
        vec4 lightClip = uCascadeVP[c] * vec4(worldPos, 1.0);
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
    vec4 view = uInvProjection * clip;
    view /= max(view.w, 0.0001);
    vec4 world = uInvView * view;
    return world.xyz;
}

void main() {
    vec3 scene = texture(uColor, vUv).rgb;
    if (uHasDepth == 0 || uSteps < 1 || uIntensity <= 0.0) {
        FragColor = vec4(scene, 1.0);
        return;
    }
    float depth = texture(uDepth, vUv).r;
    vec3 camPos = (uInvView * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
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
    int steps = uSteps;
    if (steps < 8) steps = 8;
    if (steps > 32) steps = 32;
    float stepLen = dist / float(steps);
    float jitter = hash12(vUv * 1.731 + uInvResolution * 17.0);
    vec3 pos = camPos + dir * stepLen * (0.25 + jitter * 0.75);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float densityScale = max(uFogDensity, 0.00005) * max(uIntensity, 0.0);
    vec3 lightCol = uLightColor * max(uLightIntensity, 0.2);
    for (int i = 0; i < 32; i++) {
        if (i >= steps) break;
        float travelled = distance(camPos, pos);
        if (travelled >= uFogStart && travelled <= dist) {
            float heightFog = exp(-uFogHeightFalloff * max(pos.z - uFogHeight, 0.0));
            float density = densityScale * heightFog;
            float lit = cascadeShadow(pos);
            float scatter = 1.0 - exp(-density * stepLen);
            accum += transmittance * scatter * mix(0.55, 1.0, lit) * lightCol * uFogColor;
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
