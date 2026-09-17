// Folder: SiegeEngine/Core/GPU/PostProcess
// File: ColorComposeShaders.cs
namespace SiegeEngine.Core.GPU.PostProcess
{
    public static class ColorComposeShaders
    {
        public const string FullscreenVertex = AntiAliasingShaders.FullscreenVertex;

        public const string ExtractFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D Color;

            float Luma(vec3 c)
            {
                return dot(c, vec3(0.2126, 0.7152, 0.0722));
            }

            void main()
            {
                vec3 hdr = max(texture(Color, vUv).rgb, vec3(0.0));
                float luma = Luma(hdr);
                float knee = max(Knee, 0.0001);
                float soft = luma - Threshold + knee;
                soft = clamp(soft, 0.0, 2.0 * knee);
                soft = (soft * soft) / (4.0 * knee);
                float contrib = max(luma - Threshold, soft);
                vec3 bright = hdr * (contrib / max(luma, 1e-4));
                FragColor = vec4(bright, 1.0);
            }";

        public const string DownsampleFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D Color;

            void main()
            {
                vec2 rcp = InvResolution.xy;
                vec3 a = texture(Color, vUv + vec2(-rcp.x, -rcp.y)).rgb;
                vec3 b = texture(Color, vUv + vec2( rcp.x, -rcp.y)).rgb;
                vec3 c = texture(Color, vUv + vec2(-rcp.x,  rcp.y)).rgb;
                vec3 d = texture(Color, vUv + vec2( rcp.x,  rcp.y)).rgb;
                vec3 e = texture(Color, vUv).rgb;
                FragColor = vec4((a + b + c + d) * 0.125 + e * 0.5, 1.0);
            }";

        public const string UpsampleFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uLow;
            uniform sampler2D uHigh;

            void main()
            {
                vec2 rcp = InvResolution.xy;
                vec3 blur = texture(uLow, vUv).rgb * 4.0;
                blur += texture(uLow, vUv + vec2(-rcp.x, 0.0)).rgb;
                blur += texture(uLow, vUv + vec2( rcp.x, 0.0)).rgb;
                blur += texture(uLow, vUv + vec2(0.0, -rcp.y)).rgb;
                blur += texture(uLow, vUv + vec2(0.0,  rcp.y)).rgb;
                blur += texture(uLow, vUv + vec2(-rcp.x, -rcp.y)).rgb;
                blur += texture(uLow, vUv + vec2( rcp.x, -rcp.y)).rgb;
                blur += texture(uLow, vUv + vec2(-rcp.x,  rcp.y)).rgb;
                blur += texture(uLow, vUv + vec2( rcp.x,  rcp.y)).rgb;
                blur *= 1.0 / 12.0;
                vec3 high = texture(uHigh, vUv).rgb;
                FragColor = vec4(high + blur * uAddLow, 1.0);
            }";

        public const string ComposeFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D Color;
            uniform sampler2D uBloom;
            uniform sampler2D AdaptedLuma;

            float Luma(vec3 c)
            {
                return dot(c, vec3(0.2126, 0.7152, 0.0722));
            }

            vec3 AcesFilm(vec3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
            }

            vec3 Reinhard(vec3 x)
            {
                return x / (x + vec3(1.0));
            }

            void main()
            {
                vec3 hdr = max(texture(Color, vUv).rgb, vec3(0.0));
                if (HasBloom == 1)
                    hdr += max(texture(uBloom, vUv).rgb, vec3(0.0)) * BloomIntensity;

                float ev = max(Exposure, 0.05);
                if (AutoExposure == 1)
                {
                    float adapted = max(texture(AdaptedLuma, vec2(0.5)).r, 0.04);
                    float ratio = clamp(TargetLuma / adapted, 0.78, 1.35);
                    ev *= mix(1.0, ratio, 0.45);
                }
                hdr *= ev;

                vec3 mapped = hdr;
                if (Tonemap == 1)
                    mapped = AcesFilm(hdr);
                else if (Tonemap == 2)
                    mapped = Reinhard(hdr);
                else
                    mapped = clamp(hdr, 0.0, 1.0);

                mapped.r += Temperature * 0.12;
                mapped.b -= Temperature * 0.12;
                mapped = clamp(mapped, 0.0, 1.0);

                mapped = (mapped - vec3(0.5)) * Contrast + vec3(0.5);
                float luma = Luma(mapped);
                mapped = mix(vec3(luma), mapped, Saturation);
                mapped = clamp(mapped, 0.0, 1.0);

                FragColor = vec4(mapped, 1.0);
            }";
 
        public const string LumaFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D Color;

            void main()
            {
                vec3 hdr = max(texture(Color, vUv).rgb, vec3(0.0));
                float luma = dot(hdr, vec3(0.2126, 0.7152, 0.0722));
                vec2 d = vUv - vec2(0.5);
                float w = exp(-dot(d, d) * 10.0);
                FragColor = vec4(luma * w, w, 0.0, 1.0);
            }";

        public const string LumaDownFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D Color;

            void main()
            {
                vec2 rcp = InvResolution.xy;
                vec4 acc = vec4(0.0);
                acc += texture(Color, vUv + vec2(-rcp.x, -rcp.y));
                acc += texture(Color, vUv + vec2( rcp.x, -rcp.y));
                acc += texture(Color, vUv + vec2(-rcp.x,  rcp.y));
                acc += texture(Color, vUv + vec2( rcp.x,  rcp.y));
                FragColor = acc * 0.25;
            }";

        public const string AdaptFragment = @"            #version 330 core
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
    vec4 LightPos;
};

            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uCurrent;
            uniform sampler2D uPrevious;

            void main()
            {
                vec4 cur = texture(uCurrent, vec2(0.5));
                float current = cur.r / max(cur.g, 1e-4);
                float prev = current;
                if (HasPrev == 1)
                    prev = texture(uPrevious, vec2(0.5)).r;
                float adapted = mix(prev, current, clamp(Adapt, 0.0, 1.0));
                FragColor = vec4(adapted, 1.0, 0.0, 1.0);
            }";

    }
}
