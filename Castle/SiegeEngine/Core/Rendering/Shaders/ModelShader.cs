// Folder: SiegeEngine.Core.Rendering.Shaders
// File: ModelShader.cs
namespace SiegeEngine.Core.Rendering.Shaders
{
    public static class ModelShader
    {
        public const string VertexShaderSource = @" #version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 2) in vec2 aTexCoord;
layout (location = 3) in vec3 aNormal;
layout (location = 4) in float aMaterialIndex;
layout (location = 5) in vec3 aTangent;
layout (location = 6) in vec4 aBoneIDs;
layout (location = 7) in vec4 aBoneWeights;

out vec2 vTexCoord;
out vec3 vNormal;
out vec3 vTangent;
out vec3 vPosition;
out float vMaterialIndex;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform mat4 uBoneTransforms[128];
uniform int uHasBones;

void main() {
    vTexCoord = vec2(aTexCoord.x, aTexCoord.y);
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    if (uHasBones == 1) {
        for (int i = 0; i < 4; i++) {
            int id = int(aBoneIDs[i]);
            if (id < 0 || id >= 128) continue;
            mat4 boneMat = uBoneTransforms[id];
            vec4 localPos = boneMat * vec4(aPosition, 1.0);
            totalPosition += localPos * aBoneWeights[i];
            mat3 normalMat = transpose(inverse(mat3(boneMat)));
            vec3 localNorm = normalMat * aNormal;
            totalNormal += localNorm * aBoneWeights[i];
            vec3 localTan = normalMat * aTangent;
            totalTangent += localTan * aBoneWeights[i];
        }
        totalNormal = normalize(totalNormal);
        totalTangent = normalize(totalTangent);
    } else {
        totalPosition = vec4(aPosition, 1.0);
        totalNormal = aNormal;
        totalTangent = aTangent;
    }
    mat3 normalMatrix = transpose(inverse(mat3(uModel)));
    vNormal = normalize(normalMatrix * totalNormal);
    vTangent = normalize(normalMatrix * totalTangent);
    vPosition = vec3(uModel * totalPosition);
    vMaterialIndex = aMaterialIndex;
    gl_Position = uProjection * uView * uModel * totalPosition;
}";
        public const string FragmentShaderSource = @" #version 330 core
in vec2 vTexCoord;
in vec3 vNormal;
in vec3 vTangent;
in vec3 vPosition;
in float vMaterialIndex;

out vec4 FragColor;

uniform vec4 uLightDir;
uniform vec4 uLightColor;
uniform float uLightIntensity;
uniform vec4 uViewPos;
uniform float uAmbientStrength;
uniform float uSpecularStrength;
uniform float uShininess;

uniform sampler2D uAlbedoMap[4];
uniform sampler2D uNormalMap[4];
uniform sampler2D uMetallicMap[4];

uniform int uDebugTextureOnly;
uniform int uDebugMaterialIndex;

void main() {
    int matIdx = int(vMaterialIndex);
    if (matIdx < 0 || matIdx > 3) {
        FragColor = vec4(1.0, 0.0, 1.0, 1.0); // Magenta error
        return;
    }
    if (uDebugMaterialIndex == 1) {
        if (matIdx == 0) FragColor = vec4(1.0, 0.0, 0.0, 1.0);
        else if (matIdx == 1) FragColor = vec4(0.0, 1.0, 0.0, 1.0);
        else if (matIdx == 2) FragColor = vec4(0.0, 0.0, 1.0, 1.0);
        else FragColor = vec4(1.0, 1.0, 0.0, 1.0);
        return;
    }
    if (uDebugTextureOnly == 1) {
        FragColor = texture(uAlbedoMap[matIdx], vTexCoord);
        return;
    }

    vec3 materialDiffuse = vec3(1.0, 1.0, 1.0);
    if (textureSize(uAlbedoMap[matIdx], 0).x > 0) {
        materialDiffuse = texture(uAlbedoMap[matIdx], vTexCoord).rgb;
    }

    vec3 N = normalize(vNormal);
    vec3 norm = N;

    if (length(vTangent) > 0.001f && textureSize(uNormalMap[matIdx], 0).x > 0) {
        vec3 T = normalize(vTangent);
        T = normalize(T - dot(T, N) * N);
        vec3 B = cross(N, T);
        mat3 TBN = mat3(T, B, N);
        vec3 tangentNormal = texture(uNormalMap[matIdx], vTexCoord).rgb * 2.0 - 1.0;
        norm = normalize(TBN * tangentNormal);
    }

    float metallic = 0.0;
    if (textureSize(uMetallicMap[matIdx], 0).x > 0) {
        metallic = texture(uMetallicMap[matIdx], vTexCoord).r;
    }

    vec3 lightDir = normalize(-uLightDir.xyz);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor.xyz * uLightIntensity * materialDiffuse;

    vec3 ambient = uAmbientStrength * materialDiffuse;

    vec3 viewDir = normalize(uViewPos.xyz - vPosition);
    vec3 reflectDir = reflect(-lightDir, norm);
    float specStrength = uSpecularStrength * (1.0 - metallic);
    float shininess = uShininess * (1.0 - metallic);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
    vec3 specular = specStrength * spec * uLightColor.xyz * uLightIntensity * (1.0 - metallic);

    vec3 result = ambient + diffuse + specular;
    FragColor = vec4(result, 1.0);
}";
    }
}