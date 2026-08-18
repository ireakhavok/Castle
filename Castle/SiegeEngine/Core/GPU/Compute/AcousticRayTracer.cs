// Folder: SiegeEngine/Core/Rendering/Compute
// File: AcousticRayTracer.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using SiegeEngine.Core.Events;
using SiegeEngine.Core.GPU.ContextManagement;

namespace SiegeEngine.Core.GPU.Compute
{
    public unsafe class AcousticRayTracer : IDisposable
    {
        public enum DebugSegmentKind : int { Listener = 0, Source = 1, Meeting = 2 }

        [StructLayout(LayoutKind.Sequential)]
        public struct GpuRayResult
        {
            public float Intensity;
            public float Delay;
            public float LowPass;
            public float Pad;
            public Vector4 ArrivalDir;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GpuDebugSegment
        {
            public Vector4 A;   // xyz = origin
            public Vector4 B;   // xyz = end, w = kind (0=listener,1=source,2=meeting)
        }

        public struct DebugSegment
        {
            public Vector3 A;
            public Vector3 B;
            public DebugSegmentKind Kind;
        }

        private readonly IRenderContext _renderContext;
        private readonly ComputeProgram _program;
        private readonly ShaderStorageBuffer[] _resultSsbo = new ShaderStorageBuffer[2];
        private readonly ShaderStorageBuffer[] _debugSsbo = new ShaderStorageBuffer[2];
        private readonly AcousticGeometry _geometry;
        private bool _disposed;
        private int _writeIdx;
        private int _readIdx = 1;
        private const int MaxRays = 128;
        private const int ContinuousRays = 128;
        private const int ContinuousBounces = 6;
        private const int MaxDebugSegments = 512;
        private readonly List<DebugSegment> _debugSegments = new List<DebugSegment>(MaxDebugSegments);

        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _program = new ComputeProgram(_renderContext, BuildComputeShaderSource());
            int resultBytes = MaxRays * sizeof(GpuRayResult);
            int debugBytes = MaxDebugSegments * sizeof(GpuDebugSegment);
            for (int i = 0; i < 2; i++)
            {
                _resultSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _resultSsbo[i].SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
                _debugSsbo[i] = new ShaderStorageBuffer(_renderContext);
                _debugSsbo[i].SetData((uint)debugBytes, null, _renderContext.Enums.DynamicCopy);
            }
        }

        public void KickContinuousTrace(Vector3 sourcePos, Vector3 listenerPos)
        {
            if (_disposed || _geometry.TriangleCount <= 0) return;
            _geometry.Buffer.BindBase(0);
            _resultSsbo[_writeIdx].BindBase(1);
            _debugSsbo[_writeIdx].BindBase(2);
            _program.Use();
            _program.SetUniform("uSourcePos", sourcePos.X, sourcePos.Y, sourcePos.Z);
            _program.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _program.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _program.SetUniform("uRayCount", ContinuousRays);
            _program.SetUniform("uMaxBounces", ContinuousBounces);
            _program.SetUniform("uListenerRadius", 5.0f);
            _program.SetUniform("uMaxDistance", 350.0f);
            _program.SetUniform("uDebugMode", 0);
            _program.SetUniform("uSourceCount", 1);
            uint groups = (uint)((ContinuousRays + 63) / 64);
            _program.Dispatch(groups, 1, 1);
            _readIdx = _writeIdx;
            _writeIdx = 1 - _writeIdx;
        }

        public void KickDebugBidirectional(Vector3 listenerPos, IReadOnlyList<Vector3> sources)
        {
            if (_disposed || _geometry.TriangleCount <= 0) return;
            Vector3 primarySource = (sources != null && sources.Count > 0) ? sources[0] : listenerPos;
            _geometry.Buffer.BindBase(0);
            _resultSsbo[_writeIdx].BindBase(1);
            _debugSsbo[_writeIdx].BindBase(2);
            _program.Use();
            _program.SetUniform("uSourcePos", primarySource.X, primarySource.Y, primarySource.Z);
            _program.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _program.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _program.SetUniform("uRayCount", ContinuousRays);
            _program.SetUniform("uMaxBounces", ContinuousBounces);
            _program.SetUniform("uListenerRadius", 5.0f);
            _program.SetUniform("uMaxDistance", 350.0f);
            _program.SetUniform("uDebugMode", 1);
            _program.SetUniform("uSourceCount", sources != null ? Math.Min(sources.Count, 8) : 0);
            uint groups = (uint)((ContinuousRays + 63) / 64);
            _program.Dispatch(groups, 1, 1);
            _program.Barrier();
            ReadDebugSegments();
            _readIdx = _writeIdx;
            _writeIdx = 1 - _writeIdx;
        }

