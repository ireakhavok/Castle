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
    /// <summary>
    /// GPU multi-bounce acoustic ray tracer.
    /// Dispatches a compute shader that traces rays from a source against the
    /// uploaded AcousticGeometry and writes Intensity / Delay / LowPass results.
    /// </summary>
    public unsafe class AcousticRayTracer : IDisposable
    {
        // Result layout written by the compute shader (one slot per ray, then reduced on CPU)
        [StructLayout(LayoutKind.Sequential)]
        public struct GpuRayResult
        {
            public float Intensity;
            public float Delay;
            public float LowPass;
            public float Pad;
        }

        private readonly IRenderContext _renderContext;
        private readonly ComputeProgram _program;
        private readonly ShaderStorageBuffer _resultSsbo;
        private readonly AcousticGeometry _geometry;
        private bool _disposed;

        private const int MaxRays = 64;
        private const int MaxBounces = 4;

        public AcousticRayTracer(IRenderContext renderContext, AcousticGeometry geometry)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));

            string source = BuildComputeShaderSource();
            _program = new ComputeProgram(_renderContext, source);
            _resultSsbo = new ShaderStorageBuffer(_renderContext);

            // Pre-allocate result buffer
            int resultBytes = MaxRays * sizeof(GpuRayResult);
            _resultSsbo.SetData((uint)resultBytes, null, _renderContext.Enums.DynamicCopy);
        }

        /// <summary>
        /// Trace multi-bounce rays from sourcePos toward the listener.
        /// Returns a SoundRayTraceResult that can be fed directly to AudioSystem.
        /// </summary>
        public SoundRayTraceResult Trace(Vector3 sourcePos, Vector3 listenerPos, int rayCount = 32)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AcousticRayTracer));
            if (_geometry.TriangleCount <= 0)
            {
                return new SoundRayTraceResult { Intensity = 1f, Delay = 0f, LowPassCutoff = 0f };
            }

            rayCount = Math.Clamp(rayCount, 1, MaxRays);

            // Bind geometry + result buffers
            _geometry.Buffer.BindBase(0);
            _resultSsbo.BindBase(1);

            _program.Use();
            _program.SetUniform("uSourcePos", sourcePos.X, sourcePos.Y, sourcePos.Z);
            _program.SetUniform("uListenerPos", listenerPos.X, listenerPos.Y, listenerPos.Z);
            _program.SetUniform("uTriangleCount", _geometry.TriangleCount);
            _program.SetUniform("uRayCount", rayCount);
            _program.SetUniform("uMaxBounces", MaxBounces);
            _program.SetUniform("uListenerRadius", 8.0f);
            _program.SetUniform("uMaxDistance", 2000.0f);

            // Dispatch one workgroup per ray (local size = 1 for simplicity in first version)
            uint groups = (uint)((rayCount + 63) / 64);
            _program.Dispatch(groups, 1, 1);
            _program.Barrier();

            // Read results back
            GpuRayResult* results = (GpuRayResult*)_resultSsbo.Map(_renderContext.Enums.MapReadBit);
            if (results == null)
            {
                return new SoundRayTraceResult { Intensity = 0.05f, Delay = 0.05f, LowPassCutoff = 1200f };
            }

            float totalIntensity = 0f;
            float totalDelay = 0f;
            float totalLowPass = 0f;
            int valid = 0;

            for (int i = 0; i < rayCount; i++)
            {
                float inten = results[i].Intensity;
                if (inten > 0.0001f)
                {
                    totalIntensity += inten;
                    totalDelay += results[i].Delay;
                    totalLowPass += results[i].LowPass;
                    valid++;
                }
            }

            _resultSsbo.Unmap();

            if (valid == 0)
            {
                return new SoundRayTraceResult
                {
                    Intensity = 0.03f,
                    Delay = 0.06f,
                    LowPassCutoff = 700f
                };
            }

            float avgIntensity = totalIntensity / valid;
            float avgDelay = totalDelay / valid;
            float avgLowPass = totalLowPass / valid;

            return new SoundRayTraceResult
            {
                Intensity = Math.Clamp(avgIntensity, 0.02f, 1f),
                Delay = avgDelay,
                LowPassCutoff = avgLowPass > 0f ? avgLowPass : 18000f / (1f + avgIntensity * 4f)
            };
        }

        private static string BuildComputeShaderSource()
        {
            var sb = new StringBuilder();
            sb.Append(@"
#version 430
layout(local_size_x = 64) in;

struct GpuTriangle {
    vec4 A;
    vec4 B;
    vec4 C;   // .w = density
};

struct GpuRayResult {
    float Intensity;
    float Delay;
    float LowPass;
    float Pad;
};

layout(std430, binding = 0) readonly buffer TriangleBuffer {
    GpuTriangle triangles[];
};

layout(std430, binding = 1) writeonly buffer ResultBuffer {
    GpuRayResult results[];
};

uniform vec3  uSourcePos;
uniform vec3  uListenerPos;
uniform int   uTriangleCount;
uniform int   uRayCount;
uniform int   uMaxBounces;
uniform float uListenerRadius;
uniform float uMaxDistance;

// Simple hash for deterministic pseudo-random directions
float hash(float n) { return fract(sin(n) * 43758.5453); }
vec3 randomDir(float seed) {
    float z = hash(seed) * 2.0 - 1.0;
    float a = hash(seed + 1.0) * 6.2831853;
    float r = sqrt(max(0.0, 1.0 - z*z));
    return vec3(r * cos(a), r * sin(a), z);
}

bool rayTriangle(vec3 origin, vec3 dir, vec3 a, vec3 b, vec3 c, out float t, out vec3 normal) {
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

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uRayCount)) return;

    vec3 dir = randomDir(float(idx) * 12.9898);
    vec3 pos = uSourcePos;
    float intensity = 1.0;
    float distance = 0.0;
    float lowPass = 20000.0;
    bool reached = false;

    for (int bounce = 0; bounce < uMaxBounces; bounce++) {
        float closestT = uMaxDistance;
        vec3 hitNormal = vec3(0.0);
        float hitDensity = 1.0;
        bool hit = false;

        for (int i = 0; i < uTriangleCount; i++) {
            float t;
            vec3 n;
            if (rayTriangle(pos, dir, triangles[i].A.xyz, triangles[i].B.xyz, triangles[i].C.xyz, t, n)) {
                if (t > 0.001 && t < closestT) {
                    closestT = t;
                    hitNormal = n;
                    hitDensity = max(0.1, triangles[i].C.w);
                    hit = true;
                }
            }
        }

        if (!hit) {
            // Open path – check if we are close enough to the listener
            float remaining = length(uListenerPos - pos);
            if (remaining < uListenerRadius * 2.0) {
                intensity *= 0.2;
                distance += remaining;
                reached = true;
            }
            break;
        }

        distance += closestT;
        if (distance > uMaxDistance) break;

        // Geometric spreading + material absorption
        intensity *= 1.0 / (1.0 + distance * distance * 0.0001);
        intensity *= pow(10.0, -1.5 * hitDensity * closestT / 10.0);
        intensity *= hitDensity > 1.5 ? 0.85 : 0.65;
        lowPass = min(lowPass, 18000.0 / (1.0 + hitDensity * 3.0));

        vec3 hitPoint = pos + dir * closestT;

        if (length(hitPoint - uListenerPos) < uListenerRadius) {
            reached = true;
            break;
        }

        // Reflect
        dir = reflect(dir, hitNormal);
        pos = hitPoint + dir * 0.02;
    }

    if (reached) {
        results[idx].Intensity = intensity;
        results[idx].Delay = distance / 34300.0;
        results[idx].LowPass = lowPass;
    } else {
        results[idx].Intensity = 0.0;
        results[idx].Delay = 0.0;
        results[idx].LowPass = 0.0;
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
                _resultSsbo?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}