// Folder: SiegeEngine/Core/GPU/Lighting
// File: ShadowShaders.cs
namespace SiegeEngine.Core.GPU.Lighting
{
    public static class ShadowShaders
    {
        public const string DepthVertex = @"#version 330 core

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
layout(std140) uniform SkinCB
{
    mat4 BoneTransforms[128];
};
layout(std140) uniform MaterialCB
{
    int HasOpacity;
    int OpacitySlots;
    int DebugTextureOnly;
    int DebugMaterialIndex;
    int HasWorldAligned;
    int MappingMode0;
    int MappingMode1;
    int MappingMode2;
    int MappingMode3;
    int PadMat0;
    int PadMat1;
    int PadMat2;
    vec4 Tiling0;
    vec4 Tiling1;
    vec4 Tiling2;
    vec4 Tiling3;
    vec4 Offset0;
    vec4 Offset1;
    vec4 Offset2;
    vec4 Offset3;
    vec4 Rotation;
    vec4 BlendSharpness;
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
layout (location = 0) in vec3 aPosition;
layout (location = 2) in vec2 aTexCoord;
layout (location = 4) in float aMaterialIndex;
layout (location = 6) in vec4 aBoneIDs;
layout (location = 7) in vec4 aBoneWeights;
out vec3 vWorldPos;
out float vDepth01;
out vec2 vTexCoord;
out float vMaterialIndex;
void main() {
    vTexCoord = aTexCoord;
    vMaterialIndex = aMaterialIndex;
    vec4 local = vec4(aPosition, 1.0);
    if (HasBones == 1) {
        vec4 skinned = vec4(0.0);
        for (int i = 0; i < 4; i++) {
            int id = int(aBoneIDs[i]);
            if (id < 0 || id >= 128) continue;
            skinned += (BoneTransforms[id] * local) * aBoneWeights[i];
        }
        if (dot(skinned, skinned) > 0.0001)
            local = skinned;
    }
    vec4 world = Model * local;
    vWorldPos = world.xyz;
    vec4 clip = CascadeVP0 * world;
    gl_Position = clip;
    vDepth01 = clip.z / max(clip.w, 0.0001) * 0.5 + 0.5;
}";

        public const string DepthFragment = @"#version 330 core

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
layout(std140) uniform SkinCB
{
    mat4 BoneTransforms[128];
};
layout(std140) uniform MaterialCB
{
    int HasOpacity;
    int OpacitySlots;
    int DebugTextureOnly;
    int DebugMaterialIndex;
    int HasWorldAligned;
    int MappingMode0;
    int MappingMode1;
    int MappingMode2;
    int MappingMode3;
    int PadMat0;
    int PadMat1;
    int PadMat2;
    vec4 Tiling0;
    vec4 Tiling1;
    vec4 Tiling2;
    vec4 Tiling3;
    vec4 Offset0;
    vec4 Offset1;
    vec4 Offset2;
    vec4 Offset3;
    vec4 Rotation;
    vec4 BlendSharpness;
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
in vec3 vWorldPos;
in float vDepth01;
in vec2 vTexCoord;
in float vMaterialIndex;
uniform sampler2D uOpacityMap;
out vec4 FragColor;
void main() {
    int matIdx = int(vMaterialIndex);
    if (matIdx < 0) matIdx = 0;
    if (matIdx > 3) matIdx = 3;
    if (HasOpacity == 1 && ((OpacitySlots >> matIdx) & 1) == 1) {
        float mask = texture(uOpacityMap, vTexCoord).r;
        if (mask <= 0.0) discard;
    }
    if (int(LinearDepth) == 1) {
        float dist = length(vWorldPos - LightPos.xyz);
        gl_FragDepth = clamp(dist / max(FarPlane, 0.001), 0.0, 1.0);
    } else {
        gl_FragDepth = clamp(vDepth01, 0.0, 1.0);
    }
    FragColor = vec4(1.0);
}";
    }
}
