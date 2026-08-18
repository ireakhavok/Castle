// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticComputeShader.cs
using System;

namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticComputeShader
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

vec3 randomUnit(float seed)
{
    float z = hash(seed) * 2.0 - 1.0;
    float a = hash(seed + 17.0) * 6.2831853;
    float r = sqrt(max(0.0, 1.0 - z * z));
    return vec3(r * cos(a), r * sin(a), z);
}

bool rayTriangle(vec3 origin, vec3 dir, vec3 a, vec3 b, vec3 c, out float t, out vec3 normal)
{
    vec3 e1 = b - a; vec3 e2 = c - a;
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
    tHit = uMaxDistance; nHit = vec3(0); dens = 1.0; bool hit = false;
    for (int i = 0; i < uTriangleCount; i++)
    {
        float t; vec3 n;
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

    // ============================================================
    // DEBUG MODE
    // ============================================================
    if (uDebugMode != 0)
    {
        const uint slotsPerRay = 8u;
        uint base = idx * slotsPerRay;
        for (uint s = 0u; s < slotsPerRay; s++)
        {
            if (base + s < 1024u)
            {
                debugSegs[base + s].A = vec4(0.0);
                debugSegs[base + s].B = vec4(0.0, 0.0, 0.0, -1.0);
            }
        }

        bool fromListener = (idx < uint(uRayCount / 2));
        vec3 origin = fromListener ? uListenerPos : uSourcePos;
        int originKind = fromListener ? 0 : 1;
        vec3 opposite = fromListener ? uSourcePos : uListenerPos;

        vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);
        vec3 pos = origin;
        vec3 curDir = dir;
        uint written = 0u;

        for (int iter = 0; iter < 3; ++iter)
        {
            float tHit;
            vec3 nHit;
            float dens;
            bool hit = closestHit(pos, curDir, tHit, nHit, dens);

            vec3 freeEnd;
            if (hit && tHit < uMaxDistance)
                freeEnd = pos + curDir * tHit;
            else
                freeEnd = pos + curDir * min(uMaxDistance * 0.55, 70.0);

            if (base + written < 1024u)
            {
                debugSegs[base + written].A = vec4(pos, 0.0);
                debugSegs[base + written].B = vec4(freeEnd, float(originKind));
                written++;
            }

            vec3 connect = opposite - freeEnd;
            float cLen = length(connect);
            if (cLen > 0.8 && cLen < 90.0)
            {
                vec3 cDir = connect / cLen;
                float cosAng = clamp(dot(normalize(curDir), cDir), -1.0, 1.0);
                float ang = acos(cosAng);

                if (ang <= 0.174533)
                {
                    float tC;
                    vec3 nC;
                    float dC;
                    bool blocked = closestHit(freeEnd, cDir, tC, nC, dC);

                    if (!blocked || tC >= cLen - 0.25)
                    {
                        if (base + written < 1024u)
                        {
                            debugSegs[base + written].A = vec4(freeEnd - vec3(0.55, 0.0, 0.0), 0.0);
                            debugSegs[base + written].B = vec4(freeEnd + vec3(0.55, 0.0, 0.0), 2.0);
                            written++;
                        }
                        if (base + written < 1024u)
                        {
                            debugSegs[base + written].A = vec4(freeEnd - vec3(0.0, 0.55, 0.0), 0.0);
                            debugSegs[base + written].B = vec4(freeEnd + vec3(0.0, 0.55, 0.0), 2.0);
                            written++;
                        }
                        if (base + written < 1024u)
                        {
                            debugSegs[base + written].A = vec4(freeEnd, 0.0);
                            debugSegs[base + written].B = vec4(freeEnd + cDir * min(cLen * 0.45, 12.0), 2.0);
                            written++;
                        }
                        break;
                    }
                }
            }

            if (!hit) break;

            vec3 reflected = reflect(curDir, nHit);
            float jitter = (hash(float(idx) * 4.7 + float(iter) * 9.1) - 0.5) * 0.45;
            vec3 tangent = normalize(cross(nHit, reflected + vec3(0.01, 0.0, 0.0)));
            curDir = normalize(reflected + nHit * 0.04 + tangent * jitter);
            pos = freeEnd + curDir * 0.06;
        }
        return;
    }

    // ============================================================
    // RESIDUAL / CONTINUOUS MODE (unchanged)
    // ============================================================
    bool fromListener = (idx % 2u) == 0u;
    vec3 origin = fromListener ? uListenerPos : uSourcePos;
    vec3 target = fromListener ? uSourcePos : uListenerPos;
    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);

    float green = 0.0, blue = 0.0, orange = 0.0, yellow = 0.0;
    float distanceTravelled = 0.0, lowPass = 18000.0;
    bool reached = false;
    vec3 arrivalDir = vec3(0.0);
    float pathWeight = 1.0;
    int pathwayType = 0;
    vec3 freeEnd = origin;

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

        vec3 hitPoint = origin + dir * tHit;
        freeEnd = hitPoint;

        vec3 reflected = reflect(dir, nHit);
        float jitter = (hash(float(idx) * 4.7 + float(bounce) * 9.1) - 0.5) * 0.55;
        vec3 tangent = normalize(cross(nHit, reflected + vec3(0.01)));
        dir = normalize(reflected + nHit * 0.05 + tangent * jitter);
        origin = hitPoint + dir * 0.04;
        pathWeight *= 0.92;
        pathwayType = 0;
    }

    float totalEnergy = green * 1.4 + blue * 0.95 + yellow * 1.15 + orange * 0.25;
    if (reached && totalEnergy > 0.0002)
    {
        float intensity = clamp(totalEnergy * 3.8, 0.0, 1.0);
        float directDist = length(uSourcePos - uListenerPos);
        if (distanceTravelled > directDist * 1.8) intensity *= 0.75;
        else if (distanceTravelled < directDist * 1.35) intensity = min(1.0, intensity * 1.1);

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
}
";
    }
}