// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticResidualShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticResidualShader
    {
        public static readonly string Source = @"
void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uRayCount)) return;

    bool fromL = (idx % 2u) == 0u;
    vec3 origin = fromL ? uListenerPos : uSourcePos;
    vec3 target = fromL ? uSourcePos : uListenerPos;
    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);
    float green = 0.0, blue = 0.0, orange = 0.0, yellow = 0.0;
    float dist = 0.0, low = 18000.0;
    bool reached = false;
    vec3 arr = vec3(0);
    float wt = 1.0;
    int ptype = 0;

    for (int b = 0; b < uMaxBounces; b++) {
        vec3 toT = target - origin;
        float dT = length(toT);
        if (dT < uListenerRadius) {
            dist += dT; arr = normalize(uListenerPos - origin);
            green += wt / max(dist * dist, 0.2); reached = true; ptype = 2; break;
        }
        if (dT > 0.01) {
            vec3 ld = toT / dT; float tl; vec3 nl; float dl;
            bool blk = closestHit(origin, ld, tl, nl, dl);
            if (!blk || tl >= dT - 0.12) {
                dist += dT; arr = normalize(uListenerPos - origin);
                green += wt / max(dist * dist, 0.2); reached = true; ptype = 2; break;
            }
        }
        float th; vec3 nh; float dn;
        if (!closestHit(origin, dir, th, nh, dn)) {
            float d = length(target - origin);
            if (d < uMaxDistance * 0.95) {
                dist += d; arr = normalize(uListenerPos - origin);
                yellow += wt * 0.9 / max(dist * dist, 0.4); reached = true; ptype = 1;
            }
            break;
        }
        dist += th;
        if (dist > uMaxDistance) break;
        float R = clamp(exp(-0.18 * dn), 0.30, 0.88);
        float T = clamp(0.12 / (dn * dn), 0.008, 0.18);
        float r2 = max(dist * dist, 0.5);
        blue += wt * R * 0.75 / r2;
        orange += wt * T * 0.35 / r2;
        yellow += wt * (1.0 - abs(dot(dir, nh))) * 0.55 / r2;
        low = min(low, 14000.0 / (1.0 + dn * 1.6 + dist * 0.015));
        vec3 hp = origin + dir * th;
        vec3 refl = reflect(dir, nh);
        float j = (hash(float(idx) * 4.7 + float(b) * 9.1) - 0.5) * 0.55;
        vec3 tan = normalize(cross(nh, refl + vec3(0.01)));
        dir = normalize(refl + nh * 0.05 + tan * j);
        origin = hp + dir * 0.04;
        wt *= 0.92; ptype = 0;
    }

    float tot = green * 1.4 + blue * 0.95 + yellow * 1.15 + orange * 0.25;
    if (reached && tot > 0.0002) {
        float inten = clamp(tot * 3.8, 0.0, 1.0);
        float dd = length(uSourcePos - uListenerPos);
        if (dist > dd * 1.8) inten *= 0.75;
        else if (dist < dd * 1.35) inten = min(1.0, inten * 1.1);
        results[idx].Intensity = inten;
        results[idx].Delay = dist / 34300.0;
        results[idx].LowPass = low;
        results[idx].ArrivalDir = vec4(normalize(arr), 0);
        results[idx].Pad = float(ptype);
    } else {
        results[idx].Intensity = 0.0;
        results[idx].Delay = 0.0;
        results[idx].LowPass = 0.0;
        results[idx].ArrivalDir = vec4(0);
        results[idx].Pad = 0.0;
    }
}
";
    }
}