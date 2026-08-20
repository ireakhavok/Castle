// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticVisibilityShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticVisibilityShader
    {
        public static readonly string Source = @"
#version 430
layout(local_size_x = 64) in;

struct GpuTriangle
{
    vec4 A;
    vec4 B;
    vec4 C;
};

layout(std430, binding = 0) readonly buffer TriangleBuffer
{
    GpuTriangle triangles[];
};

// One int per sample: the triangle index that was hit (-1 = miss)
layout(std430, binding = 1) writeonly buffer HitIDBuffer
{
    int hitIDs[];
};

uniform vec3 uOrigin;
uniform int uTriangleCount;
uniform float uMaxDistance;
uniform int uGridRes;
uniform int uSampleCount;

bool rayTriangle(vec3 o, vec3 d, vec3 a, vec3 b, vec3 c, out float t, out vec3 n)
{
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

bool closestHit(vec3 pos, vec3 dir, out float tHit, out int hitIndex)
{
    tHit = uMaxDistance;
    hitIndex = -1;
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
                hitIndex = i;
                hit = true;
            }
        }
    }
    return hit;
}

// Independent hemisphere sample (source-agnostic). No look-at the other origin.
vec3 hemisphereDir(int sampleIdx, int gridRes)
{
    int gx = sampleIdx % gridRes;
    int gy = sampleIdx / gridRes;
    // Uniform-ish hemisphere: z up, covering full upper + lower for continuous coverage
    float u = (float(gx) + 0.5) / float(gridRes);
    float v = (float(gy) + 0.5) / float(gridRes);
    float phi = u * 6.28318530718;
    // Full sphere for independent continuous visibility (not just upper hemisphere)
    float cosTheta = 1.0 - 2.0 * v;
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
    return vec3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}

void main()
{
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uSampleCount)) return;

    vec3 dir = hemisphereDir(int(idx), uGridRes);

    float tHit;
    int hitIndex;
    if (closestHit(uOrigin, dir, tHit, hitIndex))
        hitIDs[idx] = hitIndex;
    else
        hitIDs[idx] = -1;
}
";
    }
}