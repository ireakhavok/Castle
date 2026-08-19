// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticFreeRayShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticFreeRayShader
    {
        public static readonly string Source = @"
#version 430
layout(local_size_x = 64) in;

struct GpuTriangle { vec4 A; vec4 B; vec4 C; };
struct GpuRayResult { float Intensity; float Delay; float LowPass; float Pad; vec4 ArrivalDir; };
struct GpuDebugSegment { vec4 A; vec4 B; };

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

vec3 randomUnit(float seed) {
    float z = hash(seed) * 2.0 - 1.0;
    float a = hash(seed + 17.0) * 6.2831853;
    float r = sqrt(max(0.0, 1.0 - z * z));
    return vec3(r * cos(a), r * sin(a), z);
}

bool rayTriangle(vec3 o, vec3 d, vec3 a, vec3 b, vec3 c, out float t, out vec3 n) {
    vec3 e1 = b - a, e2 = c - a;
    vec3 p = cross(d, e2);
    float det = dot(e1, p);
    if (abs(det) < 1e-8) return false;
    float inv = 1.0 / det;
    vec3 tv = o - a;
    float u = dot(tv, p) * inv;
    if (u < 0.0 || u > 1.0) return false;
    vec3 q = cross(tv, e1);
    float v = dot(d, q) * inv;
    if (v < 0.0 || u + v > 1.0) return false;
    t = dot(e2, q) * inv;
    if (t < 0.0) return false;
    n = normalize(cross(e1, e2));
    if (dot(n, d) > 0.0) n = -n;
    return true;
}

bool closestHit(vec3 pos, vec3 dir, out float tHit, out vec3 nHit, out float dens) {
    tHit = uMaxDistance; nHit = vec3(0); dens = 1.0; bool hit = false;
    for (int i = 0; i < uTriangleCount; i++) {
        float t; vec3 n;
        if (rayTriangle(pos, dir, triangles[i].A.xyz, triangles[i].B.xyz, triangles[i].C.xyz, t, n)) {
            if (t > 0.001 && t < tHit) {
                tHit = t; nHit = n; dens = max(0.1, triangles[i].C.w); hit = true;
            }
        }
    }
    return hit;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uRayCount)) return;

    uint base = idx * 4u;
    for (uint s = 0u; s < 4u; s++) {
        if (base + s < 1024u) {
            debugSegs[base + s].A = vec4(0.0);
            debugSegs[base + s].B = vec4(0.0, 0.0, 0.0, -1.0);
        }
    }

    bool fromListener = (idx < uint(uRayCount / 2));
    vec3 origin = fromListener ? uListenerPos : uSourcePos;
    int kind = fromListener ? 0 : 1;

    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);

    float tHit;
    vec3 nHit;
    float dens;
    bool hit = closestHit(origin, dir, tHit, nHit, dens);

    const float MAX_FREE = 40.0;
    vec3 freeEnd;
    if (hit && tHit < MAX_FREE)
        freeEnd = origin + dir * tHit;
    else
        freeEnd = origin + dir * MAX_FREE;

    if (freeEnd.z < origin.z - 3.0)
        freeEnd = origin + dir * 6.0;

    // Real free-ray leg (Kind 0 or 1) – this is drawn
    if (base < 1024u) {
        debugSegs[base].A = vec4(origin, 0.0);
        debugSegs[base].B = vec4(freeEnd, float(kind));
    }

    // Storage slot for the meeting pass (Kind = -1 so C# filter discards it)
    if (base + 1u < 1024u) {
        debugSegs[base + 1u].A = vec4(freeEnd, 0.0);
        debugSegs[base + 1u].B = vec4(dir, -1.0);
    }
}
";
    }
}