// The ten looks, ported from the web build's post shader. Runs as a URP Full Screen Pass after post-processing.
// Works in display (sRGB) space like the original; input is the tonemapped frame, output goes back to linear.
// 0 STUDIO 1 DRAFT 2 RISO 3 GRAPHITE 4 MATRIX 5 OIL 6 GHOST 7 VINYL 8 GLYPH 9 PIXEL
Shader "VoidStunts/Looks"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "Looks"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _VS_From, _VS_To, _VS_WipeR, _VS_Flash, _VS_LineAmt;
            float4 _VS_WipeC; float4 _VS_Volt;

            static float2 RES; static float DPR;
            static float3 COL; static float LINE, L, SKY, VOLTM, R;

            float h12(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vn(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3. - 2. * f); return lerp(lerp(h12(i), h12(i + float2(1, 0)), f.x), lerp(h12(i + float2(0, 1)), h12(i + float2(1, 1)), f.x), f.y); }
            float2 fmodp(float2 x, float y) { return x - y * floor(x / y); }
            float3 sceneAt(float2 uv) { return LinearToSRGB(saturate(SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb)); }
            float rawDepth(float2 uv) { return SampleSceneDepth(uv); }
            float isSky(float2 uv)
            {
                float d = rawDepth(uv);
                #if UNITY_REVERSED_Z
                    return step(d, 1e-6);
                #else
                    return step(0.999999, d);
                #endif
            }
            float rz(float2 uv) { return 1.0 / max(LinearEyeDepth(rawDepth(uv), _ZBufferParams), 0.01); }
            float lum(float3 c) { return dot(c, float3(.299, .587, .114)); }
            float voltOf(float3 c) { return smoothstep(.22, .45, c.g - c.b) * smoothstep(.35, .6, c.r); }
            float2 rotA(float2 p, float c, float s) { return float2(c * p.x + s * p.y, -s * p.x + c * p.y); }   // GLSL mat2(c,-s,s,c) * p
            float2 rotAi(float2 p, float c, float s) { return float2(c * p.x - s * p.y, s * p.x + c * p.y); }  // transpose
            float bayer(float2 p)
            {
                float2 q = fmodp(floor(p), 4.); float i = q.x + q.y * 4.;
                static const float B[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return B[(int)i];
            }
            float3 pal16(float3 c)
            {
                static const float3 P[16] = { float3(.05,.045,.11), float3(.11,.1,.22), float3(.18,.17,.34), float3(.29,.25,.46), float3(.43,.36,.6), float3(.62,.48,.7),
                    float3(.86,.55,.67), float3(.95,.71,.62), float3(1.,.85,.63), float3(1.,.96,.86), float3(.95,.55,.24), float3(.75,.3,.24),
                    float3(.2,.4,.45), float3(.36,.62,.64), float3(.62,.84,.75), float3(.83,1.,0.) };
                float3 best = P[0]; float bd = 9.;
                [unroll] for (int i = 0; i < 16; i++) { float3 d = (c - P[i]) * float3(.9, 1.2, .7); float e = dot(d, d); if (e < bd) { bd = e; best = P[i]; } }
                return best;
            }

            float3 look(float m, float2 px, float2 uv)
            {
                float3 volt = _VS_Volt.rgb;
                if (m > 8.5) { // PIXEL — 16-bit dusk
                    float cell = 3. * DPR; float2 cid = floor(px / cell), cc = (cid + .5) * cell;
                    float3 s = sceneAt(cc / RES); float sk = isSky(cc / RES);
                    float3 g = lerp(s * float3(1.02, .95, 1.08), s * s * float3(1.1, .9, 1.2) + float3(.04, .02, .08), .35);
                    g = lerp(g, lerp(float3(.98, .72, .6), float3(.34, .27, .5), smoothstep(.3, 1., cc.y / RES.y)) * (.92 + .1 * lum(s)), sk * .7);
                    g += (bayer(cid) / 16. - .47) * .09;
                    float3 c = pal16(g);
                    float l0 = lum(s), lx = lum(sceneAt((cc + float2(cell, 0.)) / RES)), ly = lum(sceneAt((cc + float2(0., cell)) / RES));
                    float d0 = rz(cc / RES), dx = rz((cc + float2(cell, 0.)) / RES), dy = rz((cc + float2(0., cell)) / RES);
                    float edge = max(step(.16, abs(l0 - lx) + abs(l0 - ly)), step(.12, (abs(d0 - dx) + abs(d0 - dy)) / d0)) * (1. - sk);
                    c = lerp(c, float3(.05, .045, .11), edge * .85);
                    return lerp(c, float3(.83, 1., 0.), voltOf(s) * .9);
                }
                if (m < .5) return lerp(COL, float3(.07, .07, .07), LINE * .2 * _VS_LineAmt); // STUDIO
                if (m < 1.5) { // DRAFT
                    float3 paper = lerp(float3(.95, .947, .935), float3(.86, .857, .845), smoothstep(.2, 1.15, R));
                    float g1 = 12. * DPR; float2 l1 = abs(fmodp(px + g1 * .5, g1) - g1 * .5);
                    float g2 = 60. * DPR; float2 l2 = abs(fmodp(px + g2 * .5, g2) - g2 * .5);
                    paper *= 1. - (1. - smoothstep(0., .8 * DPR, min(l1.x, l1.y))) * .045 - (1. - smoothstep(0., .9 * DPR, min(l2.x, l2.y))) * .07;
                    float sh = 1. - L, sp = 5.5 * DPR;
                    float a1 = abs(frac((px.x + px.y) / sp) - .5) * sp, a2 = abs(frac((px.x - px.y) / sp) - .5) * sp;
                    float hatch = (1. - smoothstep(.35 * DPR, .9 * DPR, a1)) * smoothstep(.38, .48, sh) + (1. - smoothstep(.35 * DPR, .9 * DPR, a2)) * smoothstep(.6, .7, sh);
                    float ink = saturate(LINE * 1.3 + hatch * .5) * (1. - SKY);
                    float3 c = lerp(paper, float3(.1, .1, .115), ink);
                    return lerp(c, volt, VOLTM * .92);
                }
                if (m < 2.5) { // RISO
                    float3 paper = float3(.955, .937, .9);
                    float cell = 5.2 * DPR;
                    float2 q = rotA(px, .7071, .7071), cq = (floor(q / cell) + .5) * cell, cpx = rotAi(cq, .7071, .7071);
                    float3 s = sceneAt(cpx / RES); float ls = smoothstep(.12, .92, lum(s));
                    float rad = sqrt(saturate(1. - ls)) * cell * .6;
                    float k = (1. - smoothstep(rad - .6 * DPR, rad + .6 * DPR, length(q - cq))) * (1. - SKY);
                    float3 c = paper * lerp(.8, 1., lerp(smoothstep(.1, .9, L), 1., SKY));
                    c = lerp(c, float3(.13, .13, .15), k * .92);
                    float2 q2 = rotA(px + float2(1.3 * DPR, -.9 * DPR), .966, .259), cq2 = (floor(q2 / cell) + .5) * cell;
                    float3 s2 = sceneAt(rotAi(cq2, .966, .259) / RES);
                    float rv = sqrt(voltOf(s2)) * cell * .62, kv = 1. - smoothstep(rv - .6 * DPR, rv + .6 * DPR, length(q2 - cq2));
                    c = lerp(c, volt * float3(.98, .97, .9), max(kv * .95, VOLTM * .55));
                    return lerp(c, float3(.08, .08, .08), LINE * .85 * (1. - SKY));
                }
                if (m < 3.5) { // GRAPHITE
                    float3 bg = lerp(float3(.2, .2, .205), float3(.11, .11, .115), smoothstep(0., 1.05, R));
                    float gc = 9. * DPR; float2 gg = fmodp(px + gc * .5, gc) - gc * .5;
                    bg += (1. - smoothstep(.5 * DPR, 1.05 * DPR, length(gg))) * .075 * (1. - smoothstep(.45, 1.15, R) * .7);
                    float3 c = lerp(float3(.07, .07, .07) + COL * .3, bg, SKY);
                    c = lerp(c, float3(.93, .93, .93), LINE * .7 * (1. - SKY));
                    return lerp(c, COL, max(VOLTM, smoothstep(.93, 1., L)));
                }
                if (m < 4.5) { // MATRIX
                    float cell = 7. * DPR; float2 cid = floor(px / cell), cc = (cid + .5) * cell;
                    float3 s = sceneAt(cc / RES); float ls = lum(s), vs = voltOf(s);
                    float rad = (.14 + .38 * ls) * cell;
                    float k = 1. - smoothstep(rad - .6 * DPR, rad + .6 * DPR, length(px - cc));
                    return lerp(float3(.035, .035, .035), lerp(float3(.94, .94, .94) * (.35 + .65 * ls), volt, vs), k);
                }
                if (m < 5.5) { // OIL
                    float fa = vn(px / (46. * DPR)) * 6.2832 + vn(px / (130. * DPR)) * 3.;
                    float2 dir = float2(cos(fa), sin(fa)) * 2.1 * DPR;
                    float3 acc = 0; [unroll] for (int i = -3; i <= 3; i++) acc += sceneAt((px + dir * (float)i) / RES); acc /= 7.;
                    float l = lum(acc), q = floor(l * 5. + .5) / 5.; q = lerp(q, l, .45);
                    float3 p0 = float3(.1, .15, .16), p1 = float3(.24, .36, .37), p2 = float3(.71, .52, .3), p3 = float3(.95, .88, .72);
                    float3 ramp = q < .34 ? lerp(p0, p1, q / .34) : q < .68 ? lerp(p1, p2, (q - .34) / .34) : lerp(p2, p3, (q - .68) / .32);
                    float3 c = lerp(ramp, acc * float3(1.06, .98, .86), .22);
                    float sn = vn(px / (70. * DPR) + float2(_Time.y * .015, 0.));
                    float3 sky = lerp(float3(.9, .78, .58), float3(.33, .47, .5), smoothstep(.2, 1., uv.y)) + (sn - .5) * .1;
                    c = lerp(c, sky, SKY);
                    float stroke = vn(float2(dot(px, float2(dir.y, -dir.x)) / (1.6 * DPR), dot(px, dir) / (14. * DPR)));
                    c *= .94 + stroke * .1; c += (vn(px / (1.4 * DPR)) - .5) * .045;
                    c = lerp(c, float3(.07, .09, .1), LINE * .6 * (1. - SKY) * (.55 + .45 * vn(px / (5. * DPR))));
                    return lerp(c, float3(.93, .47, .2), VOLTM * .85);
                }
                if (m < 6.5) { // GHOST
                    float2 off = (uv - .5) * .006;
                    float lr = lum(sceneAt(uv + off)), lb = lum(sceneAt(uv - off));
                    float3 g = float3(.03, .08, .08) + float3(.22, .7, .62) * pow(L, 1.25) * .85;
                    g += float3(lr - L, 0., lb - L) * .7;
                    g = lerp(g, lerp(float3(.015, .035, .04), float3(.05, .13, .13), smoothstep(.3, 1., uv.y)), SKY * .9);
                    float gc = 24. * DPR; float2 gl = abs(fmodp(px + gc * .5, gc) - gc * .5);
                    g += (1. - smoothstep(0., .8 * DPR, min(gl.x, gl.y))) * .025 * SKY;
                    g = lerp(g, float3(.45, 1., .9), LINE * .8 * (1. - SKY));
                    g *= .9 + .1 * sin(px.y / DPR * 1.5708);
                    g += smoothstep(.985, 1., sin(uv.y * PI * 1.2 - _Time.y * .9)) * .035;
                    return lerp(g, float3(1., .66, .2), VOLTM * .95);
                }
                if (m < 7.5) { // VINYL
                    float2 o2 = float2(DPR * 2.6, DPR * 2.6) / RES; float w0 = rz(uv);
                    float sil2 = (abs(rz(uv + float2(o2.x, 0.)) - rz(uv - float2(o2.x, 0.))) + abs(rz(uv + float2(0., o2.y)) - rz(uv - float2(0., o2.y)))) / w0;
                    float thick = max(LINE, smoothstep(.06, .14, sil2));
                    float3 c = L < .3 ? float3(.16, .15, .18) : L < .58 ? float3(.55, .53, .6) : L < .86 ? float3(.86, .84, .88) : float3(.99, .98, .98);
                    c = lerp(c, float3(1., .5, .7), VOLTM);
                    c = lerp(c, lerp(float3(.97, .86, .9), float3(.82, .83, .93), smoothstep(.2, 1., uv.y)), SKY);
                    return lerp(c, float3(.07, .065, .08), saturate(thick * 1.3) * (1. - SKY));
                }
                { // GLYPH
                    float cell = 6. * DPR; float2 cid = floor(px / cell), cc = (cid + .5) * cell;
                    float3 s = sceneAt(cc / RES); float ls = lum(s), sk = isSky(cc / RES);
                    float rad = sqrt(saturate(1. - smoothstep(.12, .96, ls))) * cell * .5 * (1. - sk);
                    float k = 1. - smoothstep(rad - .55 * DPR, rad + .55 * DPR, length(px - cc));
                    float3 c = lerp(float3(.966, .962, .955), float3(.075, .075, .075), k);
                    c -= (1. - smoothstep(.4 * DPR, .9 * DPR, length(px - cc))) * .05 * SKY;
                    c = lerp(c, float3(.075, .075, .075), LINE * .92 * (1. - SKY));
                    return lerp(c, float3(.84, .09, .13), max(VOLTM, voltOf(s) * k) * .95);
                }
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                RES = _BlitTexture_TexelSize.zw; DPR = max(1., min(RES.x, RES.y) / 400.);
                float2 px = uv * RES; float asp = RES.x / RES.y;
                R = length((uv - .5) * float2(asp, 1.) * float2(1., .82));
                COL = sceneAt(uv); L = lum(COL); VOLTM = voltOf(COL); SKY = isSky(uv);
                float2 o = float2(DPR * 1.1, DPR * 1.1) / RES;
                float w0 = rz(uv), wl = rz(uv - float2(o.x, 0.)), wr = rz(uv + float2(o.x, 0.)), wd = rz(uv - float2(0., o.y)), wu = rz(uv + float2(0., o.y));
                float crease = (abs(wl + wr - 2. * w0) + abs(wd + wu - 2. * w0)) / w0;
                float sil = (abs(wr - wl) + abs(wu - wd)) / w0;
                float nearW = 1. - smoothstep(.004, .0016, w0);
                float el = abs(lum(sceneAt(uv - float2(o.x, 0.))) - lum(sceneAt(uv + float2(o.x, 0.)))) + abs(lum(sceneAt(uv - float2(0., o.y))) - lum(sceneAt(uv + float2(0., o.y))));
                LINE = saturate(max(smoothstep(.01, .04, crease) * nearW, smoothstep(.05, .16, sil)) + smoothstep(.2, .4, el) * .3);
                float3 c = look(_VS_From, px, uv);
                if (_VS_WipeR > 0.)
                {
                    float dd = distance(px, _VS_WipeC.xy), w = 1. - smoothstep(_VS_WipeR - 22. * DPR, _VS_WipeR, dd);
                    if (w > 0.) c = lerp(c, look(_VS_To, px, uv), w);
                    float ring = 1. - smoothstep(0., 1.4 * DPR, abs(dd - _VS_WipeR + 11. * DPR));
                    c = lerp(c, _VS_Volt.rgb, ring * .85);
                }
                if (_VS_From > .5 || _VS_WipeR > 0.) c *= lerp(.86, 1., smoothstep(1.25, .45, length((uv - .5) * float2(asp * .9, 1.))));
                c = lerp(c, float3(1, 1, 1), _VS_Flash * .35);
                c += (h12(px + frac(_Time.y) * 91.) - .5) * .01;
                return half4(SRGBToLinear(saturate(c)), 1);
            }
            ENDHLSL
        }
    }
}
