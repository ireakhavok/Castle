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
    gl_Position = uLightVP * uModel * local;
}";

        public const string DepthFragment = @"#version 330 core
out vec4 FragColor;
void main() {
    FragColor = vec4(1.0);
}";
    }
}
