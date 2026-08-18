// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticDebugShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticDebugShader
    {
        // Completely self-contained – no concatenation needed
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

    uint base = idx * 8u;
    for (uint s = 0u; s < 8u; s++) {
        if (base + s < 1024u) {
            debugSegs[base + s].A = vec4(0.0);
            debugSegs[base + s].B = vec4(0.0, 0.0, 0.0, -1.0);
        }
    }

    bool fromListener = (idx < uint(uRayCount / 2));
    vec3 origin = fromListener ? uListenerPos : uSourcePos;
    int kind = fromListener ? 0 : 1;
    vec3 opposite = fromListener ? uSourcePos : uListenerPos;

    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);

    float tHit;
    vec3 nHit;
    float dens;
    bool hit = closestHit(origin, dir, tHit, nHit, dens);

    const float MAX_FREE = 32.0;
    vec3 freeEnd;
    if (hit && tHit < MAX_FREE) {
        freeEnd = origin + dir * tHit;
    } else {
        freeEnd = origin + dir * MAX_FREE;
        if (freeEnd.z < origin.z - 2.5) {
            freeEnd = origin + dir * min(MAX_FREE, 8.0);
        }
    }

    uint w = 0u;

    if (base + w < 1024u) {
        debugSegs[base + w].A = vec4(origin, 0.0);
        debugSegs[base + w].B = vec4(freeEnd, float(kind));
        w++;
    }

    vec3 toOpp = opposite - freeEnd;
    float dist = length(toOpp);
    if (dist > 2.0 && dist < 55.0) {
        vec3 connDir = toOpp / dist;
        float cosAng = clamp(dot(normalize(dir), connDir), -1.0, 1.0);
        float ang = acos(cosAng);

        if (ang <= 0.2618) {
            float tC;
            vec3 nC;
            float dC;
            bool blocked = closestHit(freeEnd, connDir, tC, nC, dC);
            float clearRatio = blocked ? (tC / dist) : 1.0;

            if (clearRatio >= 0.94) {
                vec3 meet = freeEnd + connDir * min(dist * 0.45, 10.0);

                if (base + w < 1024u) {
                    debugSegs[base + w].A = vec4(meet - vec3(0.5, 0.0, 0.0), 0.0);
                    debugSegs[base + w].B = vec4(meet + vec3(0.5, 0.0, 0.0), 2.0);
                    w++;
                }
                if (base + w < 1024u) {
                    debugSegs[base + w].A = vec4(meet - vec3(0.0, 0.5, 0.0), 0.0);
                    debugSegs[base + w].B = vec4(meet + vec3(0.0, 0.5, 0.0), 2.0);
                    w++;
                }
                if (base + w < 1024u) {
                    debugSegs[base + w].A = vec4(freeEnd, 0.0);
                    debugSegs[base + w].B = vec4(meet, 2.0);
                    w++;
                }
            }
        }
    }

    if (hit && w + 1u < 8u) {
        vec3 refl = reflect(dir, nHit);
        float jitter = (hash(float(idx) * 4.1) - 0.5) * 0.25;
        vec3 tangent = normalize(cross(nHit, refl + vec3(0.01, 0.0, 0.0)));
        vec3 bounceDir = normalize(refl + nHit * 0.05 + tangent * jitter);

        float bt;
        vec3 bn;
        float bd;
        bool bhit = closestHit(freeEnd + bounceDir * 0.04, bounceDir, bt, bn, bd);

        vec3 bounceEnd;
        if (bhit && bt < 22.0)
            bounceEnd = freeEnd + bounceDir * bt;
        else
            bounceEnd = freeEnd + bounceDir * 18.0;

        if (bounceEnd.z < freeEnd.z - 2.0)
            bounceEnd = freeEnd + bounceDir * 6.0;

        if (base + w < 1024u) {
            debugSegs[base + w].A = vec4(freeEnd, 0.0);
            debugSegs[base + w].B = vec4(bounceEnd, float(kind));
            w++;
        }
    }
}
";
    }
}