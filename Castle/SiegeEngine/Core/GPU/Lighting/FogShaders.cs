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

float cascadeShadow(vec3 worldPos, float viewZ) {
    if (uCascadeCount <= 0)
        return 1.0;
    int cascade = 0;
    if (uCascadeCount > 1 && viewZ > uCascadeSplits.x) cascade = 1;
    if (uCascadeCount > 2 && viewZ > uCascadeSplits.y) cascade = 2;
    if (uCascadeCount > 3 && viewZ > uCascadeSplits.z) cascade = 3;
    vec4 lightClip = uCascadeVP[cascade] * vec4(worldPos, 1.0);
    vec3 proj = lightClip.xyz / max(lightClip.w, 0.0001);
    proj = proj * 0.5 + 0.5;
    if (proj.x < 0.0 || proj.x > 1.0 || proj.y < 0.0 || proj.y > 1.0 || proj.z > 1.0)
        return 1.0;
    float cell = 0.5;
    vec2 atlas = vec2(float(cascade - (cascade / 2) * 2), float(cascade / 2)) * cell + proj.xy * cell;
    float closest = texture(uShadowAtlas, atlas).r;
    return proj.z - 0.003 > closest ? 0.35 : 1.0;
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
    vec3 worldEnd;
    bool sky = depth >= 0.9995;
    if (sky) {
        vec3 farPoint = reconstructWorld(vUv, 0.998);
        vec3 dirSky = normalize(farPoint - camPos);
        worldEnd = camPos + dirSky * 400.0;
    } else {
        worldEnd = reconstructWorld(vUv, depth);
    }
    vec3 delta = worldEnd - camPos;
    float dist = length(delta);
    if (dist < 0.01) {
        FragColor = vec4(scene, 1.0);
        return;
    }
    vec3 dir = delta / dist;
    dist = min(dist, 400.0);
    int steps = max(uSteps, 1);
    float stepLen = dist / float(steps);
    vec3 pos = camPos + dir * (stepLen * 0.5);
    vec3 accum = vec3(0.0);
    float transmittance = 1.0;
    float densityScale = max(uFogDensity, 0.0001) * max(uIntensity, 0.0);
    for (int i = 0; i < 32; i++) {
        if (i >= steps) break;
        float travelled = float(i) * stepLen;
        if (travelled < uFogStart) {
            pos += dir * stepLen;
            continue;
        }
        float heightFog = exp(-uFogHeightFalloff * max(pos.z - uFogHeight, 0.0));
        float density = densityScale * heightFog;
        vec4 viewPos = inverse(uInvView) * vec4(pos, 1.0);
        float lit = cascadeShadow(pos, abs(viewPos.z));
        float scatter = 1.0 - exp(-density * stepLen);
        accum += transmittance * scatter * lit * uLightColor * max(uLightIntensity, 0.15) * uFogColor;
        transmittance *= exp(-density * stepLen);
        pos += dir * stepLen;
        if (transmittance < 0.02) break;
    }
    vec3 result = scene * transmittance + accum;
    FragColor = vec4(result, 1.0);
}";
    }
}
