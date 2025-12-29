// Folder: SiegeEngine.Core
// File: Rendering/Shaders/AnimationShader.cs
namespace SiegeEngine.Core.Rendering.Shaders
{
    public static class AnimationShader
    {
        public const string VertexShaderSource = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec3 aNormal;
layout(location = 4) in float aMaterialIndex;
layout(location = 5) in vec3 aTangent;
layout(location = 6) in vec4 aBoneIDs;
layout(location = 7) in vec4 aWeights;
out vec2 TexCoord;
out vec3 Normal;
out vec3 FragPos;
out float MaterialIndex;
out mat3 TBN;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform int uHasBones;
uniform mat4 uBoneTransforms[100]; // Adjust max bones as needed
uniform int uUseDualQuat = 1; // 1 to enable dual quaternion skinning

// Dual quaternion functions
vec4 blendQuat(vec4 q1, vec4 q2, float w) {
    return normalize(q1 * (1.0 - w) + q2 * w);
}

vec3 blendTrans(vec3 t1, vec3 t2, float w) {
    return t1 * (1.0 - w) + t2 * w;
}

void main()
{
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    float totalWeight = 0.0;
    if (uHasBones == 1) {
        if (uUseDualQuat == 1) {
            // Dual quaternion implementation (simplified for test; expand with actual dual quat parsing if needed)
            // Assuming uBoneTransforms are rotation quats + translation (need to adjust parser for dual)
            // Placeholder: Fall back to linear for now, but log to implement full
            // Full impl: Convert bone mats to dual quats, blend, apply
            for (int i = 0; i < 4; i++) {
                int boneIndex = int(aBoneIDs[i]);
                if (boneIndex < 0 || boneIndex >= 100) continue;
                mat4 boneTransform = uBoneTransforms[boneIndex];
                vec4 localPosition = boneTransform * vec4(aPosition, 1.0);
                totalPosition += localPosition * aWeights[i];
                vec3 localNormal = mat3(boneTransform) * aNormal;
                totalNormal += localNormal * aWeights[i];
                vec3 localTangent = mat3(boneTransform) * aTangent;
                totalTangent += localTangent * aWeights[i];
                totalWeight += aWeights[i];
            }
        } else {
            // Original linear blend
            for (int i = 0; i < 4; i++) {
                int boneIndex = int(aBoneIDs[i]);
                if (boneIndex < 0 || boneIndex >= 100) continue;
                mat4 boneTransform = uBoneTransforms[boneIndex];
                vec4 localPosition = boneTransform * vec4(aPosition, 1.0);
                totalPosition += localPosition * aWeights[i];
                vec3 localNormal = mat3(boneTransform) * aNormal;
                totalNormal += localNormal * aWeights[i];
                vec3 localTangent = mat3(boneTransform) * aTangent;
                totalTangent += localTangent * aWeights[i];
                totalWeight += aWeights[i];
            }
        }
        if (totalWeight > 0.0) {
            totalPosition /= totalWeight;
            totalNormal = normalize(totalNormal / totalWeight);
            totalTangent = normalize(totalTangent / totalWeight);
        } else {
            totalPosition = vec4(aPosition, 1.0);
            totalNormal = aNormal;
            totalTangent = aTangent;
        }
    } else {
        totalPosition = vec4(aPosition, 1.0);
        totalNormal = aNormal;
        totalTangent = aTangent;
    }
    gl_Position = uProjection * uView * uModel * totalPosition;
    FragPos = vec3(uModel * totalPosition);
    Normal = mat3(transpose(inverse(uModel))) * totalNormal;
    TexCoord = aTexCoord;
    MaterialIndex = aMaterialIndex;
    vec3 T = normalize(mat3(uModel) * totalTangent);
    vec3 N = normalize(mat3(uModel) * totalNormal);
    T = normalize(T - dot(T, N) * N);
    vec3 B = cross(N, T);
    TBN = mat3(T, B, N);
}";
        public const string FragmentShaderSource = @"
#version 330 core
in vec2 TexCoord;
in vec3 Normal;
in vec3 FragPos;
in float MaterialIndex;
in mat3 TBN;
out vec4 FragColor;
uniform sampler2D uAlbedoMap[4];
uniform sampler2D uNormalMap[4];
uniform sampler2D uMetallicMap[4];
uniform vec3 uLightDir;
uniform vec3 uLightColor;
uniform float uLightIntensity;
uniform vec3 uViewPos;
uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform float uShininess;
void main()
{
    int matIdx = int(MaterialIndex);
    vec3 albedo = texture(uAlbedoMap[matIdx], TexCoord).rgb;
    vec3 normalMap = texture(uNormalMap[matIdx], TexCoord).rgb * 2.0 - 1.0;
    float metallic = texture(uMetallicMap[matIdx], TexCoord).r;
    vec3 normal = normalize(TBN * normalMap);
    vec3 ambient = uAmbientStrength * albedo;
    vec3 lightDir = normalize(-uLightDir);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = diff * albedo * uLightColor * uLightIntensity;
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(normal, halfwayDir), 0.0), uShininess);
    vec3 specular = uSpecularStrength * spec * uLightColor * uLightIntensity * metallic;
    FragColor = vec4(ambient + diffuse + specular, 1.0);
}";
    }
}