        public IReadOnlyList<DebugSegment> GetDebugSegments() => _debugSegments;

        private void ReadDebugSegments()
        {
            _debugSegments.Clear();
            uint byteSize = (uint)(MaxDebugSegments * sizeof(GpuDebugSegment));
            GpuDebugSegment* segs = (GpuDebugSegment*)_debugSsbo[_writeIdx].MapRange(0, byteSize, _renderContext.Enums.MapReadBit);
            if (segs == null) return;
            for (int i = 0; i < MaxDebugSegments; i++)
            {
                float kindF = segs[i].B.W;
                if (kindF < 0f) break;
                int kind = (int)kindF;
                if (kind < 0 || kind > 2) continue;
                Vector3 a = new Vector3(segs[i].A.X, segs[i].A.Y, segs[i].A.Z);
                Vector3 b = new Vector3(segs[i].B.X, segs[i].B.Y, segs[i].B.Z);
                if ((a - b).LengthSquared() < 1e-6f) continue;
                _debugSegments.Add(new DebugSegment
                {
                    A = a,
                    B = b,
                    Kind = (DebugSegmentKind)kind
                });
            }
            _debugSsbo[_writeIdx].Unmap();
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

            float totalEnergy = 0f;
            Vector3 weightedDir = Vector3.Zero;
            float bestDelay = 0f;
            float bestLowPass = 0f;
            float maxSingle = 0f;
            int valid = 0;
            int txCount = 0, diffrCount = 0, connCount = 0;

            for (int i = 0; i < ContinuousRays; i++)
            {
                float inten = results[i].Intensity;
                if (inten > 0.00015f)
                {
                    valid++;
                    float energy = inten * inten;
                    totalEnergy += energy;
                    Vector3 dir = new Vector3(results[i].ArrivalDir.X, results[i].ArrivalDir.Y, results[i].ArrivalDir.Z);
                    if (dir.LengthSquared() > 1e-8f)
                        weightedDir += dir * energy;
                    if (inten > maxSingle)
                    {
                        maxSingle = inten;
                        bestDelay = results[i].Delay;
                        bestLowPass = results[i].LowPass;
                    }
                    if (results[i].Pad > 1.5f) connCount++;
                    else if (results[i].Pad > 0.5f) diffrCount++;
                    else txCount++;
                }
            }
            _resultSsbo[_readIdx].Unmap();

            if (valid == 0 || totalEnergy < 1e-8f)
            {
                Console.WriteLine($"AcousticRayTracer residual: Primary=blocked residual=0.001 dir=(0,0,0) pathways tx=0 diffr=0 conn=0");
                return new SoundRayTraceResult
                {
                    Intensity = 0.001f,
                    Delay = 0.04f,
                    LowPassCutoff = 800f,
                    ApparentDirection = Vector3.Zero
                };
            }

            Vector3 arrival = Vector3.Normalize(weightedDir);
            float intensity = Math.Clamp(MathF.Sqrt(totalEnergy / valid) * 1.8f, 0.001f, 0.82f);
            Console.WriteLine($"AcousticRayTracer residual: Primary=blocked residual={intensity:F3} dir=({arrival.X:F2},{arrival.Y:F2},{arrival.Z:F2}) pathways tx={txCount} diffr={diffrCount} conn={connCount}");
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

struct GpuDebugSegment
{
    vec4 A;
    vec4 B;
};

layout(std430, binding = 0) readonly buffer TriangleBuffer { GpuTriangle triangles[]; };
layout(std430, binding = 1) writeonly buffer ResultBuffer { GpuRayResult results[]; };
layout(std430, binding = 2) writeonly buffer DebugBuffer { GpuDebugSegment debugSegs[]; };

uniform vec3 uSourcePos;
uniform vec3 uListenerPos;
uniform int uTriangleCount;
uniform int uRayCount;
uniform int uMaxBounces;
uniform float uListenerRadius;
uniform float uMaxDistance;
uniform int uDebugMode;
uniform int uSourceCount;

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

    bool fromListener = (idx % 2u) == 0u;
    vec3 origin = fromListener ? uListenerPos : uSourcePos;
    vec3 target = fromListener ? uSourcePos : uListenerPos;
    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);

    float green = 0.0;
    float blue = 0.0;
    float orange = 0.0;
    float yellow = 0.0;
    float distanceTravelled = 0.0;
    float lowPass = 18000.0;
    bool reached = false;
    vec3 arrivalDir = vec3(0.0);
    float pathWeight = 1.0;
    int pathwayType = 0;
    vec3 freeEnd = origin;
    vec3 lastHit = origin;
    bool wroteDebug = false;

