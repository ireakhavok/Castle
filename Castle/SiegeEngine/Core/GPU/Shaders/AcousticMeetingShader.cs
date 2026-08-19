// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticMeetingShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticMeetingShader
    {
        public static readonly string Source = @"
#version 430
layout(local_size_x = 64) in;

struct GpuTriangle { vec4 A; vec4 B; vec4 C; };
struct GpuRayResult { float Intensity; float Delay; float LowPass; float Pad; vec4 ArrivalDir; };
struct GpuDebugSegment { vec4 A; vec4 B; };

layout(std430, binding = 0) readonly buffer TriangleBuffer { GpuTriangle triangles[]; };
layout(std430, binding = 1) writeonly buffer ResultBuffer { GpuRayResult results[]; };
layout(std430, binding = 2) buffer DebugBuffer { GpuDebugSegment debugSegs[]; };

uniform vec3 uSourcePos;
uniform vec3 uListenerPos;
uniform int uTriangleCount;
uniform int uRayCount;
uniform int uMaxBounces;
uniform float uListenerRadius;
uniform float uMaxDistance;
uniform int uDebugMode;
uniform int uSourceCount;

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
    if (idx >= uint(uRayCount / 2)) return;

    uint myBase = idx * 4u;
    if (myBase + 1u >= 1024u) return;

    vec3 freeL = debugSegs[myBase + 1u].A.xyz;
    vec3 dirL  = debugSegs[myBase + 1u].B.xyz;
    if (length(dirL) < 0.1) return;

    uint halfCount = uint(uRayCount / 2);
    float bestLen = 1e9;
    vec3 bestMeet = vec3(0);
    bool found = false;

    // Sample more opposite free-ends
    for (int s = 0; s < 24; s++) {
        uint srcIdx = halfCount + uint((idx * 7u + uint(s) * 13u) % halfCount);
        uint srcBase = srcIdx * 4u;
        if (srcBase + 1u >= 1024u) continue;

        vec3 freeS = debugSegs[srcBase + 1u].A.xyz;
        vec3 dirS  = debugSegs[srcBase + 1u].B.xyz;
        if (length(dirS) < 0.1) continue;

        vec3 conn = freeS - freeL;
        float clen = length(conn);
        if (clen < 1.0 || clen > 55.0) continue;

        vec3 cdir = conn / clen;

        // Wider angular acceptance (~25°)
        float angL = acos(clamp(dot(normalize(dirL), cdir), -1.0, 1.0));
        float angS = acos(clamp(dot(normalize(dirS), -cdir), -1.0, 1.0));
        if (angL > 0.45 || angS > 0.45) continue;

        float tC; vec3 nC; float dC;
        bool blocked = closestHit(freeL + cdir * 0.1, cdir, tC, nC, dC);
        float ratio = blocked ? (tC / clen) : 1.0;
        if (ratio < 0.92) continue;

        float total = length(freeL - uListenerPos) + clen + length(freeS - uSourcePos);
        if (total < bestLen) {
            bestLen = total;
            bestMeet = freeL + cdir * (clen * 0.5);
            found = true;
        }
    }

    if (found) {
        uint meetSlot = myBase + 2u;
        if (meetSlot < 1024u) {
            debugSegs[meetSlot].A = vec4(bestMeet - vec3(0.55, 0.0, 0.0), 0.0);
            debugSegs[meetSlot].B = vec4(bestMeet + vec3(0.55, 0.0, 0.0), 2.0);
        }
        if (meetSlot + 1u < 1024u) {
            debugSegs[meetSlot + 1u].A = vec4(bestMeet - vec3(0.0, 0.55, 0.0), 0.0);
            debugSegs[meetSlot + 1u].B = vec4(bestMeet + vec3(0.0, 0.55, 0.0), 2.0);
        }
    }
}
";
    }
}