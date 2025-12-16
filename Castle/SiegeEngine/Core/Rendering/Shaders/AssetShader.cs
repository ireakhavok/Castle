// Folder: SiegeEngine.Rendering.Shaders
// File: AssetShader.cs
namespace SiegeEngine.Core.Rendering.Shaders
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

void main()
{
    vec4 pos = vec4(aPosition, 1.0);
    vec3 norm = aNormal;
    vec3 tang = aTangent;
    if (uHasBones == 1) {
        // Apply bone transform, assuming no skinning weights for simplicity
        // In full implementation, use vertex weights and bone indices
        pos = uBoneTransforms[0] * pos; // Placeholder, use actual bone index
        norm = mat3(uBoneTransforms[0]) * norm;
        tang = mat3(uBoneTransforms[0]) * tang;
    }
    gl_Position = uProjection * uView * uModel * pos;
    FragPos = vec3(uModel * pos);
    Normal = mat3(transpose(inverse(uModel))) * norm;
    TexCoord = aTexCoord;
    MaterialIndex = aMaterialIndex;

    vec3 T = normalize(mat3(uModel) * tang);
    vec3 N = normalize(mat3(uModel) * norm);
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