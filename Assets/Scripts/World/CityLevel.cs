using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// CANOPY — an overgrown city at true scale. 12 × 12 blocks on a 1.43 km grid: 22 m streets, a 38 m boulevard,
    /// 3.4 m storeys, towers to 180 m, 10–18 m street trees, a 14 m viaduct loop, a garage-to-rooftop run, a spur drop,
    /// Kowloon-style terraces, a skate plaza, a lantern market, a glass screen tower and a 44 m tree at the crossing.
    /// </summary>
    public class CityLevel : Level
    {
        public override string Id => "canopy";
        public override string Name => "CANOPY";
        public override string Blurb => "1.4 KM CITY · 4 LAYERS";
        public override (string, string)[] Goals => new[] {
            ("flip", "LAND A FLIP OR ROLL"), ("double", "LAND A DOUBLE ROTATION"), ("gap", "THE DROP · CLEAR THE SPUR"), ("land_via", "LAND ON THE VIADUCT"),
            ("land_rooftail", "FINISH THE ROOF RUN"), ("land_stacks", "TOP THE STACKS"), ("land_market", "TOP THE MARKET CANOPY"), ("rings", "FLY THROUGH 5 RINGS"), ("combo", "HIT A ×4 COMBO") };

        const int N = 12; const float BLK = 88, VT = 14, VW = 22;
        readonly float[] lo = new float[N], hi = new float[N];
        float half; LevelContext C; MeshKit K;
        Material screenMat; Texture2D screenTex; float screenT;
        static readonly string[] WORDS = { "OPEN", "24H", "HOTEL", "BAR", "DRIVE", "ROOF", "LIVE", "VOID", "NIGHT", "EXIT", "MOTEL", "AIR", "XX", "NOW", "ROOM", "FOOD", "TEA", "NOODLE", "LOFT", "GARAGE", "CLUB", "WASH", "PARK" };

        public override bool Gap(AirInfo a, Vector3 land) => a.start.y > 12.5f && Mathf.Abs(a.start.x) < 10 && a.start.z > -585 && a.start.z < -540 && land.z > -520;

        public override void Build(LevelContext c)
        {
            C = c; K = c.kit; K.chunkSize = 240;
            // grid: outer ring road 54 m (viaduct above), central boulevard 38 m, tree streets 28 m, others 22 m
            var sw = new float[N + 1]; for (int i = 0; i <= N; i++) sw[i] = 22; sw[0] = sw[N] = 54; sw[6] = 38; sw[3] = sw[9] = 28;
            float total = BLK * N; foreach (var w in sw) total += w; half = total / 2;
            float s = -half; for (int i = 0; i < N; i++) { s += sw[i]; lo[i] = s; s += BLK; hi[i] = s; }
            c.extent = half + 60;

            Ground();
            Perimeter();
            Viaduct();
            Drop();
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
            {
                if (j == 1 && i >= 1) continue;               // roof run row
                if ((i == 5 || i == 6) && (j == 5 || j == 6)) continue; // landmark quarter
                if (i == 2 && j == 8) { Stacks(i, j); continue; }
                if (c.R() < .09f) { Park(i, j); continue; }
                GenericBlock(i, j);
            }
            RoofRun();
            TreePark(5, 5); SkatePlaza(6, 5); Market(5, 6); ScreenTower(6, 6);
            Streets();
            c.spawn = new Vector3(-(half - 27 - VW / 2) + 8, 0, 150); c.spawnYaw = 180;
        }

        float Cx(int i) => (lo[i] + hi[i]) / 2;

        // ---------------- ground, perimeter ----------------
        void Ground()
        {
            float E = half + 140;
            for (float x = -E; x < E; x += 190) for (float z = -E; z < E; z += 190)
            {
                K.Region(new Vector3(x + 95, 0, z + 95));
                K.Quad(Lib.Asphalt, new Vector3(x, 0, z), new Vector3(Mathf.Min(E, x + 190), 0, z), new Vector3(Mathf.Min(E, x + 190), 0, Mathf.Min(E, z + 190)), new Vector3(x, 0, Mathf.Min(E, z + 190)), Vector3.up, 1);
            }
            K.Box(new Vector3(0, -1, 0), new Vector3(E, 1, E), Quaternion.identity, Lib.Asphalt, tag: "street", render: false);
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
                K.Box(new Vector3(Cx(i), .06f, Cx(j)), new Vector3(BLK / 2, .06f, BLK / 2), Quaternion.identity, Lib.Concrete, Lib.Concrete, tag: "sidewalk");
        }

        void Perimeter()
        {
            // a continuous ring of megastructures closes the city: 80–260 m, set back 40 m behind the viaduct
            float inner = half + 30, outer = half + 110;
            for (int side = 0; side < 4; side++)
                for (float t = -outer; t < outer; t += 0)
                {
                    float w = C.R(36, 90), h = C.R(80, 260), d = outer - inner;
                    float a = t, b = Mathf.Min(outer, t + w); t = b + C.R(0, 6);
                    Vector3 ctr; Vector3 hf;
                    if (side == 0) { ctr = new Vector3((a + b) / 2, h / 2, inner + d / 2); hf = new Vector3((b - a) / 2, h / 2, d / 2); }
                    else if (side == 1) { ctr = new Vector3((a + b) / 2, h / 2, -inner - d / 2); hf = new Vector3((b - a) / 2, h / 2, d / 2); }
                    else if (side == 2) { ctr = new Vector3(inner + d / 2, h / 2, (a + b) / 2); hf = new Vector3(d / 2, h / 2, (b - a) / 2); }
                    else { ctr = new Vector3(-inner - d / 2, h / 2, (a + b) / 2); hf = new Vector3(d / 2, h / 2, (b - a) / 2); }
                    K.Box(ctr, hf, Quaternion.identity, C.R() < .4f ? Lib.FacadeDark : Lib.Facade, Lib.Dark, tag: "wall", safe: false, facade: new FacadeParams(C.R(3, 4.6f), 3.4f, C.R(0, 99), Mathf.Floor(C.R(0, 3.99f))));
                    if (C.R() < .5f) Crown(ctr + Vector3.up * h / 2, hf);
                }
        }
        void Crown(Vector3 top, Vector3 hf)
        {
            float cw = Mathf.Min(hf.x, hf.z) * C.R(.35f, .6f);
            K.Box(top + Vector3.up * 5, new Vector3(cw, 5, cw), Quaternion.identity, Lib.Dark);
            K.Cylinder(top + Vector3.up * 10, .5f, top.y + 10 + C.R(14, 40), top.y + 10, 6, Lib.Steel, Lib.Steel);
            K.Box(top + Vector3.up * (10 + 1), new Vector3(.4f, .4f, .4f), Quaternion.identity, Lib.LedRed);
        }

        // ---------------- buildings ----------------
        void Building(float x0, float z0, float x1, float z1, float h, string tag = "roof", bool moss = false, bool props = true, bool shops = true, float signChance = .3f, System.Func<float, float, bool> keepClear = null)
        {
            float w = x1 - x0, d = z1 - z0; var ctr = new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2);
            float style = Mathf.Floor(C.R(0, 3.99f)), bay = C.R(3.0f, 4.6f), seed = C.R(0, 999);
            var mat = C.R() < .28f ? Lib.FacadeDark : Lib.Facade; var tint = Color.Lerp(Color.white, new Color(1.04f, .97f, .88f), C.R());
            float podium = 0;
            if (h > 62 && Mathf.Min(w, d) > 30 && tag == "roof")
            {   // podium + setback tower
                podium = 3.4f * Mathf.Round(C.R(3, 6)) + .12f;
                K.Box(ctr + Vector3.up * (podium / 2 + .12f), new Vector3(w / 2, podium / 2, d / 2), Quaternion.identity, mat, moss ? Lib.Moss : Lib.Concrete, tag: tag, facade: new FacadeParams(bay, 3.4f, seed, 2), tint: tint);
                float inset = C.R(5, 9);
                var tctr = ctr + Vector3.up * (podium + (h - podium) / 2 + .12f);
                K.Box(tctr, new Vector3(w / 2 - inset, (h - podium) / 2, d / 2 - inset), Quaternion.identity, mat, Lib.Concrete, tag: tag, facade: new FacadeParams(bay, 3.4f, seed + 3, style), tint: tint);
                RoofDetail(x0 + inset, z0 + inset, x1 - inset, z1 - inset, h + .12f, props, null);
                if (C.R() < .5f) Vines(x0, z0, x1, z1, podium + .12f, 8);
            }
            else
            {
                K.Box(ctr + Vector3.up * (h / 2 + .12f), new Vector3(w / 2, h / 2, d / 2), Quaternion.identity, mat, moss ? Lib.Moss : Lib.Concrete, tag: tag, facade: new FacadeParams(bay, 3.4f, seed, style), tint: tint);
                RoofDetail(x0, z0, x1, z1, h + .12f, props, keepClear);
                if (moss) for (int k = 0; k < w * d / 260; k++) { float x = C.R(x0 + 4, x1 - 4), z = C.R(z0 + 4, z1 - 4); if (keepClear == null || keepClear(x, z)) C.veg.Add(Vegetation.Kind.Shrub, new Vector3(x, h + .12f, z), C.R(.8f, 1.3f)); }
            }
            // street level: warm shopfront band, awnings, vending light, blade signs, vertical pipes
            var faces = new[] { (new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.back), (new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), Vector3.forward), (new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), Vector3.left), (new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.right) };
            foreach (var (a, b, n) in faces)
            {
                float L = Vector3.Distance(a, b); var tdir = (b - a).normalized;
                if (shops && h > 8)
                {
                    K.Strip(a + tdir * 1.2f + n * .06f + Vector3.up * 3.75f, b - tdir * 1.2f + n * .06f + Vector3.up * 3.75f, n, .3f, .05f, Lib.LedWarm);
                    if (C.R() < .5f)
                    {   // awning: a 2.4 m deep canopy tilted down and out over the pavement
                        float c0 = C.R(.05f, .55f), m = C.R(.18f, .4f); var p0 = a + tdir * (L * c0); var p1 = a + tdir * Mathf.Min(L - 1, L * (c0 + m));
                        var qa = Quaternion.LookRotation(n, Vector3.up) * Quaternion.Euler(18, 0, 0);
                        K.Box((p0 + p1) / 2 + n * 1.15f + Vector3.up * 4.25f, new Vector3(Vector3.Distance(p0, p1) / 2, .04f, 1.2f), qa, C.R() < .5f ? Lib.Pink : Lib.Dark, C.R() < .5f ? Lib.Pink : Lib.Dark);
                    }
                    if (C.R() < .35f) { var p = a + tdir * C.R(2, L - 2) + n * .55f; K.Box(p + Vector3.up * 1f, new Vector3(.5f, 1f, .45f), Quaternion.LookRotation(n), Lib.Dark, tag: "pole", safe: false); K.Box(p + n * .46f + Vector3.up * 1.15f, new Vector3(.4f, .7f, .01f), Quaternion.LookRotation(n), C.R() < .5f ? Lib.LedWarm : Lib.LedWhite); }
                }
                if (h > 16 && C.R() < signChance && L > 8) C.Sign(a + tdir * C.R(3, L - 3), n, C.R(6, Mathf.Min(24, h - 8)), WORDS[C.rng.Next(WORDS.Length)], C.rng.Next(5));
                if (C.R() < .22f) { var p = a + tdir * C.R(1, L - 1) + n * .35f; K.Cylinder(p, .18f, h, .2f, 6, Lib.Steel, null); }
                if (C.R() < .45f) for (int k = 0; k < L / 9; k++) if (C.R() < .5f) { var p = a + tdir * C.R(1, L - 1) + n * .15f; C.veg.Add(Vegetation.Kind.Vine, new Vector3(p.x, h + .1f, p.z), 1, Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg, new Vector3(C.R(1.2f, 2.4f), C.R(3, Mathf.Min(22, h * .6f)), 1.4f)); }
            }
        }
        void Vines(float x0, float z0, float x1, float z1, float y, int n)
        {
            for (int k = 0; k < n; k++)
            {
                int e = C.rng.Next(4); float t = C.R();
                var p = e == 0 ? new Vector3(Mathf.Lerp(x0, x1, t), y, z0 - .15f) : e == 1 ? new Vector3(Mathf.Lerp(x0, x1, t), y, z1 + .15f) : e == 2 ? new Vector3(x0 - .15f, y, Mathf.Lerp(z0, z1, t)) : new Vector3(x1 + .15f, y, Mathf.Lerp(z0, z1, t));
                C.veg.Add(Vegetation.Kind.Vine, p, 1, e * 90, new Vector3(C.R(1.4f, 2.6f), C.R(3, Mathf.Min(14, y * .8f)), 1.4f));
            }
        }
        void RoofDetail(float x0, float z0, float x1, float z1, float y, bool props, System.Func<float, float, bool> keep)
        {
            // parapet light line under the roof edge
            if (C.R() < .55f) { var pts = new[] { new Vector3(x0, y - .6f, z0 - .05f), new Vector3(x1, y - .6f, z0 - .05f), new Vector3(x1 + .05f, y - .6f, z1), new Vector3(x0 - .05f, y - .6f, z1) }; K.Strip(pts[0], pts[1], Vector3.back, .12f, .03f, Lib.LedWhite); K.Strip(pts[2], new Vector3(x1 + .05f, y - .6f, z0), Vector3.right, .12f, .03f, Lib.LedWhite); }
            if (!props) return;
            int n = Mathf.Clamp((int)((x1 - x0) * (z1 - z0) / 220), 2, 9);
            for (int k = 0; k < n; k++)
            {
                float x = C.R(x0 + 3, x1 - 3), z = C.R(z0 + 3, z1 - 3); if (keep != null && !keep(x, z)) continue;
                float r = C.R();
                if (r < .35f) { K.Cylinder(new Vector3(x, 0, z), 1.6f, y + 4.6f, y + 1.2f, 12, Lib.Dark, Lib.Dark, "prop", false); for (int l = 0; l < 4; l++) K.Box(new Vector3(x + (l % 2 * 2 - 1) * 1.1f, y + .6f, z + (l / 2 * 2 - 1) * 1.1f), new Vector3(.08f, .6f, .08f), Quaternion.identity, Lib.Steel); }
                else if (r < .75f) K.Box(new Vector3(x, y + .7f, z), new Vector3(1.1f, .7f, .8f), Quaternion.Euler(0, C.R(0, 90), 0), Lib.Steel, tag: "prop", safe: false);
                else if (r < .88f) { K.Cylinder(new Vector3(x, 0, z), .12f, y + C.R(8, 22), y, 5, Lib.Steel, null); }
                else { var q = Quaternion.Euler(0, C.R(0, 360), 0); K.Box(new Vector3(x, y + 6, z), new Vector3(5.5f, 3, .15f), q, Lib.Dark); K.Box(new Vector3(x, y + 6, z) + q * Vector3.forward * .16f, new Vector3(5.2f, 2.7f, .02f), q, C.R() < .5f ? Lib.LedPink : Lib.LedTeal); K.Box(new Vector3(x, y + 1.5f, z), new Vector3(.15f, 1.5f, .15f), Quaternion.identity, Lib.Steel); }
            }
        }

        void GenericBlock(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2;
            // taller toward the north-centre "downtown", lower and greener to the south
            float dc = Mathf.Sqrt((i - 6.5f) * (i - 6.5f) + (j - 8.5f) * (j - 8.5f)) / 8f;
            float hmax = Mathf.Lerp(190, 40, Mathf.Clamp01(dc)), hmin = Mathf.Lerp(40, 12, Mathf.Clamp01(dc));
            float pick = C.R();
            if (pick < .25f)
            {   // one big tower on a podium
                Building(x0, z0, x1, z1, H(hmin * 1.4f, hmax), moss: C.R() < .2f);
            }
            else if (pick < .55f)
            {   // two slabs with a 10 m alley
                bool alongX = C.R() < .5f; float m = (alongX ? (z0 + z1) : (x0 + x1)) / 2;
                if (alongX) { Building(x0, z0, x1, m - 5, H(hmin, hmax * .7f), moss: C.R() < .3f); Building(x0, m + 5, x1, z1, H(hmin, hmax * .7f), moss: C.R() < .3f); AlleyWires(new Vector3(x0, 0, m - 5), new Vector3(x0, 0, m + 5), x1 - x0, true); }
                else { Building(x0, z0, m - 5, z1, H(hmin, hmax * .7f), moss: C.R() < .3f); Building(m + 5, z0, x1, z1, H(hmin, hmax * .7f), moss: C.R() < .3f); AlleyWires(new Vector3(m - 5, 0, z0), new Vector3(m + 5, 0, z0), z1 - z0, false); }
            }
            else
            {   // four lots
                float mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
                foreach (var (a0, a1) in new[] { (x0, mx - 5), (mx + 5, x1) }) foreach (var (b0, b1) in new[] { (z0, mz - 5), (mz + 5, z1) })
                {
                    if (C.R() < .1f) { PocketPark(a0, b0, a1, b1); continue; }
                    Building(a0, b0, a1, b1, H(hmin, hmax * .55f), moss: C.R() < .35f);
                }
                AlleyWires(new Vector3(x0, 0, mz - 5), new Vector3(x0, 0, mz + 5), x1 - x0, true);
            }
        }
        float H(float a, float b) => 3.4f * Mathf.Round(Mathf.Lerp(a, b, Mathf.Pow(C.R(), 1.4f)) / 3.4f);
        void AlleyWires(Vector3 sideA, Vector3 sideB, float len, bool alongX)
        {
            var dir = alongX ? Vector3.right : Vector3.forward;
            for (float t = 4; t < len - 4; t += C.R(5, 11)) { float y = C.R(5, 14); C.Wire(sideA + dir * t + Vector3.up * y, sideB + dir * (t + C.R(-2, 2)) + Vector3.up * (y + C.R(-1, 1)), C.R(.4f, .9f), C.R() < .3f); }
        }
        void PocketPark(float x0, float z0, float x1, float z1)
        {
            K.Box(new Vector3((x0 + x1) / 2, .2f, (z0 + z1) / 2), new Vector3((x1 - x0) / 2, .08f, (z1 - z0) / 2), Quaternion.identity, Lib.Moss, Lib.Moss, tag: "park");
            for (int k = 0; k < (x1 - x0) * (z1 - z0) / 90; k++) Tree(new Vector3(C.R(x0 + 3, x1 - 3), .28f, C.R(z0 + 3, z1 - 3)), C.R(.8f, 1.15f));
            for (int k = 0; k < 6; k++) C.veg.Add(Vegetation.Kind.Shrub, new Vector3(C.R(x0 + 2, x1 - 2), .28f, C.R(z0 + 2, z1 - 2)), C.R(.8f, 1.4f));
        }
        void Park(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2;
            PocketPark(x0, z0, x1, z1);
            // a ruined pavilion overgrown with vines, and two grass kickers
            var ctr = new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2);
            for (int k = 0; k < 6; k++) { float a = k / 6f * Mathf.PI * 2; var p = ctr + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 12; K.Box(p + Vector3.up * 3.5f, new Vector3(.6f, 3.5f, .6f), Quaternion.identity, Lib.ConcreteWarm, tag: "pillar", safe: false); C.veg.Add(Vegetation.Kind.Vine, p + Vector3.up * 7, 1, a * Mathf.Rad2Deg, new Vector3(1.6f, 5, 1.6f)); }
            K.Box(ctr + Vector3.up * 7.4f, new Vector3(13, .4f, 13), Quaternion.Euler(0, 15, 0), Lib.Concrete, Lib.Moss, tag: "roof");
            K.Prism(Curve(14, 26, 6), 10, ctr + new Vector3(-30, .28f, -20), 90, Lib.Moss, Lib.ConcreteWarm);
            K.Prism(Curve(12, 22, 5), 10, ctr + new Vector3(30, .28f, 20), -90, Lib.Moss, Lib.ConcreteWarm);
        }
        void Tree(Vector3 p, float s)
        {
            C.veg.Add(C.R() < .22f ? Vegetation.Kind.Tall : Vegetation.Kind.Broadleaf, p, s);
            K.Cylinder(p, .45f * s, p.y + 4, p.y, 6, Lib.Bark, null, "tree", false, false);
        }

        // ---------------- profiles ----------------
        public static List<Vector2> Curve(float R, float deg, int n)
        {
            var o = new List<Vector2> { Vector2.zero };
            for (int i = 1; i <= n; i++) { float a = deg * i / n * Mathf.Deg2Rad; o.Add(new Vector2(R * Mathf.Sin(a), R - R * Mathf.Cos(a))); }
            return o;
        }
        public static List<Vector2> Wedge(float L, float H) => new() { new(0, 0), new(L, H), new(L + 1.6f, H - .02f) };
        public static List<Vector2> Straight(float L, float H, int n = 1) { var o = new List<Vector2>(); for (int i = 0; i <= n; i++) o.Add(new Vector2(L * i / n, H * i / n)); return o; }
        public static List<Vector2> WithDeck(List<Vector2> p, float L) { var o = new List<Vector2>(p); var e = p[^1]; o.Add(new Vector2(e.x + L, e.y)); return o; }

        /// <summary>A drivable deck from a to b (centre-line at deck height), no roll, rails on chosen sides.</summary>
        void Deck(Vector3 a, Vector3 b, float w, string tag, Material top, bool railL = true, bool railR = true, float th = 1.4f)
        {
            var dir = b - a; float L = dir.magnitude; dir /= L;
            var xA = Vector3.Cross(Vector3.up, dir).normalized; var yA = Vector3.Cross(dir, xA);
            var q = Quaternion.LookRotation(dir, yA);
            K.Box((a + b) / 2 - yA * (th / 2), new Vector3(w / 2, th / 2, L / 2), q, Lib.Concrete, top, Lib.Dark, tag);
            if (railL) Rail(a - xA * (w / 2 - .3f), b - xA * (w / 2 - .3f), yA);
            if (railR) Rail(a + xA * (w / 2 - .3f), b + xA * (w / 2 - .3f), yA);
            K.Strip(a - xA * (w / 2 + .02f) - yA * (th * .5f), b - xA * (w / 2 + .02f) - yA * (th * .5f), -xA, .14f, .03f, Lib.LedWhite);
            K.Strip(a + xA * (w / 2 + .02f) - yA * (th * .5f), b + xA * (w / 2 + .02f) - yA * (th * .5f), xA, .14f, .03f, Lib.LedWhite);
        }
        void Rail(Vector3 a, Vector3 b, Vector3 up)
        {
            var dir = (b - a); float L = dir.magnitude; var q = Quaternion.LookRotation(dir / L, up);
            K.Box((a + b) / 2 + up * .5f, new Vector3(.25f, .5f, L / 2), q, Lib.Concrete, Lib.Concrete, tag: "rail", safe: false);
            K.Strip(a + up * 1.0f, b + up * 1.0f, up, .2f, .04f, Lib.LedWhite);
        }

        // ---------------- viaduct, ramps, the drop ----------------
        void Viaduct()
        {
            float VR = half - 27, I = VR - VW / 2, O = VR + VW / 2;
            // four decks (north/south span the corners)
            Deck(new Vector3(-O, VT, VR), new Vector3(O, VT, VR), VW, "via", Lib.Asphalt, false, false, 1.8f);
            Deck(new Vector3(-O, VT, -VR), new Vector3(O, VT, -VR), VW, "via", Lib.Asphalt, false, false, 1.8f);
            Deck(new Vector3(VR, VT, -I), new Vector3(VR, VT, I), VW, "via", Lib.Asphalt, false, false, 1.8f);
            Deck(new Vector3(-VR, VT, -I), new Vector3(-VR, VT, I), VW, "via", Lib.Asphalt, false, false, 1.8f);
            Rail(new Vector3(-O, VT, O - .3f), new Vector3(O, VT, O - .3f), Vector3.up); Rail(new Vector3(-O, VT, -O + .3f), new Vector3(O, VT, -O + .3f), Vector3.up);
            Rail(new Vector3(O - .3f, VT, -O), new Vector3(O - .3f, VT, O), Vector3.up); Rail(new Vector3(-O + .3f, VT, -O), new Vector3(-O + .3f, VT, O), Vector3.up);
            // inner rails with openings for ramps (s = position along each side), the spur and the roof-run landing
            var open = new List<(float, float)>[4]; for (int k = 0; k < 4; k++) open[k] = new List<(float, float)> { (-120, -40) };
            open[1].Add((-12, 12));                    // spur (south)
            open[2].Add((-hi[1] - 30, -lo[1] + 40));  // roof-run landing (east side runs s = −z; row 1)
            for (int k = 0; k < 4; k++)
            {
                open[k].Sort((a, b) => a.Item1.CompareTo(b.Item1)); float s0 = -I;
                foreach (var (a, b) in open[k]) { if (a > s0 + 1) Rail(SideP(k, -I + .3f, s0), SideP(k, -I + .3f, a), Vector3.up); s0 = Mathf.Max(s0, b); }
                if (I > s0 + 1) Rail(SideP(k, -I + .3f, s0), SideP(k, -I + .3f, I), Vector3.up);
                // pylons, lane lights, vines, cables
                for (float s = -VR + 30; s < VR - 20; s += 46) { var p = SideP(k, -VR, s); p.y = 0; K.Cylinder(p, 1.8f, VT - 1.8f, 0, 12, Lib.Concrete, null, "pillar", false); K.Box(p + Vector3.up * (VT - 2.6f), new Vector3(VW / 2 - 1, .8f, 1.4f), Quaternion.LookRotation(SideDir(k)), Lib.Concrete); }
                for (float s = -VR + 15; s < VR - 15; s += 32) { var a = SideP(k, -VR - 10, s); var b = SideP(k, -VR + 10, s); a.y = b.y = VT + .01f; K.Strip(a, b, Vector3.up, .2f, .02f, Lib.LedWhite); }
                for (float s = -VR; s < VR; s += 6) foreach (var t in new[] { -VW / 2 - .2f, VW / 2 + .2f }) if (C.R() < .45f) { var p = SideP(k, -VR + t, s); p.y = VT - 1.8f; C.veg.Add(Vegetation.Kind.Vine, p, 1, -1, new Vector3(C.R(1.2f, 2.2f), C.R(2, 8), 1.3f)); }
                for (float s = -VR + 20; s < VR - 60; s += 46) for (int j = 0; j < 3; j++) { var a = SideP(k, -VR - VW / 2 - .3f, s); var b = SideP(k, -VR - VW / 2 - .3f, s + 46); a.y = b.y = VT - 1.5f - j * .45f; C.Wire(a, b, 2.4f + j * .5f); }
                // on-ramp beside the deck: 104 m climb then a tapering merge lane with a diagonal guide rail
                var r0 = SideP(k, -I + 8, 70); r0.y = 0; var r1 = SideP(k, -I + 8, -40); r1.y = VT;
                Deck(r0, r1, 12, "ramp", Lib.Asphalt, true, true, 1.4f);
                for (float s = 50; s > -30; s -= 24) { var p = SideP(k, -I + 8, s); float y = Mathf.Lerp(VT, 0, (s + 40) / 110f); if (y > 3) { p.y = 0; K.Cylinder(p, 1f, y - 1.4f, 0, 8, Lib.Concrete, null, "pillar", false); } }
                foreach (var (sa, sb, tw) in new[] { (-40f, -62f, 14f), (-62f, -84f, 12.5f), (-84f, -104f, 8.5f), (-104f, -116f, 4.4f) })
                { var c0 = SideP(k, -I, sb); var c1 = SideP(k, -I + tw, sa); var mid = (c0 + c1) / 2; mid.y = VT - .9f; K.Box(mid, new Vector3(Mathf.Abs(c1.x - c0.x) / 2, .9f, Mathf.Abs(c1.z - c0.z) / 2), Quaternion.identity, Lib.Concrete, Lib.Asphalt, Lib.Dark, "via"); }
                var g0 = SideP(k, -I + 13.7f, -40); var g1 = SideP(k, -I + 13.7f, -50); var g2 = SideP(k, -I - 2, -118); g0.y = g1.y = g2.y = VT;
                Rail(g0, g1, Vector3.up); Rail(g1, g2, Vector3.up);
                var pp = SideP(k, -I + 8, 84); pp.y = 0; var pd = SideP(k, -I + 8, 60) - pp; C.AddPad(pp, pd, 32);
            }
            C.AddRing(new Vector3(0, VT + 6, VR), Vector3.right, 5.5f); C.AddRing(new Vector3(VR, VT + 6, 0), Vector3.forward, 5.5f);
        }
        // side k: 0 W (x = −VR), 1 S (z = −VR), 2 E (x = +VR), 3 N (z = +VR). t: offset along the inward axis from the side line; s: along the side.
        Vector3 SideP(int k, float xLocal, float s)
        {
            float a = k * Mathf.PI / 2, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector3(xLocal * c - s * sn, 0, xLocal * sn + s * c);
        }
        Vector3 SideDir(int k) => (SideP(k, 0, 1) - SideP(k, 0, 0)).normalized;

        void Drop()
        {
            float VR = half - 27; float z0 = -VR + VW / 2 - 2, z1 = -560;
            Deck(new Vector3(0, VT, z0), new Vector3(0, VT, z1), 16, "spur", Lib.Asphalt, true, true, 1.6f);
            for (float z = z0 + 20; z > z1 + 10; z -= 30) foreach (var s in new[] { -1, 1 }) K.Cylinder(new Vector3(s * 6.5f, 0, z), 1.1f, VT - 1.6f, 0, 10, Lib.Concrete, null, "pillar", false);
            K.Prism(Curve(14, 24, 6), 14, new Vector3(0, VT, z1 - 6.5f), 0, Lib.Asphalt, Lib.Concrete, "ramp");
            C.AddPad(new Vector3(0, VT, z0 + 30), Vector3.forward, 34);
            C.AddRing(new Vector3(0, 23, -520), new Vector3(0, .2f, 1), 6); C.AddRing(new Vector3(0, 15, -470), new Vector3(0, -.2f, 1), 5.5f);
        }

        // ---------------- the roof run ----------------
        void RoofRun()
        {
            int j = 1; float zc = Cx(j), z0 = lo[j] + 2, z1 = hi[j] - 2;
            // garage spiral in block 1: NE 9 → NW 18 → SW 27 → SE 36
            float bx0 = lo[1] + 2, bx1 = hi[1] - 2, mx = (bx0 + bx1) / 2, mz = zc;
            SlabDeck(mx, mz, bx1, z1, 9, "safe"); SlabDeck(bx0, mz, mx, z1, 18, "safe"); SlabDeck(bx0, z0, mx, mz, 27, "safe"); SlabDeck(mx, z0, bx1, mz, 36, "garage", Lib.Volt);
            K.Prism(Wedge(21, 9), 12, new Vector3((mx + bx1) / 2, 0, z1 + 22.6f), 180, Lib.Asphalt, Lib.Concrete);
            K.Prism(Wedge(26, 9), 12, new Vector3(mx + 27.6f, 9, (mz + z1) / 2), -90, Lib.Asphalt, Lib.Concrete);
            K.Prism(Wedge(26, 9), 12, new Vector3((bx0 + mx) / 2, 18, mz + 27.6f), 180, Lib.Asphalt, Lib.Concrete);
            K.Prism(Wedge(26, 9), 12, new Vector3(mx - 27.6f, 27, (z0 + mz) / 2), 90, Lib.Asphalt, Lib.Concrete);
            float rz = (z0 + mz) / 2; // the run's line
            K.Prism(Curve(10, 16, 4), 14, new Vector3(bx1 - 2.8f, 36, rz), 90, Lib.Asphalt, Lib.Concrete);
            // blocks 2..11: a long roof per block split by a 10 m alley (kicker), streets crossed by skybridges, towers either side
            float h = 36;
            for (int i = 2; i < N; i++)
            {
                float x0 = lo[i] + 2, x1 = hi[i] - 2, m = (x0 + x1) / 2;
                float ha = h, hb = h; string tagB = i == N - 1 ? "rooftail" : "roof";
                bool keep(float x, float z) => Mathf.Abs(z - rz) > 11;
                Building(x0, rz - 15, m - 5, rz + 15, ha, "roof", i % 3 == 0, true, true, .4f, keep);
                Building(m + 5, rz - 15, x1, rz + 15, hb, tagB, i % 3 == 1, true, true, .4f, keep);
                K.Prism(Curve(10, 16, 4), 14, new Vector3(m - 5 - 2.8f, ha + .12f, rz), 90, Lib.Asphalt, Lib.Concrete);
                C.AddRing(new Vector3(m, ha + 6, rz), Vector3.right, 4.8f);
                // flanking towers make it a canyon in the sky
                if (rz - 19 - z0 > 12) Building(x0, z0, x1, rz - 19, H(60, 150)); if (z1 - rz - 19 > 12) Building(x0, rz + 19, x1, z1, H(60, 150));
                if (i < N - 1) { float hn = Mathf.Max(24, h - 1.7f); Deck(new Vector3(x1 - .5f, hb + .12f, rz), new Vector3(lo[i + 1] + 2.5f, hn + .12f, rz), 16, "roof", Lib.Asphalt); h = hn; }
            }
            // launch from the last roof across the ring road onto the east viaduct
            K.Prism(Curve(16, 8, 3), 14, new Vector3(hi[N - 1] - 2 - 2.4f, h + .12f, rz), 90, Lib.Asphalt, Lib.Concrete);
            C.AddRing(new Vector3(hi[N - 1] + 16, h + 4, rz), new Vector3(1, -.15f, 0), 5.5f);
        }
        void SlabDeck(float x0, float z0, float x1, float z1, float top, string tag, Material topMat = null)
        {
            var ctr = new Vector3((x0 + x1) / 2, top - .7f, (z0 + z1) / 2);
            K.Box(ctr, new Vector3((x1 - x0) / 2, .7f, (z1 - z0) / 2), Quaternion.identity, Lib.Concrete, topMat ?? Lib.Asphalt, Lib.Dark, tag);
            for (float x = x0 + 3; x <= x1 - 2; x += 13) for (float z = z0 + 3; z <= z1 - 2; z += 13) K.Cylinder(new Vector3(x, 0, z), .7f, top - 1.4f, 0, 10, Lib.Concrete, null, "pillar", false);
            K.Strip(new Vector3(x0, top - .7f, z0 - .03f), new Vector3(x1, top - .7f, z0 - .03f), Vector3.back, .14f, .03f, Lib.LedWhite);
            K.Strip(new Vector3(x1 + .03f, top - .7f, z0), new Vector3(x1 + .03f, top - .7f, z1), Vector3.right, .14f, .03f, Lib.LedWhite);
            if (C.R() < .8f) Vines(x0, z0, x1, z1, top - 1.4f, 10);
        }

        // ---------------- Kowloon terraces ----------------
        void Stacks(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2;
            Building(x0, z0, x1, z1, 6, "safe", true, false, true, .8f);
            Building(x0, z0 + 30, x1 - 30, z1, 12, "safe", false, false, false, .9f);
            Building(x0, z0 + 52, x1 - 52, z1, 18, "safe", true, false, false, 1);
            Building(x0, z0 + 70, x0 + 18, z1, 24, "stacks", false, false, false, 1);
            K.Prism(Wedge(20, 6), 12, new Vector3(x1 - 6, 0, z0 - 21.6f), 0, Lib.Asphalt, Lib.Concrete);
            K.Prism(Wedge(20, 6), 12, new Vector3(x1 - 36, 6, z0 + 30 - 21.6f), 0, Lib.Asphalt, Lib.Concrete);
            K.Prism(Wedge(20, 6), 12, new Vector3(x1 - 58, 12, z0 + 52 - 21.6f), 0, Lib.Asphalt, Lib.Concrete);
            K.Prism(Wedge(16, 6), 10, new Vector3(x0 + 9, 18, z0 + 70 - 17.6f), 0, Lib.Asphalt, Lib.Concrete);
            K.Prism(Curve(12, 26, 5), 12, new Vector3(x0 + 9, 24, z1 - 6), 0, Lib.Asphalt, Lib.Concrete);
            for (int k = 0; k < 16; k++) { float y = 5 + k * 1.3f; C.Wire(new Vector3(x0 + C.R(10, 40), y + 4, z0 + C.R(2, 30)), new Vector3(x0 + C.R(10, 60), y + 5, z1 - C.R(2, 30)), 1.1f, C.R() < .5f); }
            for (int k = 0; k < 10; k++) C.Sign(new Vector3(C.R(x0 + 4, x1 - 4), 0, z0), Vector3.back, C.R(7, 22), WORDS[C.rng.Next(WORDS.Length)], C.rng.Next(5));
            C.AddRing(new Vector3(x0 + 9, 34, z1 + 18), new Vector3(0, .1f, 1), 5);
        }

        // ---------------- the landmark quarter ----------------
        void TreePark(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2; var ctr = new Vector3(x1 - 30, 0, z1 - 30);
            PocketPark(x0, z0, x1, z1);
            K.Cylinder(ctr, 14, 2.6f, 0, 40, Lib.ConcreteWarm, Lib.Moss, "safe");
            for (int k = 0; k < 8; k++) { float a = (k + .5f) / 8 * 360; var d = Quaternion.Euler(0, a, 0) * Vector3.forward; K.Prism(Wedge(16, 2.6f), 7, ctr - d * (14 + 17.4f), a, Lib.Moss, Lib.ConcreteWarm, "ramp"); }
            var tree = Vegetation.BuildTree(Vegetation.Kind.Broadleaf, 99, 44);
            var go = new GameObject("GreatTree"); go.transform.SetParent(C.root, false); go.transform.position = ctr + Vector3.up * 2.6f;
            go.AddComponent<MeshFilter>().sharedMesh = tree; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = new[] { Lib.Bark, Lib.Leaves };
            K.Cylinder(ctr, 2.2f, 30, 2.6f, 10, Lib.Bark, null, "tree", false, false);
            for (int k = 0; k < 40; k++) { float a = C.R(0, 360), r = C.R(6, 16); C.veg.Add(Vegetation.Kind.Vine, ctr + Quaternion.Euler(0, a, 0) * Vector3.forward * r + Vector3.up * C.R(22, 30), 1, a, new Vector3(1.8f, C.R(6, 16), 1.8f)); }
            C.AddRing(ctr + new Vector3(0, 20, -24), Vector3.forward, 6);
            C.AddPad(new Vector3(0, 0, Cx(j) - 50), Vector3.forward, 30); C.AddPad(new Vector3(Cx(i) - 52, 0, 0), Vector3.right, 30);
        }
        void SkatePlaza(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2; var c = new Vector3((x0 + x1) / 2, .12f, (z0 + z1) / 2);
            K.Prism(WithDeck(Curve(9, 86, 12), 3), 60, c + new Vector3(0, 0, -16), 180, Lib.Concrete, Lib.ConcreteWarm, "qp");
            K.Prism(WithDeck(Curve(9, 86, 12), 3), 60, c + new Vector3(0, 0, 16), 0, Lib.Concrete, Lib.ConcreteWarm, "qp");
            K.Prism(Straight(12, 3.2f), 18, c + new Vector3(-36, 0, 0), 90, Lib.Concrete, Lib.ConcreteWarm); K.Prism(Straight(12, 3.2f), 18, c + new Vector3(-12, 0, 0), -90, Lib.Concrete, Lib.ConcreteWarm);
            K.Prism(Curve(12, 30, 6), 12, c + new Vector3(24, 0, -38), 0, Lib.Concrete, Lib.ConcreteWarm);
            C.AddRing(c + new Vector3(0, 14, 0), Vector3.right, 5); C.AddPad(c + new Vector3(24, 0, -52), Vector3.forward, 28);
            for (int k = 0; k < 8; k++) Tree(new Vector3(C.R(x0 + 4, x1 - 4), .12f, k % 2 == 0 ? z0 + 4 : z1 - 4), C.R(.8f, 1.1f));
        }
        void Market(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2, mx = (x0 + x1) / 2;
            SlabDeck(x0, z0, mx - 3, z1 - 40, 7, "safe", Lib.Moss); SlabDeck(mx + 3, z0, x1, z1 - 40, 7, "safe"); SlabDeck(x0, z1 - 36, x1, z1, 7, "market", Lib.Moss);
            K.Prism(Wedge(20, 7), 12, new Vector3(x0 + 18, 0, z0 - 21.6f), 0, Lib.Asphalt, Lib.Concrete);
            K.Prism(Curve(10, 20, 4), 12, new Vector3(x1 - 18, 7, z1 - 44), 0, Lib.Asphalt, Lib.Concrete);
            for (float x = x0 + 6; x < x1 - 4; x += 9) for (float z = z0 + 6; z < z1 - 40; z += 9)
            { if (C.R() < .3f) continue; K.Box(new Vector3(x, 1.25f, z), new Vector3(1.7f, 1.25f, 1.2f), Quaternion.identity, Lib.Steel, Lib.Dark, tag: "stall", safe: false); K.Strip(new Vector3(x - 1.5f, 2.45f, z - 1.22f), new Vector3(x + 1.5f, 2.45f, z - 1.22f), Vector3.back, .25f, .03f, Lib.LedWarm); }
            for (float z = z0 + 4; z < z1 - 40; z += 6) C.Wire(new Vector3(x0, 5, z), new Vector3(x1, 5, z + C.R(-1, 1)), 1, true);
        }
        void ScreenTower(int i, int j)
        {
            float x0 = lo[i] + 2, x1 = hi[i] - 2, z0 = lo[j] + 2, z1 = hi[j] - 2; var c = new Vector3(x1 - 26, 0, z1 - 26); float H2 = 172;
            K.Box(c + Vector3.up * H2 / 2, new Vector3(16, H2 / 2, 16), Quaternion.identity, Lib.Concrete, tag: "tower", safe: false, render: false);
            K.Box(c + Vector3.up * H2 / 2, new Vector3(16.05f, H2 / 2, 16.05f), Quaternion.identity, Lib.GlassClear, Lib.GlassClear);
            K.Box(c + Vector3.up * H2 / 2, new Vector3(5, H2 / 2, 5), Quaternion.identity, Lib.Dark);
            for (float y = 4; y < H2; y += 4.2f) K.Box(c + Vector3.up * y, new Vector3(15.4f, .22f, 15.4f), Quaternion.identity, Lib.Concrete);
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) K.Strip(c + new Vector3(sx * 16.1f, .2f, sz * 16.1f), c + new Vector3(sx * 16.1f, H2, sz * 16.1f), new Vector3(sx, 0, sz).normalized, .3f, .06f, Lib.LedWhite);
            // the dot display, facing the crossing
            screenTex = new Texture2D(192, 336, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            screenMat = new Material(Lib.Emissive) { name = "Screen" }; screenMat.SetTexture("_BaseMap", screenTex); screenMat.SetFloat("_Intensity", 2.4f); screenMat.SetFloat("_FarDim", 0);
            var sgo = Geo.Obj("Screen", C.root, Geo.Box(26, 46, .2f), screenMat, c + new Vector3(-16.4f, 44, 0), default, false); sgo.transform.rotation = Quaternion.Euler(0, 90, 0);
            DrawScreen(0);
            K.Prism(Curve(12, 26, 6), 12, new Vector3(x0 + 20, .12f, z0 + 10), 0, Lib.Concrete, Lib.ConcreteWarm); C.AddPad(new Vector3(x0 + 20, .12f, z0 - 4), Vector3.forward, 28);
            K.Prism(WithDeck(Curve(9, 80, 10), 3), 40, new Vector3(x1 - 4, .12f, z0 + 24), 90, Lib.Concrete, Lib.ConcreteWarm, "qp");
            C.AddRing(c + new Vector3(-30, 16, -30), new Vector3(1, 0, 1), 5.5f);
        }
        void DrawScreen(float t)
        {
            if (screenTex == null) return;
            int W = screenTex.width, Hh = screenTex.height, d = 6; var px = new Color32[W * Hh];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(6, 6, 7, 255);
            for (int y = 0; y < Hh; y += d) for (int x = 0; x < W; x += d) px[(y + 2) * W + x + 2] = new Color32(28, 29, 31, 255);
            string[] words = { "CANOPY", "DISTRICT", "VS 02", "DRIVE", "ROOFS", "XX" };
            int step = 9 * d, loop = words.Length * step, off = (int)(t * 22) % loop;
            for (int k = 0; k < words.Length * 2; k++)
            {
                string w = words[k % words.Length]; int y0 = k * step - off; if (y0 < -step || y0 > Hh) continue;
                int x0 = ((W / d - w.Length * 6) / 2) * d; Color32 col = k % 3 == 1 ? new Color32(212, 255, 0, 255) : new Color32(244, 241, 234, 255);
                for (int ci = 0; ci < w.Length; ci++) for (int r = 0; r < 7; r++) for (int cc = 0; cc < 5; cc++)
                {
                    if (!DotMatrix.Lit(w[ci], r, cc)) continue;
                    int px0 = x0 + (ci * 6 + cc) * d + 1, py0 = Hh - 1 - (y0 + r * d + d) ;
                    for (int yy = 0; yy < 4; yy++) for (int xx = 0; xx < 4; xx++) { int X = px0 + xx, Y = py0 + yy; if (X >= 0 && X < W && Y >= 0 && Y < Hh) px[Y * W + X] = col; }
                }
            }
            screenTex.SetPixels32(px); screenTex.Apply(false);
        }
        public override void Tick(float dt) { screenT += dt; if (screenTex != null && (int)(screenT * 7) != (int)((screenT - dt) * 7)) DrawScreen(screenT); }

        // ---------------- streets: trees, lights, markings, wires ----------------
        void Streets()
        {
            float E = half - 54;
            // centre dashes and lane edges on every street (x and z)
            var paint = Lib.Surf(null, 1, new Color(.86f, .85f, .8f), .55f, 0, false, 0); paint.name = "Paint";
            var paintY = Lib.Surf(null, 1, new Color(.9f, .72f, .3f), .55f, 0, false, 0); paintY.name = "PaintY";
            for (int g = 0; g <= N; g++)
            {
                float a = g == 0 ? -half + 27 : g == N ? half - 27 : (hi[g - 1] + lo[g]) / 2;
                if (g == 0 || g == N) continue; // ring road is under the viaduct
                for (float t = -E; t < E; t += 9)
                {
                    bool atCross = false; for (int i = 0; i < N; i++) if (t > hi[i] && (i + 1 >= N || t < lo[i + 1])) atCross = true;
                    if (atCross) continue;
                    K.Box(new Vector3(a, .015f, t + 2.5f), new Vector3(.12f, .015f, 2.5f), Quaternion.identity, paintY, paintY, render: true);
                    K.Box(new Vector3(t + 2.5f, .015f, a), new Vector3(2.5f, .015f, .12f), Quaternion.identity, paintY, paintY, render: true);
                }
            }
            // street lights every 34 m along both sides of every block, trees on the tree streets and the boulevard
            for (int i = 0; i < N; i++) for (int j = 0; j < N; j++)
            {
                float x0 = lo[i], x1 = hi[i], z0 = lo[j], z1 = hi[j];
                bool treeX = j == 2 || j == 3 || j == 5 || j == 6 || j == 8 || j == 9, treeZ = i == 2 || i == 3 || i == 5 || i == 6 || i == 8 || i == 9;
                for (float t = x0 + 8; t < x1 - 4; t += 34) { Lamp(new Vector3(t, .12f, z0 + .9f), Vector3.back); Lamp(new Vector3(t, .12f, z1 - .9f), Vector3.forward); }
                for (float t = z0 + 8; t < z1 - 4; t += 34) { Lamp(new Vector3(x0 + .9f, .12f, t), Vector3.left); Lamp(new Vector3(x1 - .9f, .12f, t), Vector3.right); }
                if (treeX) for (float t = x0 + 12; t < x1 - 6; t += 17) { Tree(new Vector3(t, .12f, z0 + 2.6f), C.R(.85f, 1.15f)); Tree(new Vector3(t + 6, .12f, z1 - 2.6f), C.R(.85f, 1.15f)); }
                if (treeZ) for (float t = z0 + 12; t < z1 - 6; t += 17) { Tree(new Vector3(x0 + 2.6f, .12f, t), C.R(.85f, 1.15f)); Tree(new Vector3(x1 - 2.6f, .12f, t + 6), C.R(.85f, 1.15f)); }
            }
            // wires across streets with lanterns (Waneella's sky of cables)
            for (int g = 1; g < N; g++)
            {
                float a0 = hi[g - 1], a1 = lo[g];
                for (int k = 0; k < N; k++) for (float t = lo[k] + 6; t < hi[k] - 6; t += C.R(14, 26))
                {
                    if (C.R() < .5f) C.Wire(new Vector3(a0, C.R(7, 13), t), new Vector3(a1, C.R(7, 13), t + C.R(-3, 3)), C.R(.8f, 1.6f), C.R() < .35f);
                    if (C.R() < .5f) C.Wire(new Vector3(t, C.R(7, 13), a0), new Vector3(t + C.R(-3, 3), C.R(7, 13), a1), C.R(.8f, 1.6f), C.R() < .35f);
                }
            }
        }
        void Lamp(Vector3 p, Vector3 outward)
        {
            K.Cylinder(p, .11f, p.y + 8.6f, p.y, 6, Lib.Dark, null, "pole", false);
            var arm = p + Vector3.up * 8.6f - outward * 1.4f;
            K.Box((p + Vector3.up * 8.6f + arm) / 2, new Vector3(.06f, .06f, .7f), Quaternion.LookRotation(-outward), Lib.Dark);
            K.Box(arm - Vector3.up * .08f, new Vector3(.24f, .05f, .55f), Quaternion.LookRotation(-outward), Lib.LedWarm);
        }
    }
}
