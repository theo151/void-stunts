using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// VS-02, frozen at v0.8. A number-for-number port of buildCar()/carVisual() from the web build.
    /// Everything lives under "Shell", which has scale (-1,1,1): that converts three.js's right-handed space to Unity's
    /// left-handed one, so every position and XYZ-Euler rotation is copied verbatim and the car renders identically.
    /// </summary>
    public class CarVisual : MonoBehaviour
    {
        // packaging (frozen): every wheel has a reserved envelope at full lock and full travel; the body is clamped out of it
        public static class WV
        {
            public const float fx = .98f, rx = 1.0f, fz = 1.32f, rz = -1.28f, fr = .37f, rr = .38f, fw = .30f, rw = .38f, cyMin = -.62f, cyMax = -.26f, steerMax = .62f, fenR = .46f, fenWF = .38f, fenWR = .44f, gap = .05f;
            public static readonly float frontIn = fx - (fenWF / 2 * Mathf.Cos(steerMax) + fenR * Mathf.Sin(steerMax)) - gap, frontZ = Mathf.Sqrt(fenR * fenR + fenWF * fenWF / 4) + gap;
            public static readonly float rearIn = rx - fenWR / 2 - gap, rearZ = fenR + gap;
        }
        static float BodyLimit(float z) { float L = 9; if (Mathf.Abs(z - WV.fz) < WV.frontZ) L = Mathf.Min(L, WV.frontIn); if (Mathf.Abs(z - WV.rz) < WV.rearZ) L = Mathf.Min(L, WV.rearIn); return L; }
        static readonly float[][] BODY_ST = {
            new[] { -2.18f, .48f, .16f, -.16f }, new[] { -2.06f, .58f, .24f, -.24f }, new[] { -1.70f, .64f, .30f, -.28f }, new[] { -1.10f, .67f, .34f, -.30f }, new[] { -0.50f, .70f, .37f, -.31f }, new[] { 0.10f, .70f, .36f, -.31f },
            new[] { 0.60f, .60f, .30f, -.30f }, new[] { 0.95f, .47f, .24f, -.29f }, new[] { 1.40f, .45f, .16f, -.28f }, new[] { 1.85f, .40f, .07f, -.27f }, new[] { 2.10f, .30f, -.01f, -.24f }, new[] { 2.24f, .14f, -.08f, -.19f } };
        static readonly float[][] CANOPY_ST = { new[] { -1.25f, .34f, .30f, .08f }, new[] { -0.90f, .43f, .55f, .08f }, new[] { -0.30f, .46f, .74f, .08f }, new[] { 0.30f, .41f, .66f, .08f }, new[] { 0.80f, .29f, .40f, .1f }, new[] { 1.0f, .12f, .2f, .12f } };

        public struct Livery { public string name; public Color trim, face, cap, tyre, canopy; public float rough, canopyA; }
        public static readonly Livery[] LIVERIES = {
            new Livery { name = "TRANSPARENT", rough = .4f, face = Hex("#f4f3ef"), cap = Hex("#16171a"), tyre = Hex("#151516"), canopy = Hex("#dfe5e7"), canopyA = .2f, trim = Hex("#ecebe6") },
            new Livery { name = "SECTION", rough = .72f, face = Hex("#262722"), cap = Hex("#f0ac2c"), tyre = Hex("#1a1a17"), canopy = Hex("#9cb2a8"), canopyA = .34f, trim = Hex("#6c6f5b") },
            new Livery { name = "VINYL", rough = .86f, face = Hex("#ff8fb8"), cap = Hex("#141416"), tyre = Hex("#29292d"), canopy = Hex("#cfc6d6"), canopyA = .3f, trim = Hex("#141416") },
            new Livery { name = "OIL", rough = .62f, face = Hex("#1d2a2c"), cap = Hex("#e9dcbc"), tyre = Hex("#1b1b19"), canopy = Hex("#9fc6c1"), canopyA = .3f, trim = Hex("#1d2a2c") },
            new Livery { name = "INK", rough = .3f, face = Hex("#1f2022"), cap = Hex("#d4ff00"), tyre = Hex("#0f0f10"), canopy = Hex("#8d9396"), canopyA = .38f, trim = Hex("#17181a") },
        };
        public struct Plume { public string name; public Vector4 core, sheath; public Color swatch; }
        public static readonly Plume[] PLUMES = {
            new Plume { name = "ION", core = new Vector4(2.4f, 2.8f, 3.4f), sheath = new Vector4(.32f, .62f, 1.5f), swatch = Hex("#8fc4ff") },
            new Plume { name = "VOLT", core = new Vector4(3f, 3.1f, 2.3f), sheath = new Vector4(.95f, 1.45f, 0f), swatch = Hex("#d4ff00") },
            new Plume { name = "SIGNAL", core = new Vector4(3.3f, 2.7f, 2.5f), sheath = new Vector4(1.7f, .12f, .1f), swatch = Hex("#e3262c") },
            new Plume { name = "AMBER", core = new Vector4(3.3f, 2.9f, 2.1f), sheath = new Vector4(1.7f, .62f, .08f), swatch = Hex("#ffa12a") },
        };
        public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        class Wheel { public Transform steer, spin, la, lb, up, damp, sh, spring, knuckle; public bool front; public float s; }
        Transform shell; readonly List<Wheel> wheels = new(); readonly List<Transform> petals = new();
        Material plumeA, plumeB, canopyMat;
        float heat, on;
        public int livery, plume;
        public float[] loftS; // body loft samples (for the clearance check)
        List<float[]> bodyS;

        public void Build()
        {
            shell = new GameObject("Shell").transform; shell.SetParent(transform, false); shell.localScale = new Vector3(-1, 1, 1);
            var S = shell;
            // body and caps
            var body = Loft(BODY_ST, 48, 3.1f, true, out bodyS);
            Part("Body", body.Item1, Lib.Paint, Vector3.zero); Part("Caps", body.Item2, Lib.Paint, Vector3.zero);
            // smoked canopy over the product-diagram internals
            var can = Loft(CANOPY_ST, 40, 2.6f, false, out _);
            canopyMat = new Material(Lib.GlassSmoke); Part("Canopy", can.Item1, canopyMat, Vector3.zero, shadows: false);
            Part("Board", Geo.Box(.8f, .02f, 2.0f), Lib.Board, new Vector3(0, .34f, -.28f));
            Part("Cell", Geo.RoundedBox(.5f, .14f, .7f, 3, .05f), Lib.CellWhite, new Vector3(0, .42f, 0));
            Part("Dot", Geo.Cylinder(.05f, .05f, .03f, 24), Lib.LedVolt, new Vector3(.15f, .5f, .2f), shadows: false);
            Part("Coil", Geo.Torus(.2f, .045f, 12, 40), Lib.Ink, new Vector3(0, .4f, -.86f), new Vector3(Mathf.PI / 2, 0, 0));
            Part("Arc", Geo.Torus(.28f, .011f, 6, 48, Mathf.PI * 1.3f), Lib.CarLed, new Vector3(0, .39f, -.86f), new Vector3(-Mathf.PI / 2, 0, .9f), shadows: false);
            // glyph light: one line along each shoulder, unbroken round the tail
            var gp = new List<Vector3>(); for (int i = 0; i <= 26; i++) gp.Add(Surf(1.75f - i * (3.75f / 26), .42f, .012f));
            float ty0 = gp[^1].y;
            var gl = new List<Vector3>(gp) { new(.53f, ty0, -2.12f), new(.4f, ty0, -2.196f), new(0, ty0, -2.198f), new(-.4f, ty0, -2.196f), new(-.53f, ty0, -2.12f) };
            for (int i = gp.Count - 1; i >= 0; i--) gl.Add(new Vector3(-gp[i].x, gp[i].y, gp[i].z));
            Part("Glyph", Geo.Tube(gl, 200, .013f, 6), Lib.TailLed, Vector3.zero, shadows: false);
            // nose: sensor visor, three lenses, light bar
            Part("Visor", Geo.RoundedBox(.46f, .1f, .3f, 3, .045f), Lib.Graphite, new Vector3(0, .03f, 1.92f), new Vector3(.2f, 0, 0));
            foreach (var (x, r) in new[] { (-.12f, .035f), (0f, .05f), (.11f, .025f) })
            {
                var rot = new Vector3(Mathf.PI / 2 - 1.2f, 0, 0);
                Part("Lens", Geo.Cylinder(r, r, .03f, 20), Lib.LensBlack, new Vector3(x, .085f, 1.99f), rot);
                Part("LensRing", Geo.Torus(r + .006f, .004f, 6, 24), Lib.Alu, new Vector3(x, .085f, 1.99f), rot + new Vector3(Mathf.PI / 2, 0, 0));
            }
            Part("Head", Geo.Box(.34f, .016f, .02f), Lib.CarLed, new Vector3(0, -.08f, 2.2f), shadows: false);
            // tail: plate, X lights, nozzle with an iris
            Part("Plate", Geo.Circle(.27f, 40), Lib.Ink, new Vector3(0, 0, -2.185f), new Vector3(0, Mathf.PI, 0));
            foreach (var sx in new[] { -1f, 1f }) foreach (var r in new[] { .78f, -.78f }) Part("TailX", Geo.Box(.13f, .022f, .01f), Lib.TailX, new Vector3(sx * .37f, .02f, -2.12f), new Vector3(0, 0, r), shadows: false);
            Part("Nozzle", Geo.Cylinder(.2f, .23f, .2f, 40, true), Lib.Nozzle, new Vector3(0, 0, -2.27f), new Vector3(Mathf.PI / 2, 0, 0));
            Part("NozzleRing", Geo.Torus(.215f, .018f, 10, 40), Lib.Alu, new Vector3(0, 0, -2.37f));
            Part("Core", Geo.Circle(.17f, 32), Lib.Core, new Vector3(0, 0, -2.2f), new Vector3(0, Mathf.PI, 0), shadows: false);
            var petalMesh = Geo.Box(.13f, .006f, .07f);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                var piv = new GameObject("Petal").transform; piv.SetParent(S, false); piv.localPosition = new Vector3(Mathf.Cos(a) * .2f, Mathf.Sin(a) * .2f, -2.33f); piv.localRotation = Geo.EulerXYZ(new Vector3(0, 0, a));
                var hinge = new GameObject("Hinge").transform; hinge.SetParent(piv, false);
                Geo.Obj("Leaf", hinge, petalMesh, Lib.Graphite, new Vector3(-.065f, 0, 0), new Vector3(.25f, 0, 0));
                petals.Add(hinge);
            }
            var plumeMesh = Geo.Cylinder(.5f, .5f, 1, 28, true, 0, Mathf.PI * 2, 40);
            plumeA = PlumeMat(.21f, 2.3f, 0, .62f); plumeB = PlumeMat(.09f, 1.25f, 4, 1.15f);
            foreach (var pm in new[] { plumeA, plumeB }) { var go = Part("Plume", plumeMesh, pm, new Vector3(0, 0, -2.36f), shadows: false); go.GetComponent<MeshRenderer>().receiveShadows = false; }
            // wheels
            var rod = Geo.Cylinder(1, 1, 1, 8);
            var hp = new List<Vector3>(); for (int i = 0; i <= 120; i++) { float t = i / 120f, a = t * 7 * Mathf.PI * 2; hp.Add(new Vector3(Mathf.Cos(a) * .05f, t - .5f, Mathf.Sin(a) * .05f)); }
            var helix = Geo.Tube(hp, 240, .011f, 5);
            var jsX = new[] { .82f, -.82f, .82f, -.82f }; var front = new[] { true, true, false, false };
            for (int wi = 0; wi < 4; wi++)
            {
                float s = Mathf.Sign(jsX[wi]); bool f = front[wi];
                float r = f ? WV.fr : WV.rr, tw = f ? WV.fw : WV.rw, fwid = f ? WV.fenWF : WV.fenWR;
                var w = new Wheel { front = f, s = s };
                w.steer = new GameObject("Wheel").transform; w.steer.SetParent(S, false);
                w.spin = new GameObject("Spin").transform; w.spin.SetParent(w.steer, false);
                Geo.Obj("Tyre", w.spin, RotZ90(Geo.Lathe(TyreProfile(r, tw), 56)), Lib.Tyre, Vector3.zero);
                Geo.Obj("Cover", w.spin, Geo.Circle(r * .66f, 40), Lib.WheelFace, new Vector3(s * (tw / 2 - .035f), 0, 0), new Vector3(0, s * Mathf.PI / 2, 0));
                Geo.Obj("Inner", w.spin, Geo.Circle(r * .66f, 40), Lib.Graphite, new Vector3(-s * (tw / 2 - .035f), 0, 0), new Vector3(0, -s * Mathf.PI / 2, 0));
                foreach (var rz in new[] { .785f, -.785f }) Geo.Obj("X", w.spin, Geo.Box(.012f, r * .95f, .05f), Lib.WheelCap, new Vector3(s * (tw / 2 - .025f), 0, 0), new Vector3(rz, 0, 0));
                Geo.Obj("Hub", w.spin, Geo.Cylinder(.06f, .06f, .03f, 24), Lib.WheelCap, new Vector3(s * (tw / 2 - .015f), 0, 0), new Vector3(0, 0, Mathf.PI / 2));
                Geo.Obj("Fender", w.steer, RotZ90(Geo.Cylinder(WV.fenR, WV.fenR, fwid, 40, true, .32f, 2.55f)), Lib.Trim, Vector3.zero);
                var fl = new List<Vector3>(); for (int i = 0; i <= 24; i++) { float th = .32f + i / 24f * 2.55f; fl.Add(new Vector3(s * (fwid / 2 + .004f), Mathf.Sin(th) * (WV.fenR - .03f), Mathf.Cos(th) * (WV.fenR - .03f))); }
                Geo.Obj("FenderLed", w.steer, Geo.Tube(fl, 48, .01f, 5), Lib.CarLed, Vector3.zero, default, false);
                Geo.Obj("Stub", w.steer, Geo.Cylinder(.035f, .035f, .2f, 12), Lib.Alu, new Vector3(-s * (tw / 2 + .06f), 0, 0), new Vector3(0, 0, Mathf.PI / 2));
                w.la = Geo.Obj("ArmA", S, rod, Lib.Graphite, Vector3.zero).transform; w.lb = Geo.Obj("ArmB", S, rod, Lib.Graphite, Vector3.zero).transform; w.up = Geo.Obj("Link", S, rod, Lib.Graphite, Vector3.zero).transform;
                w.damp = Geo.Obj("Damper", S, rod, Lib.Alu, Vector3.zero).transform; w.sh = Geo.Obj("Shaft", S, rod, Lib.Ink, Vector3.zero).transform; w.spring = Geo.Obj("Spring", S, helix, Lib.Spring, Vector3.zero).transform;
                w.knuckle = Geo.Obj("Knuckle", S, Geo.Box(.05f, .3f, .09f), Lib.Graphite, Vector3.zero).transform;
                wheels.Add(w);
            }
        }

        GameObject Part(string n, Mesh m, Material mat, Vector3 pos, Vector3 rot = default, bool shadows = true) => Geo.Obj(n, shell, m, mat, pos, rot, shadows);

        static Mesh RotZ90(Mesh mesh)
        {   // three's geometry.rotateZ(π/2): (x, y, z) → (−y, x, z)
            var v = mesh.vertices; var n = mesh.normals;
            for (int i = 0; i < v.Length; i++) { v[i] = new Vector3(-v[i].y, v[i].x, v[i].z); n[i] = new Vector3(-n[i].y, n[i].x, n[i].z); }
            mesh.vertices = v; mesh.normals = n; mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }
        static List<Vector2> TyreProfile(float r, float w)
        {
            float ri = r * .64f, h = w / 2, k = .075f; var P = new List<Vector2> { new(ri, -h) };
            for (int i = 0; i <= 6; i++) { float a = -Mathf.PI / 2 + i / 6f * Mathf.PI / 2; P.Add(new Vector2(r - k + Mathf.Cos(a) * k, -h + k + Mathf.Sin(a) * k)); }
            for (int i = 0; i <= 6; i++) { float a = i / 6f * Mathf.PI / 2; P.Add(new Vector2(r - k + Mathf.Cos(a) * k, h - k + Mathf.Sin(a) * k)); }
            P.Add(new Vector2(ri, h)); return P;
        }
        static Material PlumeMat(float rad, float len, float n, float inten)
        {
            var m = new Material(Lib.Plume) { renderQueue = 3010 };
            m.SetFloat("_Rad", rad); m.SetFloat("_Len", len); m.SetFloat("_N", n); m.SetFloat("_I", inten); m.SetFloat("_On", 0); return m;
        }

        // ---- the superellipse loft (frozen) ----
        (Mesh, Mesh) Loft(float[][] st, int seg, float n, bool limit, out List<float[]> S)
        {
            S = new List<float[]>(); const int K = 6;
            static float Cr(float a, float b, float c, float d, float t) => .5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
            for (int i = 0; i < st.Length - 1; i++) for (int k = 0; k < (i == st.Length - 2 ? K + 1 : K); k++)
            {
                float t = k / (float)K; var a = st[Mathf.Max(0, i - 1)]; var b = st[i]; var c = st[i + 1]; var d = st[Mathf.Min(st.Length - 1, i + 2)];
                var r = new float[4]; for (int j = 0; j < 4; j++) r[j] = Cr(a[j], b[j], c[j], d[j], t);
                if (limit) r[1] = Mathf.Min(r[1], BodyLimit(r[0])); S.Add(r);
            }
            float z0 = S[0][0], z1 = S[^1][0], e = 2 / n;
            Vector3 Ring(float[] r, int j) { float th = -Mathf.PI / 2 + j / (float)seg * Mathf.PI * 2, c = Mathf.Cos(th), s = Mathf.Sin(th), m = (r[2] + r[3]) / 2, h = (r[2] - r[3]) / 2; return new Vector3(r[1] * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e), m + h * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e), r[0]); }
            var pos = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>();
            foreach (var r in S) for (int j = 0; j < seg; j++) { pos.Add(Ring(r, j)); uv.Add(new Vector2((r[0] - z0) / (z1 - z0), j / (float)seg)); }
            for (int i = 0; i < S.Count - 1; i++) for (int j = 0; j < seg; j++) { int a = i * seg + j, b = i * seg + (j + 1) % seg, c = (i + 1) * seg + j, d = (i + 1) * seg + (j + 1) % seg; idx.AddRange(new[] { a, b, c, b, d, c }); }
            var g = MakeMesh(pos, uv, idx);
            var cp = new List<Vector3>(); var cu = new List<Vector2>(); var ci = new List<int>();
            foreach (var (ri, dir) in new[] { (0, -1), (S.Count - 1, 1) })
            {
                int bse = cp.Count; var r = S[ri]; cp.Add(new Vector3(0, (r[2] + r[3]) / 2, r[0])); cu.Add(new Vector2(ri > 0 ? 1 : 0, .5f));
                for (int j = 0; j < seg; j++) { cp.Add(Ring(r, j)); cu.Add(new Vector2(ri > 0 ? 1 : 0, j / (float)seg)); }
                for (int j = 0; j < seg; j++) { int a = bse + 1 + j, b = bse + 1 + (j + 1) % seg; if (dir > 0) ci.AddRange(new[] { bse, a, b }); else ci.AddRange(new[] { bse, b, a }); }
            }
            return (g, MakeMesh(cp, cu, ci));
        }
        static Mesh MakeMesh(List<Vector3> v, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            var c = new Color[v.Count]; for (int i = 0; i < c.Length; i++) c[i] = Color.white; m.colors = c;
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds(); return m;
        }
        Vector3 Surf(float z, float th, float o)
        {
            var S = bodyS; int i = 0; while (i < S.Count - 2 && S[i + 1][0] < z) i++;
            var a = S[i]; var b = S[i + 1]; float t = Mathf.Clamp01((z - a[0]) / (b[0] - a[0]));
            float w = Mathf.Lerp(a[1], b[1], t) + o, tp = Mathf.Lerp(a[2], b[2], t), bt = Mathf.Lerp(a[3], b[3], t), e = 2 / 3.1f, c = Mathf.Cos(th), s = Mathf.Sin(th), m = (tp + bt) / 2, h = (tp - bt) / 2 + o;
            return new Vector3(w * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e), m + h * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e), z);
        }

        /// <summary>The numerical proof from v0.8: sweep full lock and travel, sample tyre + fender, compare to the clamped loft. Must stay &gt; 0.</summary>
        public float ClearanceCheck()
        {
            float worst = 9;
            foreach (bool front in new[] { true, false }) for (int si = 0; si <= 8; si++) for (int yi = 0; yi <= 6; yi++)
            {
                float a = front ? -WV.steerMax + si / 8f * 2 * WV.steerMax : 0, cy = Mathf.Lerp(WV.cyMin, WV.cyMax, yi / 6f), cx = front ? WV.fx : WV.rx, cz = front ? WV.fz : WV.rz, fw = front ? WV.fenWF : WV.fenWR;
                for (int k = 0; k < 48; k++) foreach (var u in new[] { -fw / 2, 0, fw / 2 }) foreach (var rr in new[] { front ? WV.fr : WV.rr, WV.fenR })
                {
                    float th = k / 48f * Mathf.PI * 2, lx = u, lz = Mathf.Cos(th) * rr;
                    float x = cx + lx * Mathf.Cos(a) + lz * Mathf.Sin(a), z = cz - lx * Mathf.Sin(a) + lz * Mathf.Cos(a);
                    var S = bodyS; if (z < S[0][0] || z > S[^1][0]) continue;
                    int i = 0; while (i < S.Count - 2 && S[i + 1][0] < z) i++;
                    float t = (z - S[i][0]) / (S[i + 1][0] - S[i][0]);
                    float bw = Mathf.Max(Mathf.Lerp(S[i][1], S[i + 1][1], t), Mathf.Max(S[i][1], S[i + 1][1])); worst = Mathf.Min(worst, x - bw);
                }
            }
            return worst;
        }

        public void ApplyLivery(int i)
        {
            livery = i; var L = LIVERIES[i];
            Lib.Paint.SetTexture("_BaseMap", Lib.Tex("livery" + i + "_a")); Lib.Paint.SetFloat("_Rough", L.rough);
            Lib.Trim.SetColor("_BaseColor", L.trim); Lib.Trim.SetFloat("_Rough", L.rough);
            Lib.WheelFace.SetColor("_BaseColor", L.face); Lib.WheelCap.SetColor("_BaseColor", L.cap); Lib.Tyre.SetColor("_BaseColor", L.tyre);
            var c = L.canopy; c.a = L.canopyA; canopyMat.SetColor("_BaseColor", c);
        }
        public void ApplyPlume(int i) { plume = i; var p = PLUMES[i]; foreach (var m in new[] { plumeA, plumeB }) { m.SetVector("_Core", p.core); m.SetVector("_Sheath", p.sheath); } }

        // ---- per-frame pose (carVisual port) ----
        static void Aim(Transform m, Vector3 a, Vector3 b, float rad)
        {
            var d = b - a; float L = d.magnitude; if (L < 1e-5f) return;
            m.localPosition = a + d * .5f; m.localRotation = Quaternion.FromToRotation(Vector3.up, d / L); m.localScale = new Vector3(rad, L, rad);
        }
        static void AimSpring(Transform m, Vector3 a, Vector3 b) { var d = b - a; float L = d.magnitude; if (L < 1e-5f) return; m.localPosition = a + d * .5f; m.localRotation = Quaternion.FromToRotation(Vector3.up, d / L); m.localScale = new Vector3(1, L, 1); }

        /// <param name="hubY">visual hub height per wheel (shell space, before clamping)</param>
        public void Pose(float[] hubY, float[] spin, float steer, bool boosting, bool braking, float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i]; float s = w.s, tw = w.front ? WV.fw : WV.rw;
                float cy = Mathf.Clamp(hubY[i], WV.cyMin, WV.cyMax), cx = s * (w.front ? WV.fx : WV.rx), cz = w.front ? WV.fz : WV.rz;
                float kx = s * (Mathf.Abs(cx) - (tw / 2 + .09f)), bi = s * (w.front ? .36f : .55f);
                w.steer.localPosition = new Vector3(cx, cy, cz);
                // inside the mirrored shell a +y rotation turns toward the car's left, so right steer is negative here
                w.steer.localRotation = Geo.EulerXYZ(new Vector3(0, w.front ? -Mathf.Clamp(steer, -WV.steerMax, WV.steerMax) : 0, 0));
                w.spin.localRotation = Geo.EulerXYZ(new Vector3(spin[i], 0, 0));
                var K = new Vector3(kx, cy - .1f, cz);
                Aim(w.la, new Vector3(bi, -.22f, cz + .24f), K, .022f); Aim(w.lb, new Vector3(bi, -.22f, cz - .24f), K, .022f);
                Aim(w.up, new Vector3(bi, 0, cz), new Vector3(kx, cy + .12f, cz), .018f);
                var top = new Vector3(bi - s * .02f, .12f, cz - .06f); var bot = new Vector3(Mathf.Lerp(bi, kx, .7f), Mathf.Lerp(-.22f, cy - .1f, .7f), cz);
                Aim(w.damp, top, Vector3.Lerp(top, bot, .55f), .032f); Aim(w.sh, Vector3.Lerp(top, bot, .5f), bot, .013f); AimSpring(w.spring, top, bot);
                w.knuckle.localPosition = K + new Vector3(0, .1f, 0);
            }
            // rocket: ignition eases in, the iris opens, the nozzle heats and cools slowly
            var bs = PLUMES[plume];
            heat = Mathf.Clamp01(heat + (boosting ? dt * 1.4f : -dt * .3f));
            on = Mathf.Lerp(on, boosting ? 1 : 0, 1 - Mathf.Exp(-dt * (boosting ? 12 : 8)));
            plumeA.SetFloat("_On", on); plumeB.SetFloat("_On", on);
            foreach (var h in petals) h.localRotation = Geo.EulerXYZ(new Vector3(0, -1.2f * on, 0));
            var core = (Vector3)bs.core * on * .32f + (Vector3)bs.sheath * heat * .25f + new Vector3(.06f, .06f, .065f);
            // HDR values are linear: hand SetColor their gamma form so Unity's gamma→linear conversion lands back on them
            Lib.Core.SetColor("_BaseColor", new Color(core.x, core.y, core.z).gamma); Lib.Core.SetFloat("_Intensity", 1);
            Lib.Nozzle.SetColor("_EmissionColor", (new Color(bs.sheath.x, bs.sheath.y, bs.sheath.z) * heat * .07f).gamma);
            Lib.TailLed.SetFloat("_Intensity", 3f * (braking ? 1.4f : .55f));
            Lib.TailX.SetColor("_BaseColor", (braking ? new Color(3f, .3f, .3f) : new Color(.9f, .08f, .08f)).gamma);
        }
        public float PlumeOn => on;
    }
}