    for (int bounce = 0; bounce < uMaxBounces; bounce++)
    {
        vec3 toTarget = target - origin;
        float dT = length(toTarget);

        if (dT < uListenerRadius)
        {
            distanceTravelled += dT;
            arrivalDir = normalize(uListenerPos - origin);
            float r2 = max(distanceTravelled * distanceTravelled, 0.2);
            green += pathWeight / r2;
            freeEnd = origin + normalize(toTarget) * dT;
            reached = true;
            pathwayType = 2;
            break;
        }

        if (dT > 0.01)
        {
            vec3 losDir = toTarget / dT;
            float tLos;
            vec3 nLos;
            float densLos;
            bool blocked = closestHit(origin, losDir, tLos, nLos, densLos);
            if (!blocked || tLos >= dT - 0.12)
            {
                distanceTravelled += dT;
                arrivalDir = normalize(uListenerPos - origin);
                float r2 = max(distanceTravelled * distanceTravelled, 0.2);
                green += pathWeight / r2;
                freeEnd = origin + losDir * dT;
                reached = true;
                pathwayType = 2;
                break;
            }
        }

        float tHit;
        vec3 nHit;
        float dens;
        if (!closestHit(origin, dir, tHit, nHit, dens))
        {
            vec3 toS = target - origin;
            float d = length(toS);
            if (d < uMaxDistance * 0.95)
            {
                distanceTravelled += d;
                arrivalDir = normalize(uListenerPos - origin);
                float r2 = max(distanceTravelled * distanceTravelled, 0.4);
                yellow += pathWeight * 0.9 / r2;
                freeEnd = origin + normalize(toS) * d;
                reached = true;
                pathwayType = 1;
            }
            else
            {
                freeEnd = origin + dir * min(uMaxDistance * 0.5, 80.0);
            }
            break;
        }

        distanceTravelled += tHit;
        if (distanceTravelled > uMaxDistance) break;

        float R = clamp(exp(-0.18 * dens), 0.30, 0.88);
        float T = clamp(0.12 / (dens * dens), 0.008, 0.18);
        float r2 = max(distanceTravelled * distanceTravelled, 0.5);
        blue += pathWeight * R * 0.75 / r2;
        orange += pathWeight * T * 0.35 / r2;
        float graze = 1.0 - abs(dot(dir, nHit));
        yellow += pathWeight * graze * 0.55 / r2;
        lowPass = min(lowPass, 14000.0 / (1.0 + dens * 1.6 + distanceTravelled * 0.015));

        lastHit = origin + dir * tHit;
        freeEnd = lastHit;

        vec3 reflected = reflect(dir, nHit);
        float jitter = (hash(float(idx) * 4.7 + float(bounce) * 9.1) - 0.5) * 0.55;
        vec3 tangent = normalize(cross(nHit, reflected + vec3(0.01)));
        dir = normalize(reflected + nHit * 0.05 + tangent * jitter);
        origin = lastHit + dir * 0.04;
        pathWeight *= 0.92;
        pathwayType = 0;
    }

    float totalEnergy = green * 1.4 + blue * 0.95 + yellow * 1.15 + orange * 0.25;
    if (reached && totalEnergy > 0.0002)
    {
        float intensity = clamp(totalEnergy * 3.8, 0.0, 1.0);
        float directDist = length(uSourcePos - uListenerPos);
        if (distanceTravelled > directDist * 1.8)
            intensity *= 0.75;
        else if (distanceTravelled < directDist * 1.35)
            intensity = min(1.0, intensity * 1.1);
        results[idx].Intensity = intensity;
        results[idx].Delay = distanceTravelled / 34300.0;
        results[idx].LowPass = lowPass;
        results[idx].ArrivalDir = vec4(normalize(arrivalDir), 0.0);
        results[idx].Pad = float(pathwayType);
    }
    else
    {
        results[idx].Intensity = 0.0;
        results[idx].Delay = 0.0;
        results[idx].LowPass = 0.0;
        results[idx].ArrivalDir = vec4(0.0);
        results[idx].Pad = 0.0;
    }

    if (uDebugMode != 0 && idx < uint(512))
    {
        vec3 start = fromListener ? uListenerPos : uSourcePos;
        debugSegs[idx].A = vec4(start, 0.0);
        debugSegs[idx].B = vec4(freeEnd, fromListener ? 0.0 : 1.0);

        if (reached && pathwayType == 2 && idx + 256u < 512u)
        {
            debugSegs[idx + 256u].A = vec4(freeEnd - vec3(0.15), 0.0);
            debugSegs[idx + 256u].B = vec4(freeEnd + vec3(0.15), 2.0);
        }
    }
    else if (uDebugMode != 0 && idx == 0u)
    {
        debugSegs[0].B.w = -1.0;
    }
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
                {
                    _resultSsbo[i]?.Dispose();
                    _debugSsbo[i]?.Dispose();
                }
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}