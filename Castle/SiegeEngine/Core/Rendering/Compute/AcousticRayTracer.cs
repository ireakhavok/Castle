// Folder: SiegeEngine/Core/Rendering/Compute
// File: AcousticRayTracer.cs
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

        private const int MaxRays = 128;
        private const int ContinuousRays = 128;
        private const int ContinuousBounces = 6;

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
            _program.SetUniform("uListenerRadius", 5.0f);
            _program.SetUniform("uMaxDistance", 350.0f);

            uint groups = (uint)((ContinuousRays + 63) / 64);
            _program.Dispatch(groups, 1, 1);

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

            _program.Barrier();

            uint byteSize = (uint)(ContinuousRays * sizeof(GpuRayResult));
            GpuRayResult* results = (GpuRayResult*)_resultSsbo[_readIdx].MapRange(0, byteSize, _renderContext.Enums.MapReadBit);

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

            // Free-energy aggregation: energy-weighted average across ALL successful rays.
            // This is what produces correct wrap-around direction and intensity.
            float totalEnergy = 0f;
            Vector3 weightedDir = Vector3.Zero;
            float bestDelay = 0f;
            float bestLowPass = 0f;
            float maxSingle = 0f;
            int valid = 0;

            for (int i = 0; i < ContinuousRays; i++)
            {
                float inten = results[i].Intensity;
                if (inten > 0.00015f)
                {
                    valid++;
                    float energy = inten * inten; // energy weight
                    totalEnergy += energy;

                    Vector3 dir = new Vector3(
                        results[i].ArrivalDir.X,
                        results[i].ArrivalDir.Y,
                        results[i].ArrivalDir.Z);

                    if (dir.LengthSquared() > 1e-8f)
                        weightedDir += dir * energy;

                    if (inten > maxSingle)
                    {
                        maxSingle = inten;
                        bestDelay = results[i].Delay;
                        bestLowPass = results[i].LowPass;
                    }
                }
            }

            _resultSsbo[_readIdx].Unmap();

            if (valid == 0 || totalEnergy < 1e-8f)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0.04f,
                    LowPassCutoff = 800f,
                    ApparentDirection = Vector3.Zero
                };
            }

            Vector3 arrival = Vector3.Normalize(weightedDir);
            // Soft residual intensity (never stronger than a mild Primary occlusion)
            float intensity = Math.Clamp(MathF.Sqrt(totalEnergy / valid) * 1.8f, 0.001f, 0.82f);

            return new SoundRayTraceResult
            {
                Intensity = intensity,
                Delay = bestDelay,
                LowPassCutoff = bestLowPass > 0f ? bestLowPass : 2800f + 3200f * intensity,
                ApparentDirection = arrival
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
    vec4 C; // .w = material density
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

float hash(float n) { return fract(sin(n) * 43758.5453); }

vec3 randomUnit(float seed)
{
    float z = hash(seed) * 2.0 - 1.0;
    float a = hash(seed + 17.0) * 6.2831853;
    float r = sqrt(max(0.0, 1.0 - z * z));
    return vec3(r * cos(a), r * sin(a), z);
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

    // Listener-centric rays
    vec3 pos = uListenerPos;
    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);

    // Four specialized free-energy accumulators
    float green  = 0.0; // direct
    float blue   = 0.0; // reflection
    float orange = 0.0; // transmission (heavily penalised)
    float yellow = 0.0; // leakage / diffraction around openings

    float distanceTravelled = 0.0;
    float lowPass = 18000.0;
    bool reached = false;
    vec3 arrivalDir = vec3(0.0);
    float pathWeight = 1.0;

    for (int bounce = 0; bounce < uMaxBounces; bounce++)
    {
        vec3 toSource = uSourcePos - pos;
        float dS = length(toSource);

        // Direct reach (Green) – strongest when clear
        if (dS < uListenerRadius)
        {
            distanceTravelled += dS;
            arrivalDir = normalize(uListenerPos - pos); // direction sound arrives FROM
            float r2 = max(distanceTravelled * distanceTravelled, 0.2);
            green += pathWeight / r2;
            reached = true;
            break;
        }

        // Clear LOS check
        if (dS > 0.01)
        {
            vec3 losDir = toSource / dS;
            float tLos;
            vec3 nLos;
            float densLos;
            bool blocked = closestHit(pos, losDir, tLos, nLos, densLos);
            if (!blocked || tLos >= dS - 0.12)
            {
                distanceTravelled += dS;
                arrivalDir = normalize(uListenerPos - pos);
                float r2 = max(distanceTravelled * distanceTravelled, 0.2);
                green += pathWeight / r2;
                reached = true;
                break;
            }
        }

        float tHit;
        vec3 nHit;
        float dens;
        if (!closestHit(pos, dir, tHit, nHit, dens))
        {
            // Open space / pure leakage (Yellow) – preferred for wrap-around
            vec3 toS = uSourcePos - pos;
            float d = length(toS);
            if (d < uMaxDistance * 0.95)
            {
                distanceTravelled += d;
                arrivalDir = normalize(uListenerPos - pos);
                float r2 = max(distanceTravelled * distanceTravelled, 0.4);
                yellow += pathWeight * 0.9 / r2;
                reached = true;
            }
            break;
        }

        distanceTravelled += tHit;
        if (distanceTravelled > uMaxDistance) break;

        // Material response – transmission is deliberately weak so wrap-around wins
        float R = clamp(exp(-0.18 * dens), 0.30, 0.88);   // reflection
        float T = clamp(0.12 / (dens * dens), 0.008, 0.18); // transmission heavily penalised

        float r2 = max(distanceTravelled * distanceTravelled, 0.5);

        // Reflection (Blue)
        blue += pathWeight * R * 0.75 / r2;

        // Transmission (Orange) – kept weak so it never dominates wrap-around
        orange += pathWeight * T * 0.35 / r2;

        // Edge leakage bias (Yellow) – increases when we graze the surface
        float graze = 1.0 - abs(dot(dir, nHit));
        yellow += pathWeight * graze * 0.55 / r2;

        lowPass = min(lowPass, 14000.0 / (1.0 + dens * 1.6 + distanceTravelled * 0.015));

        // Continue – bias toward diffraction around the surface rather than pure reflection
        vec3 hitPoint = pos + dir * tHit;
        vec3 reflected = reflect(dir, nHit);
        float jitter = (hash(float(idx) * 4.7 + float(bounce) * 9.1) - 0.5) * 0.55;
        // push slightly into the tangent plane to encourage wrap-around
        vec3 tangent = normalize(cross(nHit, reflected + vec3(0.01)));
        dir = normalize(reflected + nHit * 0.05 + tangent * jitter);
        pos = hitPoint + dir * 0.04;
        pathWeight *= 0.92; // gentle energy loss per bounce
    }

    // Free-energy sum – transmission is the weakest contributor
    float totalEnergy = green * 1.4 + blue * 0.95 + yellow * 1.15 + orange * 0.25;

    if (reached && totalEnergy > 0.0002)
    {
        float intensity = clamp(totalEnergy * 3.8, 0.0, 1.0);

        // Prefer paths that are not much longer than the direct distance (wrap-around still wins over through-wall)
        float directDist = length(uSourcePos - uListenerPos);
        if (distanceTravelled > directDist * 1.8)
            intensity *= 0.75;
        else if (distanceTravelled < directDist * 1.35)
            intensity = min(1.0, intensity * 1.1);

        results[idx].Intensity = intensity;
        results[idx].Delay = distanceTravelled / 34300.0;
        results[idx].LowPass = lowPass;
        results[idx].ArrivalDir = vec4(normalize(arrivalDir), 0.0);
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