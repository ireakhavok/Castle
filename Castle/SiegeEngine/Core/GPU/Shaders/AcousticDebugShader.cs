// Folder: SiegeEngine/Core/GPU/Shaders
// File: AcousticDebugShader.cs
namespace SiegeEngine.Core.GPU.Shaders
{
    public static class AcousticDebugShader
    {
        public static readonly string Source = @"
bool refineClear(vec3 origin, vec3 coarse, vec3 target, float sector, out vec3 bestFree, out vec3 bestConn) {
    bestFree = origin; bestConn = origin;
    float best = 1e9; bool ok = false;
    for (int s = 0; s < 7; s++) {
        float off = (float(s) / 6.0 - 0.5) * 2.0 * sector;
        vec3 up = abs(coarse.z) < 0.9 ? vec3(0,0,1) : vec3(0,1,0);
        vec3 axis = normalize(cross(coarse, up));
        float c = cos(off), si = sin(off);
        vec3 dir = normalize(coarse * c + cross(axis, coarse) * si + axis * dot(axis, coarse) * (1.0 - c));
        float th; vec3 nh; float dn;
        bool h = closestHit(origin, dir, th, nh, dn);
        vec3 fe = h && th < uMaxDistance * 0.95 ? origin + dir * th : origin + dir * min(uMaxDistance * 0.5, 60.0);
        vec3 toT = target - fe;
        float cl = length(toT);
        if (cl < 0.4 || cl > 90.0) continue;
        vec3 cd = toT / cl;
        float tc; vec3 nc; float dc;
        bool blk = closestHit(fe, cd, tc, nc, dc);
        float ratio = blk ? tc / cl : 1.0;
        if (ratio < 0.92) continue;
        float tot = length(fe - origin) + cl;
        if (tot < best) {
            best = tot; bestFree = fe;
            bestConn = fe + cd * (blk ? tc * 0.98 : cl * 0.6);
            ok = true;
        }
    }
    return ok;
}

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= uint(uRayCount)) return;

    uint base = idx * 8u;
    for (uint s = 0u; s < 8u; s++) if (base + s < 1024u) {
        debugSegs[base + s].A = vec4(0);
        debugSegs[base + s].B = vec4(0,0,0,-1);
    }

    bool fromL = idx < uint(uRayCount / 2);
    vec3 origin = fromL ? uListenerPos : uSourcePos;
    int kind = fromL ? 0 : 1;
    vec3 opposite = fromL ? uSourcePos : uListenerPos;
    vec3 dir = randomUnit(float(idx) * 12.9898 + 0.13);
    vec3 pos = origin;
    vec3 cur = dir;
    uint w = 0u;

    for (int iter = 0; iter < 3; iter++) {
        float th; vec3 nh; float dn;
        bool hit = closestHit(pos, cur, th, nh, dn);
        vec3 fe = hit && th < uMaxDistance ? pos + cur * th : pos + cur * min(uMaxDistance * 0.55, 70.0);

        if (base + w < 1024u) {
            debugSegs[base + w].A = vec4(pos, 0);
            debugSegs[base + w].B = vec4(fe, float(kind));
            w++;
        }

        float sector = iter == 0 ? 0.4363 : 0.3491;
        vec3 toO = opposite - fe;
        float dO = length(toO);
        if (dO > 1.0 && dO < 95.0) {
            float ang = acos(clamp(dot(normalize(cur), normalize(toO)), -1.0, 1.0));
            if (ang <= sector) {
                vec3 rFree, rConn;
                if (refineClear(pos, cur, opposite, sector, rFree, rConn)) {
                    if (base + w < 1024u) {
                        debugSegs[base + w].A = vec4(pos, 0);
                        debugSegs[base + w].B = vec4(rFree, float(kind));
                        w++;
                    }
                    if (base + w < 1024u) {
                        debugSegs[base + w].A = vec4(rFree - vec3(0.6,0,0), 0);
                        debugSegs[base + w].B = vec4(rFree + vec3(0.6,0,0), 2);
                        w++;
                    }
                    if (base + w < 1024u) {
                        debugSegs[base + w].A = vec4(rFree - vec3(0,0.6,0), 0);
                        debugSegs[base + w].B = vec4(rFree + vec3(0,0.6,0), 2);
                        w++;
                    }
                    if (base + w < 1024u) {
                        debugSegs[base + w].A = vec4(rFree, 0);
                        debugSegs[base + w].B = vec4(rConn, 2);
                        w++;
                    }
                    break;
                }
            }
        }

        if (!hit) break;
        vec3 refl = reflect(cur, nh);
        float j = (hash(float(idx) * 4.7 + float(iter) * 9.1) - 0.5) * 0.4;
        vec3 tan = normalize(cross(nh, refl + vec3(0.01,0,0)));
        cur = normalize(refl + nh * 0.03 + tan * j);
        pos = fe + cur * 0.05;
    }
}
";
    }
}