#ifndef VS_COMMON_INCLUDED
#define VS_COMMON_INCLUDED
// Shared helpers: hashes, hex-tiling (Mikkelsen 2022, "Practical Real-Time Hex-Tiling"), procedural facades.

float VSHash12(float2 p) { float3 p3 = frac(float3(p.xyx) * .1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
float2 VSHash22(float2 p) { float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973)); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.xx + p3.yz) * p3.zy); }

// --- hex tiling: three rotated, offset samples of the same texture per pixel, blended by a sharpened barycentric weight ---
void VSTriangleGrid(float2 st, out float w1, out float w2, out float w3, out int2 v1, out int2 v2, out int2 v3)
{
    st *= 2.0 * 1.7320508; // hex cells ≈ 1/3.5 of a texture repeat
    const float2x2 gridToSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
    float2 skewed = mul(gridToSkewed, st);
    int2 baseId = int2(floor(skewed));
    float3 temp = float3(frac(skewed), 0); temp.z = 1.0 - temp.x - temp.y;
    float s = step(0.0, -temp.z), s2 = 2 * s - 1;
    w1 = -temp.z * s2; w2 = s - temp.y * s2; w3 = s - temp.x * s2;
    v1 = baseId + int2(s, s); v2 = baseId + int2(s, 1 - s); v3 = baseId + int2(1 - s, s);
}
float2x2 VSHexRot(int2 idx, float strength)
{
    float a = abs(idx.x * idx.y) + abs(idx.x + idx.y) + PI;
    a = fmod(a, 2 * PI); if (a < 0) a += 2 * PI; if (a > PI) a -= 2 * PI;
    a *= strength; float c = cos(a), s = sin(a);
    return float2x2(c, -s, s, c);
}
float2 VSHexCenter(int2 v) { const float2x2 inv = float2x2(1.0, 0.5, 0.0, 1.0 / 1.15470054); return mul(inv, float2(v)) / (2.0 * 1.7320508); }

struct VSHexSample { float3 albedo; float3 normalTS; float3 mask; };

VSHexSample VSHexTile(TEXTURE2D_PARAM(tA, sA), TEXTURE2D_PARAM(tN, sN), TEXTURE2D_PARAM(tM, sM), float2 uv, float contrast)
{
    float2 dx = ddx(uv), dy = ddy(uv);
    float w1, w2, w3; int2 v1, v2, v3; VSTriangleGrid(uv, w1, w2, w3, v1, v2, v3);
    float2x2 r1 = VSHexRot(v1, 1), r2 = VSHexRot(v2, 1), r3 = VSHexRot(v3, 1);
    float2 c1 = VSHexCenter(v1), c2 = VSHexCenter(v2), c3 = VSHexCenter(v3);
    float2 u1 = mul(uv - c1, r1) + c1 + VSHash22(float2(v1)), u2 = mul(uv - c2, r2) + c2 + VSHash22(float2(v2)), u3 = mul(uv - c3, r3) + c3 + VSHash22(float2(v3));
    float3 a1 = SAMPLE_TEXTURE2D_GRAD(tA, sA, u1, mul(dx, r1), mul(dy, r1)).rgb;
    float3 a2 = SAMPLE_TEXTURE2D_GRAD(tA, sA, u2, mul(dx, r2), mul(dy, r2)).rgb;
    float3 a3 = SAMPLE_TEXTURE2D_GRAD(tA, sA, u3, mul(dx, r3), mul(dy, r3)).rgb;
    // luminance-weighted blend keeps contrast (no blurry cross-fade)
    float3 Lw = float3(0.299, 0.587, 0.114);
    float3 Dw = float3(dot(a1, Lw), dot(a2, Lw), dot(a3, Lw));
    Dw = lerp(1.0, Dw, contrast);
    float3 W = Dw * pow(float3(w1, w2, w3), 4.0); // softer seams: no hard-edged cells
    W /= (W.x + W.y + W.z);
    VSHexSample o;
    o.albedo = W.x * a1 + W.y * a2 + W.z * a3;
    float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tN, sN, u1, mul(dx, r1), mul(dy, r1)));
    float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tN, sN, u2, mul(dx, r2), mul(dy, r2)));
    float3 n3 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tN, sN, u3, mul(dx, r3), mul(dy, r3)));
    n1.xy = mul(r1, n1.xy); n2.xy = mul(r2, n2.xy); n3.xy = mul(r3, n3.xy);
    o.normalTS = normalize(W.x * n1 + W.y * n2 + W.z * n3);
    o.mask = W.x * SAMPLE_TEXTURE2D_GRAD(tM, sM, u1, mul(dx, r1), mul(dy, r1)).rgb
           + W.y * SAMPLE_TEXTURE2D_GRAD(tM, sM, u2, mul(dx, r2), mul(dy, r2)).rgb
           + W.z * SAMPLE_TEXTURE2D_GRAD(tM, sM, u3, mul(dx, r3), mul(dy, r3)).rgb;
    return o;
}

// anti-aliased box (1 inside [lo,hi], soft by one pixel)
float VSBox(float2 p, float2 lo, float2 hi, float2 fw)
{
    fw = max(fw, 1e-4);
    float2 a = smoothstep(lo - fw, lo + fw, p) * (1 - smoothstep(hi - fw, hi + fw, p));
    return a.x * a.y;
}
#endif
