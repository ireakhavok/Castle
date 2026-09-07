// Folder: SiegeEngine/Core/GPU/Lighting
// File: ShadowShaders.cs
namespace SiegeEngine.Core.GPU.Lighting
{
    public static class ShadowShaders
    {
        public const string DepthVertex = @"#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 2) in vec2 aTexCoord;
layout (location = 4) in float aMaterialIndex;
layout (location = 6) in vec4 aBoneIDs;
layout (location = 7) in vec4 aBoneWeights;
uniform mat4 uLightVP;
uniform mat4 uModel;
uniform int uHasBones;
uniform mat4 uBoneTransforms[128];
out vec3 vWorldPos;
out float vDepth01;
out vec2 vTexCoord;
out float vMaterialIndex;
void main() {
    vTexCoord = aTexCoord;
    vMaterialIndex = aMaterialIndex;
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
    vWorldPos = world.xyz;
    vec4 clip = uLightVP * world;
    gl_Position = clip;
    vDepth01 = clip.z / max(clip.w, 0.0001) * 0.5 + 0.5;
}";

        public const string DepthFragment = @"#version 330 core
in vec3 vWorldPos;
in float vDepth01;
in vec2 vTexCoord;
in float vMaterialIndex;
uniform int uLinearDepth;
uniform vec3 uLightPos;
uniform float uFarPlane;
uniform int uHasOpacity;
uniform int uOpacitySlots;
uniform sampler2D uOpacityMap;
out vec4 FragColor;
void main() {
    int matIdx = int(vMaterialIndex);
    if (matIdx < 0) matIdx = 0;
    if (matIdx > 3) matIdx = 3;
    if (uHasOpacity == 1 && ((uOpacitySlots >> matIdx) & 1) == 1) {
        float mask = texture(uOpacityMap, vTexCoord).r;
        if (mask <= 0.0) discard;
    }
    if (uLinearDepth == 1) {
        float dist = length(vWorldPos - uLightPos);
        gl_FragDepth = clamp(dist / max(uFarPlane, 0.001), 0.0, 1.0);
    } else {
        gl_FragDepth = clamp(vDepth01, 0.0, 1.0);
    }
    FragColor = vec4(1.0);
}";
    }
}
