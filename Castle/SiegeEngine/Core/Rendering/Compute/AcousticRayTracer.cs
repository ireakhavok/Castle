using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.Rendering.ContextManagement;

namespace SiegeEngine.Core.Rendering.Compute
{
    public unsafe class AcousticRayTracer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct GpuRayResult
        {
            public float Intensity;
            public float Delay;
            public float LowPass;
            public float Pad;
            public Vector4 ArrivalDir;
        }

        private readonly IRenderContext _renderContext;
        private readonly ComputeProgram _program;
        private readonly ShaderStorageBuffer[] _resultSsbo = new ShaderStorageBuffer[2];
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private int _writeIdx;
        private int _readIdx = 1;

        private const int MaxRays = 64;
        private const int ContinuousRays = 64;
        private const int ContinuousBounces = 4;

        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _program = new ComputeProgram(_renderContext, BuildComputeShaderSource());
            int resultBytes = MaxRays * sizeof(GpuRayResult);
            for (int i = 0; i < 2; i++)
            {
                _resultSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _resultSsbo[i].SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
            }
        }

        public void KickContinuousTrace(Vector3 sourcePos, Vector3 listenerPos)
        {
            if (_disposed || _geometry.TriangleCount <= 0) return;
            _geometry.Buffer.BindBase(0);
            _resultSsbo[_writeIdx].BindBase(1);
            _program.Use();
            _program.SetUniform("uSourcePos", sourcePos.X, sourcePos.Y, sourcePos.Z);
            _program.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _program.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _program.SetUniform("uRayCount", ContinuousRays);
            _program.SetUniform("uMaxBounces", ContinuousBounces);
            _program.SetUniform("uListenerRadius", 8.0f);
            _program.SetUniform("uMaxDistance", 500.0f);
            _program.SetUniform("uConeHalfAngle", 0.3927f);
            uint groups = (uint)((ContinuousRays + 63) / 64);
            _program.Dispatch(groups, 1, 1);
            _program.Barrier();
            _readIdx = _writeIdx;
            _writeIdx = 1 - _writeIdx;
        }

        public SoundRayTraceResult ReadCompletedResult()
        {
            if (_disposed || _geometry.TriangleCount <= 0)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0f,
                    LowPassCutoff = 0f,
                    ApparentDirection = Vector3.Zero
                };
            }

            GpuRayResult* results = (GpuRayResult*)_resultSsbo[_readIdx].Map(_renderContext.Enums.MapReadBit);
            if (results == null)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0.02f,
                    LowPassCutoff = 800f,
                    ApparentDirection = Vector3.Zero
                };
            }

            float bestIntensity = 0f;
            float bestDelay = 0f;
            float bestLowPass = 0f;
            Vector3 bestArrival = Vector3.Zero;
            int valid = 0;

            for (int i = 0; i < ContinuousRays; i++)
            {
                float inten = results[i].Intensity;
                if (inten > 0.0001f)
                {
                    valid++;
                    if (inten > bestIntensity)
                    {
                        bestIntensity = inten;
                        bestDelay = results[i].Delay;
                        bestLowPass = results[i].LowPass;
                        bestArrival = new Vector3(
                            results[i].ArrivalDir.X,
                            results[i].ArrivalDir.Y,
                            results[i].ArrivalDir.Z);
                    }
                }
            }

            _resultSsbo[_readIdx].Unmap();

            if (valid == 0)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0.04f,
                    LowPassCutoff = 800f,
                    ApparentDirection = Vector3.Zero
                };
            }

            if (bestArrival.LengthSquared() > 0.0001f)
                bestArrival = Vector3.Normalize(bestArrival);

            return new SoundRayTraceResult
            {
                Intensity = Math.Clamp(bestIntensity, 0.001f, 1f),
                Delay = bestDelay,
                LowPassCutoff = bestLowPass > 0f ? bestLowPass : 12000f / (1f + bestIntensity * 2f),
                ApparentDirection = bestArrival
            };
        }

        private static string BuildComputeShaderSource()
        {
            var sb = new StringBuilder();
            sb.Append(@"
#version 430
layout(local_size_x = 64) in;

struct GpuTriangle
{
    vec4 A;
    vec4 B;
    vec4 C;
};

struct GpuRayResult
{
    float Intensity;
    float Delay;
    float LowPass;
    float Pad;
    vec4 ArrivalDir;
};

layout(std430, binding = 0) readonly buffer TriangleBuffer { GpuTriangle triangles[]; };
layout(std430, binding = 1) writeonly buffer ResultBuffer { GpuRayResult results[]; };

uniform vec3 uSourcePos;
uniform vec3 uListenerPos;
uniform int uTriangleCount;
uniform int uRayCount;
uniform int uMaxBounces;
uniform float uListenerRadius;
uniform float uMaxDistance;
uniform float uConeHalfAngle;

float hash(float n) { return fract(sin(n) * 43758.5453); }

vec3 randomUnit(float seed)
{
    float z = hash(seed) * 2.0 - 1.0;
    float a = hash(seed + 17.0) * 6.2831853;
    float r = sqrt(max(0.0, 1.0 - z * z));
    return vec3(r * cos(a), r * sin(a), z);
}

vec3 lowerHemisphere(float seed)
{
    float z = hash(seed) * -1.0;
    float a = hash(seed + 17.0) * 6.2831853;
    float r = sqrt(max(0.0, 1.0 - z * z));
    return vec3(r * cos(a), r * sin(a), z);
}

vec3 coneDir(vec3 axis, float seed)
{
    vec3 a = normalize(axis);
    vec3 helper = abs(a.y) < 0.99 ? vec3(0,1,0) : vec3(1,0,0);
    vec3 tangent = normalize(cross(a, helper));
    vec3 bitangent = cross(a, tangent);
    float u = hash(seed);
    float v = hash(seed + 31.0);
    float theta = u * uConeHalfAngle;
    float phi = v * 6.2831853;
    float st = sin(theta);
    return normalize(a * cos(theta) + (tangent * cos(phi) + bitangent * sin(phi)) * st);
}

bool rayTriangle(vec3 origin, vec3 dir, vec3 a, vec3 b, vec3 c, out float t, out vec3 normal)
{
    vec3 e1 = b - a;
    vec3 e2 = c - a;
    vec3 p = cross(dir, e2);
    float det = dot(e1, p);
    if (abs(det) < 1e-8) return false;
    float invDet = 1.0 / det;
    vec3 tvec = origin - a;
    float u = dot(tvec, p) * invDet;
    if (u < 0.0 || u > 1.0) return false;
    vec3 q = cross(tvec, e1);
    float v = dot(dir, q) * invDet;
    if (v < 0.0 || u + v > 1.0) return false;
    t = dot(e2, q) * invDet;
    if (t < 0.0) return false;
    normal = normalize(cross(e1, e2));
    if (dot(normal, dir) > 0.0) normal = -normal;
    return true;
}

bool closestHit(vec3 pos, vec3 dir, out float tHit, out vec3 nHit, out float dens)
{
    tHit = uMaxDistance;
    nHit = vec3(0);
    dens = 1.0;
    bool hit = false;
    for (int i = 0; i < uTriangleCount; i++)
    {
        float t;
        vec3 n;
        if (rayTriangle(pos, dir, triangles[i].A.xyz, triangles[i].B.xyz, triangles[i].C.xyz, t, n))
        {
            if (t > 0.001 && t < tHit)
            {
                tHit = t;
                nHit = n;
                dens = max(0.1, triangles[i].C.w);
                hit = true;
            }
        }
    }
    return hit;
}

void main()
{
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uRayCount)) return;

    vec3 toListener = uListenerPos - uSourcePos;
    float distSL = length(toListener);
    vec3 primaryDir;

    if (idx < 22u)
    {
        if (distSL > 0.01)
            primaryDir = coneDir(normalize(toListener), float(idx) * 12.9898);
        else
            primaryDir = randomUnit(float(idx) * 12.9898);
    }
    else if (idx < 43u)
    {
        primaryDir = lowerHemisphere(float(idx) * 12.9898);
    }
    else
    {
        primaryDir = randomUnit(float(idx) * 12.9898);
    }

    vec3 pos = uSourcePos;
    vec3 dir = primaryDir;
    float intensity = 1.0;
    float distanceTravelled = 0.0;
    float lowPass = 20000.0;
    bool reached = false;
    vec3 arrivalDir = vec3(0.0);

    for (int bounce = 0; bounce < uMaxBounces; bounce++)
    {
        vec3 toL = uListenerPos - pos;
        float dL = length(toL);

        if (dL < uListenerRadius)
        {
            distanceTravelled += dL;
            arrivalDir = normalize(pos - uListenerPos);
            reached = true;
            break;
        }

        if (dL > 0.01)
        {
            vec3 losDir = toL / dL;
            float tLos;
            vec3 nLos;
            float densLos;
            bool blocked = closestHit(pos, losDir, tLos, nLos, densLos);
            if (!blocked || tLos >= dL - 0.1)
            {
                distanceTravelled += dL;
                arrivalDir = normalize(pos - uListenerPos);
                reached = true;
                break;
            }
        }

        float tHit;
        vec3 nHit;
        float dens;
        if (!closestHit(pos, dir, tHit, nHit, dens))
        {
            vec3 toL2 = uListenerPos - pos;
            float dL2 = length(toL2);
            if (dL2 < uListenerRadius * 2.5)
            {
                distanceTravelled += dL2;
                arrivalDir = normalize(pos - uListenerPos);
                reached = true;
            }
            break;
        }

        distanceTravelled += tHit;
        if (distanceTravelled > uMaxDistance) break;

        float R = clamp(exp(-0.12 * dens), 0.35, 0.92);
        intensity *= R;

        lowPass = min(lowPass, 18000.0 / (1.0 + dens * 1.2 + distanceTravelled * 0.008));

        vec3 hitPoint = pos + dir * tHit;
        vec3 reflected = reflect(dir, nHit);
        dir = coneDir(reflected, float(idx) * 7.13 + float(bounce) * 3.71);
        pos = hitPoint + dir * 0.02;
    }

    if (reached)
    {
        float D = max(distanceTravelled, 0.5);

        // Relative path-length penalty in amplitude form (1/r law)
        // so that intensity follows 1/r² relative to free field
        float pathFactor = 1.0;
        if (distSL > 0.01)
            pathFactor = distSL / D;
        intensity *= pathFactor;

        // Mild air absorption (relative)
        intensity *= exp(-0.002 * D);

        intensity = clamp(intensity, 0.0, 1.0);

        results[idx].Intensity = intensity;
        results[idx].Delay = distanceTravelled / 34300.0;
        results[idx].LowPass = lowPass;
        results[idx].ArrivalDir = vec4(arrivalDir, 0.0);
    }
    else
    {
        results[idx].Intensity = 0.0;
        results[idx].Delay = 0.0;
        results[idx].LowPass = 0.0;
        results[idx].ArrivalDir = vec4(0.0);
    }
    results[idx].Pad = 0.0;
}
");
            return sb.ToString();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _program?.Dispose();
                for (int i = 0; i < 2; i++)
                    _resultSsbo[i]?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}