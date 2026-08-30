// Folder: SiegeEngine/Core/GPU/PostProcess
// File: ColorComposeShaders.cs
namespace SiegeEngine.Core.GPU.PostProcess
{
    public static class ColorComposeShaders
    {
        public const string FullscreenVertex = AntiAliasingShaders.FullscreenVertex;

        public const string ExtractFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uColor;
            uniform float uThreshold;
            uniform float uKnee;

            float Luma(vec3 c)
            {
                return dot(c, vec3(0.2126, 0.7152, 0.0722));
            }

            void main()
            {
                vec3 hdr = max(texture(uColor, vUv).rgb, vec3(0.0));
                float luma = Luma(hdr);
                float knee = max(uKnee, 0.0001);
                float soft = luma - uThreshold + knee;
                soft = clamp(soft, 0.0, 2.0 * knee);
                soft = (soft * soft) / (4.0 * knee);
                float contrib = max(luma - uThreshold, soft);
                vec3 bright = hdr * (contrib / max(luma, 1e-4));
                FragColor = vec4(bright, 1.0);
            }";

        public const string DownsampleFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uColor;
            uniform vec2 uInvResolution;

            void main()
            {
                vec2 rcp = uInvResolution;
                vec3 a = texture(uColor, vUv + vec2(-rcp.x, -rcp.y)).rgb;
                vec3 b = texture(uColor, vUv + vec2( rcp.x, -rcp.y)).rgb;
                vec3 c = texture(uColor, vUv + vec2(-rcp.x,  rcp.y)).rgb;
                vec3 d = texture(uColor, vUv + vec2( rcp.x,  rcp.y)).rgb;
                vec3 e = texture(uColor, vUv).rgb;
                FragColor = vec4((a + b + c + d) * 0.125 + e * 0.5, 1.0);
            }";

        public const string UpsampleFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uLow;
            uniform sampler2D uHigh;
            uniform vec2 uInvResolution;
            uniform float uAddLow;

            void main()
            {
                vec2 rcp = uInvResolution;
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

        public const string ComposeFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uColor;
            uniform sampler2D uBloom;
            uniform int uHasBloom;
            uniform float uBloomIntensity;
            uniform float uExposure;
            uniform int uTonemap;
            uniform float uContrast;
            uniform float uSaturation;
            uniform float uTemperature;
            uniform int uAutoExposure;
            uniform sampler2D uAdaptedLuma;
            uniform float uTargetLuma;

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
                vec3 hdr = max(texture(uColor, vUv).rgb, vec3(0.0));
                if (uHasBloom == 1)
                    hdr += max(texture(uBloom, vUv).rgb, vec3(0.0)) * uBloomIntensity;

                float ev = max(uExposure, 0.05);
                if (uAutoExposure == 1)
                {
                    float adapted = max(texture(uAdaptedLuma, vec2(0.5)).r, 0.04);
                    float ratio = clamp(uTargetLuma / adapted, 0.78, 1.35);
                    ev *= mix(1.0, ratio, 0.45);
                }
                hdr *= ev;

                vec3 mapped = hdr;
                if (uTonemap == 1)
                    mapped = AcesFilm(hdr);
                else if (uTonemap == 2)
                    mapped = Reinhard(hdr);
                else
                    mapped = clamp(hdr, 0.0, 1.0);

                mapped.r += uTemperature * 0.12;
                mapped.b -= uTemperature * 0.12;
                mapped = clamp(mapped, 0.0, 1.0);

                mapped = (mapped - vec3(0.5)) * uContrast + vec3(0.5);
                float luma = Luma(mapped);
                mapped = mix(vec3(luma), mapped, uSaturation);
                mapped = clamp(mapped, 0.0, 1.0);

                FragColor = vec4(mapped, 1.0);
            }";
 
        public const string LumaFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uColor;

            void main()
            {
                vec3 hdr = max(texture(uColor, vUv).rgb, vec3(0.0));
                float luma = dot(hdr, vec3(0.2126, 0.7152, 0.0722));
                vec2 d = vUv - vec2(0.5);
                float w = exp(-dot(d, d) * 10.0);
                FragColor = vec4(luma * w, w, 0.0, 1.0);
            }";

        public const string LumaDownFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uColor;
            uniform vec2 uInvResolution;

            void main()
            {
                vec2 rcp = uInvResolution;
                vec4 acc = vec4(0.0);
                acc += texture(uColor, vUv + vec2(-rcp.x, -rcp.y));
                acc += texture(uColor, vUv + vec2( rcp.x, -rcp.y));
                acc += texture(uColor, vUv + vec2(-rcp.x,  rcp.y));
                acc += texture(uColor, vUv + vec2( rcp.x,  rcp.y));
                FragColor = acc * 0.25;
            }";

        public const string AdaptFragment = @"
            #version 330 core
            in vec2 vUv;
            out vec4 FragColor;
            uniform sampler2D uCurrent;
            uniform sampler2D uPrevious;
            uniform float uAdapt;
            uniform int uHasPrev;

            void main()
            {
                vec4 cur = texture(uCurrent, vec2(0.5));
                float current = cur.r / max(cur.g, 1e-4);
                float prev = current;
                if (uHasPrev == 1)
                    prev = texture(uPrevious, vec2(0.5)).r;
                float adapted = mix(prev, current, clamp(uAdapt, 0.0, 1.0));
                FragColor = vec4(adapted, 1.0, 0.0, 1.0);
            }";

    }
}
