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
uniform vec3 uLookAt;
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

void main()
{
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uSampleCount)) return;

    int gx = int(idx) % uGridRes;
    int gy = int(idx) / uGridRes;

    float ndcX = (float(gx) + 0.5) / float(uGridRes) * 2.0 - 1.0;
    float ndcY = (float(gy) + 0.5) / float(uGridRes) * 2.0 - 1.0;
    float fovScale = 1.7;

    vec3 forward = normalize(uLookAt - uOrigin);
    if (length(forward) < 1e-6) forward = vec3(0.0, 1.0, 0.0);
    vec3 up = abs(forward.z) < 0.99 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0);
    vec3 right = normalize(cross(forward, up));
    up = cross(right, forward);

    vec3 dir = normalize(forward + right * (ndcX * fovScale) + up * (ndcY * fovScale));

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