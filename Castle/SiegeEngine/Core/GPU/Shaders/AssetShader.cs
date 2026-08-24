namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AssetShader
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
uniform mat4 uBoneMatrices[100]; // Adjust max bones as needed
uniform mat3 uNormalMatrices[100]; // For normal and tangent transformations
void main()
{
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    if (uHasBones == 1) {
        for (int i = 0; i < 4; i++) {
            int boneIndex = int(aBoneIDs[i]);
            if (boneIndex == -1) continue;
            vec4 localPosition = uBoneMatrices[boneIndex] * vec4(aPosition, 1.0);
            totalPosition += localPosition * aWeights[i];
            vec3 localNormal = uNormalMatrices[boneIndex] * aNormal;
            totalNormal += localNormal * aWeights[i];
            vec3 localTangent = uNormalMatrices[boneIndex] * aTangent;
            totalTangent += localTangent * aWeights[i];
        }
        totalPosition /= (aWeights[0] + aWeights[1] + aWeights[2] + aWeights[3]);
        totalNormal = normalize(totalNormal);
        totalTangent = normalize(totalTangent);
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