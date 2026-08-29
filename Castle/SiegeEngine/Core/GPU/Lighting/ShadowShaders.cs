// Folder: SiegeEngine/Core/GPU/Lighting
// File: ShadowShaders.cs
namespace SiegeEngine.Core.GPU.Lighting
{
    public static class ShadowShaders
    {
        public const string DepthVertex = @"#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 6) in vec4 aBoneIDs;
layout (location = 7) in vec4 aBoneWeights;
uniform mat4 uLightVP;
uniform mat4 uModel;
uniform int uHasBones;
uniform mat4 uBoneTransforms[128];
uniform vec3 uLightDir;
uniform float uCasterDepthBias;
out vec3 vWorldPos;
void main() {
    vec4 local = vec4(aPosition, 1.0);
    if (uHasBones == 1) {
        vec4 skinned = vec4(0.0);
        for (int i = 0; i < 4; i++) {
            int id = int(aBoneIDs[i]);
            if (id < 0 || id >= 128) continue;
            skinned += (uBoneTransforms[id] * local) * aBoneWeights[i];
        }
        if (dot(skinned, skinned) > 0.0001)
            local = skinned;
    }
    vec4 world = uModel * local;
    // Sun pass only: push the caster away from the light so stored depth
    // sits behind the real front face (GL_LESS). Receiver then cannot
    // fail against its own silhouette. Point pass keeps bias at 0.
    if (uCasterDepthBias > 0.0 && dot(uLightDir, uLightDir) > 0.0001)
        world.xyz += normalize(uLightDir) * uCasterDepthBias;
    vWorldPos = world.xyz;
    gl_Position = uLightVP * world;
}";

        public const string DepthFragment = @"#version 330 core
in vec3 vWorldPos;
uniform int uLinearDepth;
uniform vec3 uLightPos;
uniform float uFarPlane;
out vec4 FragColor;
void main() {
    if (uLinearDepth == 1) {
        float dist = length(vWorldPos - uLightPos);
        gl_FragDepth = clamp(dist / max(uFarPlane, 0.001), 0.0, 1.0);
    }
    FragColor = vec4(1.0);
}";
    }
}
