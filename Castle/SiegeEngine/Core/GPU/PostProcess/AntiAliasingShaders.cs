// Folder: SiegeEngine/Core/GPU/PostProcess
// File: AntiAliasingShaders.cs
namespace SiegeEngine.Core.GPU.PostProcess
{
    public static class AntiAliasingShaders
    {
        public const string FullscreenVertex = @"            #version 330 core
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

            out vec2 vUv;
            void main()
            {
                float x = float((gl_VertexID & 1) << 2) - 1.0;
                float y = float((gl_VertexID & 2) << 1) - 1.0;
                vUv = vec2((x + 1.0) * 0.5, (y + 1.0) * 0.5);
                gl_Position = vec4(x, y, 0.0, 1.0);
            }";

        public const string CopyFragment = @"            #version 330 core
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
                FragColor = texture(Color, vUv);
            }";

        public const string FxaaFragment = @"            #version 330 core
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
                return dot(c, vec3(0.299, 0.587, 0.114));
            }

            void main()
            {
                vec2 rcp = InvResolution.xy;
                vec3 rgbM = texture(Color, vUv).rgb;
                float lumaM = Luma(rgbM);
                float lumaN = Luma(texture(Color, vUv + vec2(0.0, -rcp.y)).rgb);
                float lumaS = Luma(texture(Color, vUv + vec2(0.0,  rcp.y)).rgb);
                float lumaW = Luma(texture(Color, vUv + vec2(-rcp.x, 0.0)).rgb);
                float lumaE = Luma(texture(Color, vUv + vec2( rcp.x, 0.0)).rgb);

                float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaW, lumaE)));
                float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaW, lumaE)));
                float range = lumaMax - lumaMin;
                if (range < max(0.0833, lumaMax * 0.166))
                {
                    FragColor = vec4(rgbM, 1.0);
                    return;
                }

                float lumaNW = Luma(texture(Color, vUv + vec2(-rcp.x, -rcp.y)).rgb);
                float lumaNE = Luma(texture(Color, vUv + vec2( rcp.x, -rcp.y)).rgb);
                float lumaSW = Luma(texture(Color, vUv + vec2(-rcp.x,  rcp.y)).rgb);
                float lumaSE = Luma(texture(Color, vUv + vec2( rcp.x,  rcp.y)).rgb);

                vec2 dir;
                dir.x = -((lumaN + lumaS) - (lumaE + lumaW));
                dir.y =  ((lumaE + lumaW) - (lumaN + lumaS));
                float dirReduce = max((lumaN + lumaS + lumaW + lumaE) * (0.25 * 0.125), 0.0078125);
                float rcpDir = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
                dir = clamp(dir * rcpDir, vec2(-8.0), vec2(8.0)) * rcp;

                vec3 rgbA = 0.5 * (
                    texture(Color, vUv + dir * (1.0 / 3.0 - 0.5)).rgb +
                    texture(Color, vUv + dir * (2.0 / 3.0 - 0.5)).rgb);
                vec3 rgbB = rgbA * 0.5 + 0.25 * (
                    texture(Color, vUv + dir * -0.5).rgb +
                    texture(Color, vUv + dir *  0.5).rgb);

                float lumaB = Luma(rgbB);
                vec3 filtered = (lumaB < lumaMin || lumaB > lumaMax) ? rgbA : rgbB;
                vec3 outC = mix(rgbM, filtered, 0.40);

                // 1px lattice: both sides agree with each other, disagree with center.
                if (abs(lumaW - lumaE) < 0.06 && min(abs(lumaM - lumaW), abs(lumaM - lumaE)) > 0.22)
                    outC = mix(outC, 0.5 * (texture(Color, vUv + vec2(-rcp.x, 0.0)).rgb + texture(Color, vUv + vec2(rcp.x, 0.0)).rgb), 0.45);
                if (abs(lumaN - lumaS) < 0.06 && min(abs(lumaM - lumaN), abs(lumaM - lumaS)) > 0.22)
                    outC = mix(outC, 0.5 * (texture(Color, vUv + vec2(0.0, -rcp.y)).rgb + texture(Color, vUv + vec2(0.0, rcp.y)).rgb), 0.45);

                FragColor = vec4(outC, 1.0);
            }";

        public const string SmaaEdgeFragment = @"            #version 330 core
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
                vec2 rcp = InvResolution.xy;
                float l = Luma(texture(Color, vUv).rgb);
                float lLeft = Luma(texture(Color, vUv + vec2(-rcp.x, 0.0)).rgb);
                float lTop  = Luma(texture(Color, vUv + vec2(0.0, -rcp.y)).rgb);
                vec2 delta = abs(vec2(l - lLeft, l - lTop));

                // High threshold so faceted / missing-texture models do not crayon.
                float thresh = max(0.18, max(l, max(lLeft, lTop)) * 0.22);
                vec2 edges = step(vec2(thresh), delta);
                if (dot(edges, vec2(1.0)) < 1e-5)
                {
                    FragColor = vec4(0.0);
                    return;
                }
                FragColor = vec4(edges, 0.0, 1.0);
            }";

        public const string SmaaWeightFragment = @"            #version 330 core
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
            uniform sampler2D uEdges;

            float Search(vec2 uv, vec2 dir, float maxSteps)
            {
                vec2 rcp = InvResolution.xy;
                float dist = 0.0;
                float e = 1.0;
                for (int i = 0; i < 8; i++)
                {
                    if (float(i) >= maxSteps) break;
                    dist += 1.0;
                    vec2 sampleUv = uv + dir * dist * rcp;
                    vec2 edge = texture(uEdges, sampleUv).rg;
                    e = (abs(dir.x) > 0.5) ? edge.r : edge.g;
                    if (e < 0.9) break;
                }
                return dist;
            }

            // Short-edge only. Long runs get almost no weight so interiors stay clean.
            vec2 Pack(float a, float b)
            {
                float span = a + b;
                if (span < 1.5 || span > 7.0)
                    return vec2(0.0);
                float w = 0.22 * (1.0 - clamp((span - 2.0) / 6.0, 0.0, 1.0));
                return vec2(w, w);
            }

            void main()
            {
                vec2 e = texture(uEdges, vUv).rg;
                vec4 weights = vec4(0.0);
                if (e.g > 0.0)
                {
                    float left = Search(vUv, vec2(-1.0, 0.0), 8.0);
                    float right = Search(vUv, vec2(1.0, 0.0), 8.0);
                    weights.rg = Pack(left, right);
                }
                if (e.r > 0.0)
                {
                    float up = Search(vUv, vec2(0.0, -1.0), 8.0);
                    float down = Search(vUv, vec2(0.0, 1.0), 8.0);
                    weights.ba = Pack(up, down);
                }
                FragColor = weights;
            }";

        public const string SmaaBlendFragment = @"            #version 330 core
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
            uniform sampler2D uWeights;

            vec3 RefineThinLines(vec3 center, vec2 uv, vec2 rcp)
            {
                vec3 rgbW = texture(Color, uv + vec2(-rcp.x, 0.0)).rgb;
                vec3 rgbE = texture(Color, uv + vec2( rcp.x, 0.0)).rgb;
                vec3 rgbN = texture(Color, uv + vec2(0.0, -rcp.y)).rgb;
                vec3 rgbS = texture(Color, uv + vec2(0.0,  rcp.y)).rgb;
                float lC = dot(center, vec3(0.2126, 0.7152, 0.0722));
                float lW = dot(rgbW, vec3(0.2126, 0.7152, 0.0722));
                float lE = dot(rgbE, vec3(0.2126, 0.7152, 0.0722));
                float lN = dot(rgbN, vec3(0.2126, 0.7152, 0.0722));
                float lS = dot(rgbS, vec3(0.2126, 0.7152, 0.0722));
                vec3 outC = center;
                if (abs(lW - lE) < 0.06 && min(abs(lC - lW), abs(lC - lE)) > 0.22)
                    outC = mix(outC, 0.5 * (rgbW + rgbE), 0.45);
                if (abs(lN - lS) < 0.06 && min(abs(lC - lN), abs(lC - lS)) > 0.22)
                    outC = mix(outC, 0.5 * (rgbN + rgbS), 0.45);
                return outC;
            }

            void main()
            {
                vec3 center = texture(Color, vUv).rgb;
                vec2 rcp = InvResolution.xy;
                vec4 w = texture(uWeights, vUv);
                float left = w.r;
                float right = w.g;
                float top = w.b;
                float bottom = w.a;
                float sum = left + right + top + bottom;
                vec3 outC = center;
                if (sum >= 1e-4)
                {
                    vec3 acc = center;
                    acc += texture(Color, vUv + vec2(-rcp.x, 0.0)).rgb * left;
                    acc += texture(Color, vUv + vec2( rcp.x, 0.0)).rgb * right;
                    acc += texture(Color, vUv + vec2(0.0, -rcp.y)).rgb * top;
                    acc += texture(Color, vUv + vec2(0.0,  rcp.y)).rgb * bottom;
                    outC = mix(center, acc / (1.0 + sum), 0.40);
                }
                outC = RefineThinLines(outC, vUv, rcp);
                FragColor = vec4(outC, 1.0);
            }";

        public const string TaaFragment = @"            #version 330 core
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
            uniform sampler2D uHistory;
            uniform sampler2D uDepth;

            float Luma(vec3 c)
            {
                return dot(c, vec3(0.299, 0.587, 0.114));
            }

            void main()
            {
                vec3 current = texture(Color, vUv).rgb;
                if (HasHistory == 0)
                {
                    FragColor = vec4(current, 1.0);
                    return;
                }

                float depth = texture(uDepth, vUv).r;
                vec4 ndc = vec4(vUv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
                mat4 invViewProj = inverse(Projection * View);
                vec4 world = invViewProj * ndc;
                world /= max(world.w, 1e-6);

                vec4 prevClip = (PrevProjection * PrevView) * world;
                prevClip /= max(prevClip.w, 1e-6);
                vec2 prevUv = prevClip.xy * 0.5 + 0.5;

                bool offscreen = prevUv.x < 0.0 || prevUv.x > 1.0 || prevUv.y < 0.0 || prevUv.y > 1.0;
                vec3 history = current;
                if (!offscreen)
                    history = texture(uHistory, prevUv).rgb;

                vec3 n00 = texture(Color, vUv + vec2(-InvResolution.xy.x, -InvResolution.xy.y)).rgb;
                vec3 n10 = texture(Color, vUv + vec2( 0.0,               -InvResolution.xy.y)).rgb;
                vec3 n20 = texture(Color, vUv + vec2( InvResolution.xy.x, -InvResolution.xy.y)).rgb;
                vec3 n01 = texture(Color, vUv + vec2(-InvResolution.xy.x,  0.0)).rgb;
                vec3 n21 = texture(Color, vUv + vec2( InvResolution.xy.x,  0.0)).rgb;
                vec3 n02 = texture(Color, vUv + vec2(-InvResolution.xy.x,  InvResolution.xy.y)).rgb;
                vec3 n12 = texture(Color, vUv + vec2( 0.0,                InvResolution.xy.y)).rgb;
                vec3 n22 = texture(Color, vUv + vec2( InvResolution.xy.x,  InvResolution.xy.y)).rgb;

                vec3 cmin = min(current, min(min(min(n00, n10), min(n20, n01)), min(min(n21, n02), min(n12, n22))));
                vec3 cmax = max(current, max(max(max(n00, n10), max(n20, n01)), max(max(n21, n02), max(n12, n22))));
                history = clamp(history, cmin, cmax);

                float still = offscreen ? 1.0 : 0.2;
                vec3 outC = mix(history, current, still);
                FragColor = vec4(outC, 1.0);
            }";
    }
}
