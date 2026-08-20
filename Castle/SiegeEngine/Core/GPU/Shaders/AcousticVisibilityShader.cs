// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticVisibilityShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticVisibilityShader
    {
        public static readonly string Source = @"
#version 430
layout(local_size_x = 64) in;

struct GpuTriangle {
    vec4 A;
    vec4 B;
    vec4 C;
};

struct GpuVisibility {
    float ListenerVisible;
    float SourceVisible;
    float Pad0;
    float Pad1;
};

layout(std430, binding = 0) readonly buffer TriangleBuffer {
    GpuTriangle triangles[];
};

layout(std430, binding = 1) writeonly buffer VisibilityBuffer {
    GpuVisibility visibility[];
};

uniform vec3 uListenerPos;
uniform vec3 uSourcePos;
uniform int uTriangleCount;
uniform float uMaxDistance;

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

bool closestHit(vec3 pos, vec3 dir, out float tHit, out int hitIndex) {
    tHit = uMaxDistance;
    hitIndex = -1;
    bool hit = false;
    for (int i = 0; i < uTriangleCount; i++) {
        float t;
        vec3 n;
        if (rayTriangle(pos, dir, triangles[i].A.xyz, triangles[i].B.xyz, triangles[i].C.xyz, t, n)) {
            if (t > 0.001 && t < tHit) {
                tHit = t;
                hitIndex = i;
                hit = true;
            }
        }
    }
    return hit;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uTriangleCount)) return;

    vec3 a = triangles[idx].A.xyz;
    vec3 b = triangles[idx].B.xyz;
    vec3 c = triangles[idx].C.xyz;
    // 'centroid' is a reserved GLSL keyword — use triCenter instead
    vec3 triCenter = (a + b + c) * 0.33333333;

    float listenerVis = 0.0;
    float sourceVis = 0.0;

    // Listener viewport
    vec3 toL = triCenter - uListenerPos;
    float distL = length(toL);
    if (distL > 0.001) {
        vec3 dirL = toL / distL;
        float tHit;
        int hitIdx;
        if (closestHit(uListenerPos, dirL, tHit, hitIdx)) {
            if (hitIdx == int(idx) || abs(tHit - distL) < 0.4)
                listenerVis = 1.0;
        } else if (distL < 120.0) {
            listenerVis = 1.0;
        }
    }

    // Source viewport
    vec3 toS = triCenter - uSourcePos;
    float distS = length(toS);
    if (distS > 0.001) {
        vec3 dirS = toS / distS;
        float tHit;
        int hitIdx;
        if (closestHit(uSourcePos, dirS, tHit, hitIdx)) {
            if (hitIdx == int(idx) || abs(tHit - distS) < 0.4)
                sourceVis = 1.0;
        } else if (distS < 120.0) {
            sourceVis = 1.0;
        }
    }

    visibility[idx].ListenerVisible = listenerVis;
    visibility[idx].SourceVisible = sourceVis;
    visibility[idx].Pad0 = 0.0;
    visibility[idx].Pad1 = 0.0;
}
";
    }
}