namespace SiegeEngine.Core.GPU.Shaders.OpenGL
{
    public static class AssetShader
    {
        public const string VertexShaderSource = @"#version 330 core


layout(std140) uniform FrameCB
{
    mat4 View;
    mat4 Projection;
    vec4 ViewPos;
    float Time;
    int HasTexture;
    float PadFrame0;
    float PadFrame1;
};
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
layout(std140) uniform LightCB
{
    vec4 LightDir;
    vec4 LightColor;
    vec4 AmbientColor;
    vec4 LightViewPos;
    float LightIntensity;
    float AmbientStrength;
    float SpecularStrength;
    float Shininess;
    int PointCount;
    int SpotCount;
    int FogMode;
    int PadLight0;
    vec4 PointPos0;
    vec4 PointPos1;
    vec4 PointPos2;
    vec4 PointPos3;
    vec4 PointColor0;
    vec4 PointColor1;
    vec4 PointColor2;
    vec4 PointColor3;
    vec4 PointIntensityRange0;
    vec4 PointIntensityRange1;
    vec4 PointIntensityRange2;
    vec4 PointIntensityRange3;
    vec4 SpotPos0;
    vec4 SpotPos1;
    vec4 SpotDir0;
    vec4 SpotDir1;
    vec4 SpotColor0;
    vec4 SpotColor1;
    vec4 SpotIntensityRange0;
    vec4 SpotIntensityRange1;
    vec4 SpotCone0;
    vec4 SpotCone1;
    vec4 FogColor;
    float FogDensity;
    float FogStart;
    float FogHeight;
    float FogHeightFalloff;
};
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
uniform mat4 BoneTransforms[100]; // Adjust max bones as needed
uniform mat3 uNormalMatrices[100]; // For normal and tangent transformations
void main()
{
    vec4 totalPosition = vec4(0.0);
    vec3 totalNormal = vec3(0.0);
    vec3 totalTangent = vec3(0.0);
    if (HasBones == 1) {
        for (int i = 0; i < 4; i++) {
            int boneIndex = int(aBoneIDs[i]);
            if (boneIndex == -1) continue;
            vec4 localPosition = BoneTransforms[boneIndex] * vec4(aPosition, 1.0);
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
    gl_Position = Projection * View * Model * totalPosition;
    FragPos = vec3(Model * totalPosition);
    Normal = mat3(transpose(inverse(Model))) * totalNormal;
    TexCoord = aTexCoord;
    MaterialIndex = aMaterialIndex;
    vec3 T = normalize(mat3(Model) * totalTangent);
    vec3 N = normalize(mat3(Model) * totalNormal);
    T = normalize(T - dot(T, N) * N);
    vec3 B = cross(N, T);
    TBN = mat3(T, B, N);
}";
        public const string FragmentShaderSource = @"#version 330 core


layout(std140) uniform FrameCB
{
    mat4 View;
    mat4 Projection;
    vec4 ViewPos;
    float Time;
    int HasTexture;
    float PadFrame0;
    float PadFrame1;
};
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
layout(std140) uniform LightCB
{
    vec4 LightDir;
    vec4 LightColor;
    vec4 AmbientColor;
    vec4 LightViewPos;
    float LightIntensity;
    float AmbientStrength;
    float SpecularStrength;
    float Shininess;
    int PointCount;
    int SpotCount;
    int FogMode;
    int PadLight0;
    vec4 PointPos0;
    vec4 PointPos1;
    vec4 PointPos2;
    vec4 PointPos3;
    vec4 PointColor0;
    vec4 PointColor1;
    vec4 PointColor2;
    vec4 PointColor3;
    vec4 PointIntensityRange0;
    vec4 PointIntensityRange1;
    vec4 PointIntensityRange2;
    vec4 PointIntensityRange3;
    vec4 SpotPos0;
    vec4 SpotPos1;
    vec4 SpotDir0;
    vec4 SpotDir1;
    vec4 SpotColor0;
    vec4 SpotColor1;
    vec4 SpotIntensityRange0;
    vec4 SpotIntensityRange1;
    vec4 SpotCone0;
    vec4 SpotCone1;
    vec4 FogColor;
    float FogDensity;
    float FogStart;
    float FogHeight;
    float FogHeightFalloff;
};
in vec2 TexCoord;
in vec3 Normal;
in vec3 FragPos;
in float MaterialIndex;
in mat3 TBN;
out vec4 FragColor;
uniform sampler2D uAlbedoMap[4];
uniform sampler2D uNormalMap[4];
uniform sampler2D uMetallicMap[4];
void main()
{
    int matIdx = int(MaterialIndex);
    vec3 albedo = texture(uAlbedoMap[matIdx], TexCoord).rgb;
    vec3 normalMap = texture(uNormalMap[matIdx], TexCoord).rgb * 2.0 - 1.0;
    float metallic = texture(uMetallicMap[matIdx], TexCoord).r;
    vec3 normal = normalize(TBN * normalMap);
    vec3 ambient = AmbientStrength * albedo;
    vec3 lightDir = normalize(-LightDir.xyz);
    float diff = max(dot(normal, lightDir), 0.0);
    vec3 diffuse = diff * albedo * LightColor.xyz * LightIntensity;
    vec3 viewDir = normalize(ViewPos.xyz - FragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(normal, halfwayDir), 0.0), Shininess);
    vec3 specular = SpecularStrength * spec * LightColor.xyz * LightIntensity * metallic;
    FragColor = vec4(ambient + diffuse + specular, 1.0);
}";
    }
}
