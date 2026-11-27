using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidStunts
{
    /// <summary>
    /// MESA — 2.6 km of open desert: a terraced canyon network, a 1.2 km salt-flat airstrip, a dam with a spillway jump,
    /// a desert motel and fuel stop, a radio dish, power lines, natural dune bowls and a fallen megastructure.
    /// Heightfield is 4 m resolution in 256 m chunks with mesh colliders.
    /// </summary>
    public class CanyonLevel : Level
    {
        public override string Id => "mesa";
        public override string Name => "MESA";
        public override string Blurb => "2.6 KM DESERT · CANYONS";
        public override int Look => 0;
        public override (string, string)[] Goals => new[] {
            ("flip", "LAND A FLIP OR ROLL"), ("air3", "3 SECONDS OF AIR"), ("gap", "SPILLWAY · CLEAR THE GORGE"), ("land_mesa", "LAND ON TOP OF A MESA"),
            ("land_dish", "LAND IN THE DISH"), ("land_wreck", "RIDE THE FALLEN TOWER"), ("rings", "FLY THROUGH 5 RINGS"), ("combo", "HIT A ×4 COMBO") };

        const float E = 1280, CELL = 5; const int CH = 64;
        LevelContext C; MeshKit K;
        readonly List<(Vector2 c, float r, float h)> mesas = new();
        Vector2 dam = new(380, 260); // spillway crest
        float damY;

        public override bool Gap(AirInfo a, Vector3 land) => Mathf.Abs(a.start.x - dam.x) < 20 && a.start.z < dam.y - 110 && a.start.z > dam.y - 145 && land.z < dam.y - 200;
        public override string LandTag(string tag, Vector3 p)
        {
            if (tag == "ground") foreach (var m in mesas) if (p.y > m.h - 3 && (new Vector2(p.x, p.z) - m.c).magnitude < m.r) return "land_mesa";
            return tag;
        }

        // ---------------- height ----------------
        static float Hash(float x, float z) { float h = Mathf.Sin(x * 127.1f + z * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
        static float Noise(float x, float z)
        {
            float ix = Mathf.Floor(x), iz = Mathf.Floor(z), fx = x - ix, fz = z - iz;
            fx = fx * fx * (3 - 2 * fx); fz = fz * fz * (3 - 2 * fz);
            float a = Hash(ix, iz), b = Hash(ix + 1, iz), c = Hash(ix, iz + 1), d = Hash(ix + 1, iz + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }
        static float Fbm(float x, float z, int o) { float s = 0, a = .5f, f = 1; for (int i = 0; i < o; i++) { s += a * Noise(x * f, z * f); f *= 2.03f; a *= .5f; } return s; }

        public float Height(float x, float z)
        {
            // canyon plateau: ridged warped noise, terraced into strata; floors are drivable sand
            float wx = x + 140 * (Fbm(x * .0016f + 3, z * .0016f, 3) - .5f), wz = z + 140 * (Fbm(x * .0016f, z * .0016f + 7, 3) - .5f);
            float n = Fbm(wx * .0019f, wz * .0019f, 5);
            float plateau = Mathf.Clamp01((n - .5f) * 5.5f);                  // 0 = canyon floor, 1 = plateau top
            float terr = Mathf.Floor(plateau * 4) / 4, fr = plateau * 4 - Mathf.Floor(plateau * 4);
            float strata = terr + Mathf.SmoothStep(0, 1, Mathf.Clamp01((fr - .55f) / .45f)) / 4;
            float h = strata * 64;
            // dunes on the floor
            h += (1 - plateau) * (Fbm(x * .011f, z * .006f, 3) * 6 + Mathf.Sin(x * .021f + Fbm(x * .003f, z * .003f, 2) * 6) * 1.4f);
            // the dam gorge: two ridges that rise from the north desert to crest height, 60 m apart
            float gxd = Mathf.Abs(x - dam.x), gzd = z - dam.y;
            if (gxd < 160 && gzd > -270 && gzd < 230)
            {
                float wall = Mathf.SmoothStep(0, 1, (gxd - 30) / 16), along = Mathf.SmoothStep(0, 1, (210 - gzd) / 170) * Mathf.SmoothStep(0, 1, (gzd + 270) / 40), gEdge = Mathf.SmoothStep(0, 1, (160 - gxd) / 30);
                h = Mathf.Max(h, damY * wall * along * gEdge);
            }
            // fade the outer rim into a high cliff wall
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)); h = Mathf.Max(h, Mathf.SmoothStep(0, 1, (edge - (E - 160)) / 140) * 120);
            // mesas
            foreach (var m in mesas) { float d = (new Vector2(x, z) - m.c).magnitude; float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((m.r + 14 - d) / 14)); h = Mathf.Lerp(h, m.h, k); }
            // flatten: airstrip, spawn, motel, dish, dune bowls
            h = Flat(h, x, z, new Rect(-640, -720, 80, 1240), 0, 40);
            h = Flat(h, x, z, new Rect(-560, -760, 220, 140), 0, 50);
            h = Flat(h, x, z, new Rect(420, -720, 260, 220), 0, 50);
            foreach (var b in Bowls) { float d = (new Vector2(x, z) - new Vector2(b.x, b.y)).magnitude, tt = d / b.z; if (tt > 1.2f) continue; float t = Mathf.Clamp01(tt); float bowl = -b.z * .22f * Mathf.Cos(t * Mathf.PI * .5f) + (t > .8f ? (t - .8f) * b.z * .4f : 0); h = Mathf.Lerp(bowl, h, Mathf.SmoothStep(0, 1, (tt - .9f) / .3f)); }
            return h;
        }
        static readonly Vector3[] Bowls = { new(-80, -300, 70), new(180, 540, 90), new(-420, 420, 60) };
        static float Flat(float h, float x, float z, Rect r, float y, float blend)
        {
            float dx = Mathf.Max(r.xMin - x, 0, x - r.xMax), dz = Mathf.Max(r.yMin - z, 0, z - r.yMax), d = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Lerp(y, h, Mathf.SmoothStep(0, 1, d / blend));
        }

        public override void Build(LevelContext c)
        {
            C = c; K = c.kit; K.chunkSize = 256; c.extent = E - 60;
            mesas.Add((new Vector2(-150, 120), 46, 92)); mesas.Add((new Vector2(260, -180), 34, 70)); mesas.Add((new Vector2(-300, -520), 28, 58)); mesas.Add((new Vector2(560, 620), 52, 104));
            damY = 38;
            Terrain();
            Airstrip(); Motel(new Vector3(-450, 0, -690)); Dish(new Vector3(550, 0, -610)); Dam(); Wreck(); PowerLine(); Scatter();
            c.spawn = new Vector3(-600, 0, -690); c.spawnYaw = 0;
        }

        void Terrain()
        {
            int n = Mathf.RoundToInt(2 * E / (CH * CELL));
            for (int cx = 0; cx < n; cx++) for (int cz = 0; cz < n; cz++)
            {
                float x0 = -E + cx * CH * CELL, z0 = -E + cz * CH * CELL; int V = CH + 1;
                var v = new Vector3[V * V]; var nr = new Vector3[V * V]; var uv = new Vector2[V * V]; var col = new Color[V * V];
                // heights with a one-cell border so normals are seamless across chunks
                var hg = new float[(V + 2) * (V + 2)];
                for (int i = 0; i < V + 2; i++) for (int j = 0; j < V + 2; j++) hg[j * (V + 2) + i] = Height(x0 + (i - 1) * CELL, z0 + (j - 1) * CELL);
                float Hg(int i, int j) => hg[(j + 1) * (V + 2) + i + 1];
                for (int i = 0; i < V; i++) for (int j = 0; j < V; j++)
                {
                    float x = x0 + i * CELL, z = z0 + j * CELL, y = Hg(i, j);
                    v[j * V + i] = new Vector3(x, y, z); uv[j * V + i] = new Vector2(x, z);
                    float hx = Hg(i + 1, j) - Hg(i - 1, j), hz = Hg(i, j + 1) - Hg(i, j - 1);
                    nr[j * V + i] = new Vector3(-hx, 2 * CELL, -hz).normalized; col[j * V + i] = new Color(1, 1, 1, Mathf.Lerp(.7f, 1, Mathf.Clamp01(y / 30 + .5f)));
                }
                var t = new int[CH * CH * 6]; int k = 0;
                for (int i = 0; i < CH; i++) for (int j = 0; j < CH; j++) { int a = j * V + i; t[k++] = a; t[k++] = a + V; t[k++] = a + 1; t[k++] = a + 1; t[k++] = a + V; t[k++] = a + V + 1; }
                var m = new Mesh { indexFormat = IndexFormat.UInt32, vertices = v, normals = nr, uv = uv, colors = col, triangles = t };
                m.SetUVs(1, new List<Vector2>(new Vector2[V * V])); m.SetUVs(2, new List<Vector4>(new Vector4[V * V]));
                m.RecalculateTangents(); m.RecalculateBounds();
                var go = new GameObject("Terrain"); go.transform.SetParent(C.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Lib.Terrain;
                var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = m; go.AddComponent<SurfaceTag>().tagName = "ground";
            }
        }

        Vector3 G(float x, float z, float up = 0) => new(x, Height(x, z) + up, z);

        void Airstrip()
        {
            var paint = Lib.Surf(null, 1, new Color(.9f, .89f, .85f), .5f, 0, false, 0); paint.name = "Paint";
            K.Box(new Vector3(-600, .05f, -100), new Vector3(30, .05f, 600), Quaternion.identity, Lib.Asphalt, Lib.Asphalt, tag: "strip");
            for (float z = -680; z < 480; z += 30) K.Box(new Vector3(-600, .11f, z), new Vector3(.4f, .01f, 8), Quaternion.identity, paint);
            for (int s = -1; s <= 1; s += 2) for (float z = -690; z < 500; z += 24) { K.Box(new Vector3(-600 + s * 31, .3f, z), new Vector3(.25f, .3f, .25f), Quaternion.identity, Lib.Dark); K.Box(new Vector3(-600 + s * 31, .65f, z), new Vector3(.18f, .06f, .18f), Quaternion.identity, Lib.LedWhite); }
            // big end-of-runway kicker straight into the canyon, and a mid-strip boost line
            K.Prism(CityLevel.Curve(40, 26, 10), 24, new Vector3(-600, 0, 480), 0, Lib.Asphalt, Lib.ConcreteWarm, "ramp");
            for (int k = 0; k < 3; k++) C.AddPad(new Vector3(-600, .1f, -200 + k * 160), Vector3.forward, 44);
            C.AddRing(new Vector3(-600, 40, 600), new Vector3(0, .1f, 1), 7); C.AddRing(new Vector3(-600, 26, 720), new Vector3(0, -.3f, 1), 6.5f);
            // control tower
            var tp = new Vector3(-540, 0, -560);
            K.Cylinder(tp, 3, 26, 0, 16, Lib.Concrete, Lib.Concrete, "wall", false);
            K.Cylinder(tp + Vector3.up * 26, 7, 31, 26, 16, Lib.GlassSmoke, Lib.Dark, "wall", false);
            K.Cylinder(tp, 7.4f, 32, 31, 16, Lib.Concrete, Lib.Concrete);
            K.Strip(tp + new Vector3(-7.45f, 31.5f, 0), tp + new Vector3(7.45f, 31.5f, 0), Vector3.up, .1f, .02f, Lib.LedRed);
            C.Sign(tp + new Vector3(0, 0, -3), Vector3.back, 14, "MESA", 2, false);
        }

        void Motel(Vector3 o)
        {
            // a long low motel and a fuel canopy, warm light, dot-matrix sign on a pole
            K.Box(o + new Vector3(0, 2.2f, 40), new Vector3(70, 2.2f, 7), Quaternion.identity, Lib.ConcreteWarm, Lib.Concrete, tag: "roof", facade: new FacadeParams(4.2f, 3.4f, 7, 1));
            for (float x = -66; x < 66; x += 6) { K.Box(o + new Vector3(x, 1.2f, 32.9f), new Vector3(.9f, 1.1f, .05f), Quaternion.identity, Lib.Dark); K.Box(o + new Vector3(x + 2, 1.6f, 32.92f), new Vector3(.8f, .5f, .02f), Quaternion.identity, Lib.LedWarm); }
            K.Strip(o + new Vector3(-70, 4.3f, 32.95f), o + new Vector3(70, 4.3f, 32.95f), Vector3.back, .2f, .04f, Lib.LedPink);
            K.Box(o + new Vector3(60, 5.4f, -8), new Vector3(16, .4f, 10), Quaternion.identity, Lib.Trim, Lib.Concrete, tag: "roof");
            K.Strip(o + new Vector3(44, 5f, -18.05f), o + new Vector3(76, 5f, -18.05f), Vector3.back, .3f, .05f, Lib.LedWhite);
            foreach (var x in new[] { 50f, 70f }) { K.Box(o + new Vector3(x, 2.5f, -8), new Vector3(.4f, 2.5f, .4f), Quaternion.identity, Lib.Trim, tag: "pole", safe: false); K.Box(o + new Vector3(x, .9f, -4), new Vector3(.6f, .9f, .4f), Quaternion.identity, Lib.Volt, tag: "pole", safe: false); }
            K.Cylinder(o + new Vector3(-80, 0, 10), .4f, 18, 0, 8, Lib.Steel, null, "pole", false);
            C.Sign(o + new Vector3(-80, 0, 10), Vector3.back, 15, "MOTEL", 1, false); C.Sign(o + new Vector3(-80, 0, 10.2f), Vector3.forward, 15, "VACANCY", 3, false);
            K.Prism(CityLevel.Wedge(16, 4.4f), 10, o + new Vector3(0, 0, 14), 0, Lib.Asphalt, Lib.ConcreteWarm, "ramp");
            K.Prism(CityLevel.Wedge(12, 5.6f), 10, o + new Vector3(60, 0, -36), 0, Lib.Asphalt, Lib.ConcreteWarm, "ramp");
        }

        void Dish(Vector3 o)
        {
            // a 70 m radio dish laid as a drivable bowl, on a ring plinth, with a feed mast
            float R = 36, D = 14; var top = new List<Vector2>();
            int n = 14; for (int i = 0; i <= n; i++) { float r = R * i / n; top.Add(new Vector2(r, D * (r * r) / (R * R))); }
            for (int k = 0; k < 24; k++) { float a = k * 15; K.Prism(Shift(top, 2), 2 * R * Mathf.PI / 24 * 1.02f, o + Vector3.up * 4, a, Lib.Trim, Lib.Steel, "land_dish", true, true); }
            K.Cylinder(o, R + .6f, 4, 0, 32, Lib.Concrete, Lib.Concrete, "safe");
            for (int k = 0; k < 4; k++) { float a = k * 90 + 45; var d = Quaternion.Euler(0, a, 0) * Vector3.forward; K.Strip(o + d * 4 + Vector3.up * 4, o + Vector3.up * 34, Vector3.up, .4f, .4f, Lib.Steel); }
            K.Box(o + Vector3.up * 34, new Vector3(1.6f, 1.6f, 1.6f), Quaternion.identity, Lib.Dark); K.Box(o + Vector3.up * 36, new Vector3(.4f, .4f, .4f), Quaternion.identity, Lib.LedRed);
            K.Prism(CityLevel.Wedge(14, 4), 12, o + new Vector3(0, 0, -R - 16), 0, Lib.Asphalt, Lib.Concrete, "ramp");
            C.AddRing(o + new Vector3(0, 26, 0), Vector3.forward, 6);
        }
        static List<Vector2> Shift(List<Vector2> p, float dx) { var o = new List<Vector2>(); foreach (var v in p) o.Add(new Vector2(v.x + dx, v.y)); return o; }

        void Dam()
        {
            // a curved concrete gravity dam across a gorge; the spillway is a 120 m chute that launches over the gorge
            var c = new Vector3(dam.x, 0, dam.y);
            for (int k = -6; k <= 6; k++)
            {
                float a = k * 6; var d = Quaternion.Euler(0, a, 0);
                var p = c + d * new Vector3(0, 0, -80) + Vector3.forward * 80;
                K.Box(p + Vector3.up * (damY + 6) / 2 + Vector3.down * 6, new Vector3(4.7f, (damY + 6) / 2, 9), d, Lib.Concrete, Lib.ConcreteWarm, tag: "safe", facade: new FacadeParams(9.4f, 6, k, 3));
                K.Box(p + d * new Vector3(0, damY + .6f, -8.6f), new Vector3(4.7f, .6f, .3f), d, Lib.Concrete, Lib.Concrete, tag: Mathf.Abs(k) <= 1 ? null : "rail", safe: false); // parapet, open over the spillway
            }
            var top = new List<Vector2>(); for (int i = 0; i <= 16; i++) { float t = i / 16f; top.Add(new Vector2(t * 130, (damY - 2) * (1 - t) * (1 - t) + 2 + (t > .85f ? (t - .85f) * 40 : 0))); }
            var ch = new List<Vector2>(); for (int i = top.Count - 1; i >= 0; i--) ch.Add(new Vector2(130 - top[i].x, top[i].y));
            K.Prism(ch, 14, c + new Vector3(0, 0, -130), 0, Lib.Concrete, Lib.ConcreteWarm, "ramp");
            C.AddPad(c + new Vector3(0, damY, -8), Vector3.back, 30);
            C.AddRing(c + new Vector3(0, 18, -175), Vector3.back, 7);
            // the ridges either side rise gently from the north: drive up either one and across the crest
        }

        void Wreck()
        {
            // a 260 m tower lying broken across a canyon, tilted: a drivable rooftop line with a gap at the break
            var a = G(-260, 320, 0); var b = new Vector3(-60, 0, 420); b.y = Height(b.x, b.z);
            var mid = Vector3.Lerp(a, b, .55f);
            Section(a + Vector3.up * 2, mid + Vector3.up * 26); Section(mid + Vector3.up * 22 + (b - a).normalized * 18, b + Vector3.up * 4);
            C.AddRing(mid + Vector3.up * 34, (b - a).normalized, 6);
        }
        void Section(Vector3 a, Vector3 b)
        {
            var dir = b - a; float L = dir.magnitude; dir /= L; var q = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0, 0, 4);
            K.Box((a + b) / 2, new Vector3(16, 9, L / 2), q, Lib.Facade, Lib.Concrete, Lib.Dark, "land_wreck", true, new FacadeParams(3.6f, 3.4f, 42, 2));
            for (int k = 0; k < L / 7; k++) C.veg.Add(Vegetation.Kind.Vine, a + dir * (k * 7 + 3) + q * new Vector3(16.2f, 8, 0), 1, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 90, new Vector3(1.8f, C.R(4, 14), 1.4f));
            K.Strip(a + q * new Vector3(-16.1f, 9, 0), b + q * new Vector3(-16.1f, 9, 0), q * Vector3.left, .15f, .03f, Lib.LedWhite);
        }

        void PowerLine()
        {
            Vector3 prev = default; bool has = false;
            for (float t = 0; t <= 1.001f; t += 1 / 14f)
            {
                var p = G(Mathf.Lerp(-1100, 1100, t), Mathf.Lerp(-900, 900, t) + Mathf.Sin(t * 7) * 120);
                K.Strip(p, p + Vector3.up * 34, Vector3.forward, .5f, .5f, Lib.Steel);
                K.Box(p + Vector3.up * 32, new Vector3(9, .4f, .4f), Quaternion.LookRotation(new Vector3(1, 0, 1)), Lib.Steel);
                K.Box(p + Vector3.up * 35, new Vector3(.3f, .3f, .3f), Quaternion.identity, Lib.LedRed);
                if (has) for (int s = -1; s <= 1; s++) { var o = Quaternion.LookRotation(new Vector3(1, 0, 1)) * Vector3.right * (s * 8); C.Wire(prev + Vector3.up * 31.5f + o, p + Vector3.up * 31.5f + o, 6); }
                prev = p; has = true;
            }
        }

        void Scatter()
        {
            // rock spires, kickers on dunes, scrub, cacti-like shrubs, a few dot-matrix road signs
            for (int k = 0; k < 140; k++)
            {
                float x = C.R(-E + 180, E - 180), z = C.R(-E + 180, E - 180); var p = G(x, z);
                if (Mathf.Abs(x + 600) < 70) continue;
                float r = C.R();
                if (r < .25f) { float h = C.R(10, 30); K.Cylinder(p + Vector3.down * 2, C.R(2, 5), p.y + h, p.y - 2, 7, Lib.Terrain, Lib.Terrain, "wall", false); }
                else if (r < .5f && p.y < 8) K.Prism(CityLevel.Curve(C.R(10, 18), C.R(16, 26), 5), C.R(8, 14), p + Vector3.down * .3f, C.R(0, 360), Lib.Terrain, Lib.Terrain, "ramp");
                else C.veg.Add(Vegetation.Kind.Shrub, p, C.R(.5f, 1.1f));
            }
            for (int k = 0; k < 30; k++) { var p = G(C.R(-E + 200, E - 200), C.R(-E + 200, E - 200)); if (p.y < 20) C.veg.Add(Vegetation.Kind.Tall, p, C.R(.6f, .9f)); }
            foreach (var b in Bowls) { C.AddRing(new Vector3(b.x, Height(b.x, b.y) + b.z * .35f, b.y), Vector3.right, 5.5f); }
            foreach (var m in mesas) C.AddRing(new Vector3(m.c.x, m.h + 14, m.c.y - m.r - 30), Vector3.forward, 6);
        }
    }
}
