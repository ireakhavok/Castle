// Folder: SiegeEngine.Core.GPU.Shaders
// File: AnimationShader.cs
namespace SiegeEngine.Core.GPU.Shaders
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
uniform mat4 uBoneMatrices[100];
uniform mat3 uNormalMatrices[100];
void main()
{
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    float sumWeights = 0.0;
    if (uHasBones == 1) {
        for (int i = 0; i < 4; i++) {
            int boneIndex = int(aBoneIDs[i]);
            if (boneIndex < 0 || boneIndex >= 100) continue;
            mat4 boneTransform = uBoneMatrices[boneIndex];
            vec4 localPosition = boneTransform * vec4(aPosition, 1.0);
            totalPosition += localPosition * aWeights[i];
            vec3 localNormal = uNormalMatrices[boneIndex] * aNormal;
            totalNormal += localNormal * aWeights[i];
            vec3 localTangent = uNormalMatrices[boneIndex] * aTangent;
            totalTangent += localTangent * aWeights[i];
            sumWeights += aWeights[i];
        }
        if (sumWeights > 0.001) {
            totalPosition /= sumWeights;
            totalNormal /= sumWeights;
            totalTangent /= sumWeights;
            totalNormal = normalize(totalNormal);
            totalTangent = normalize(totalTangent);
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
    mat3 normalMatrix = transpose(inverse(mat3(uModel)));
    Normal = normalize(normalMatrix * totalNormal);
    vec3 T = normalize(normalMatrix * totalTangent);
    vec3 N = Normal;
    T = normalize(T - dot(T, N) * N);
    vec3 B = cross(N, T);
    TBN = mat3(T, B, N);
    FragPos = vec3(uModel * totalPosition);
    TexCoord = aTexCoord;
    MaterialIndex = aMaterialIndex;
    gl_Position = uProjection * uView * uModel * totalPosition;
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

// NEW world-aligned uniforms (required by ModelRenderer)
uniform int uHasWorldAligned;
uniform int uMappingMode[4];
uniform vec2 uTiling[4];
uniform vec2 uOffset[4];
uniform float uRotation[4];
uniform float uBlendSharpness[4];

vec2 WorldPlanarUV(vec3 worldPos, vec3 normal, int axis, vec2 tiling, vec2 offset)
{
    vec2 uv;
    if (axis == 0) uv = worldPos.yz;
    else if (axis == 1) uv = worldPos.xz;
    else uv = worldPos.xy;
    return uv * tiling + offset;
}

vec3 TriplanarSample(sampler2D tex, vec3 worldPos, vec3 normal, vec2 tiling, vec2 offset, float sharpness)
{
    vec3 blend = abs(normal);
    blend /= blend.x + blend.y + blend.z + 0.0001;
    blend = pow(blend, vec3(sharpness));
    blend /= blend.x + blend.y + blend.z;

    vec2 uvX = worldPos.yz * tiling + offset;
    vec2 uvY = worldPos.xz * tiling + offset;
    vec2 uvZ = worldPos.xy * tiling + offset;

    vec3 colX = texture(tex, uvX).rgb;
    vec3 colY = texture(tex, uvY).rgb;
    vec3 colZ = texture(tex, uvZ).rgb;
    return colX * blend.x + colY * blend.y + colZ * blend.z;
}

void main()
{
    int matIdx = int(MaterialIndex);
    vec3 albedo = texture(uAlbedoMap[matIdx], TexCoord).rgb;
    vec3 normalMap = texture(uNormalMap[matIdx], TexCoord).rgb * 2.0 - 1.0;
    float metallic = texture(uMetallicMap[matIdx], TexCoord).r;

    vec3 normal = normalize(TBN * normalMap);

    // World-aligned texturing (if enabled)
    if (uHasWorldAligned == 1 && uMappingMode[matIdx] != 0)
    {
        if (uMappingMode[matIdx] == 2) // Triplanar
        {
            albedo = TriplanarSample(uAlbedoMap[matIdx], FragPos, normal, uTiling[matIdx], uOffset[matIdx], uBlendSharpness[matIdx]);
        }
        else // WorldPlanar
        {
            int axis = abs(normal.x) > abs(normal.y) && abs(normal.x) > abs(normal.z) ? 0 : (abs(normal.y) > abs(normal.z) ? 1 : 2);
            vec2 worldUV = WorldPlanarUV(FragPos, normal, axis, uTiling[matIdx], uOffset[matIdx]);
            albedo = texture(uAlbedoMap[matIdx], worldUV).rgb;
        }
    }

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