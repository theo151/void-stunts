using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidStunts
{
    /// <summary>
    /// Real-scale procedural trees, shrubs and hanging vines. Each variant is one mesh (bark + leaf-card submeshes),
    /// drawn with GPU instancing per 120 m cell; cells are frustum- and distance-culled every frame.
    /// </summary>
    public class Vegetation : MonoBehaviour
    {
        public enum Kind { Broadleaf, Tall, Shrub, Vine }
        class Variant { public Mesh mesh, lod; public Kind kind; }
        class Cell { public Bounds b; public readonly List<Matrix4x4>[] perVariant; public Cell(int n) { perVariant = new List<Matrix4x4>[n]; for (int i = 0; i < n; i++) perVariant[i] = new List<Matrix4x4>(); } }
        readonly List<Variant> variants = new();
        readonly Dictionary<long, Cell> cells = new();
        readonly Matrix4x4[] batch = new Matrix4x4[1023];
        Camera cam; readonly Plane[] planes = new Plane[6];
        public float drawDistance = 900, shadowDistance = 170, lodDistance = 230;
        System.Random rng;

        public void Init(Camera c, int seed)
        {
            cam = c; rng = new System.Random(seed);
            for (int i = 0; i < 4; i++) variants.Add(new Variant { kind = Kind.Broadleaf, mesh = BuildTree(Kind.Broadleaf, i, 10 + i * 2.2f), lod = BuildTree(Kind.Broadleaf, i, 10 + i * 2.2f, true) });
            for (int i = 0; i < 2; i++) variants.Add(new Variant { kind = Kind.Tall, mesh = BuildTree(Kind.Tall, i + 4, 15 + i * 3), lod = BuildTree(Kind.Tall, i + 4, 15 + i * 3, true) });
            for (int i = 0; i < 2; i++) variants.Add(new Variant { kind = Kind.Shrub, mesh = BuildTree(Kind.Shrub, i + 6, 2.4f + i) });
            variants.Add(new Variant { kind = Kind.Vine, mesh = BuildVine() });
        }
        public void Clear() { cells.Clear(); }
        float R() => (float)rng.NextDouble();

        /// <summary>Adds an instance. Scale ≈ 1 keeps the variant's real height; vines scale (width, length, width).</summary>
        public void Add(Kind k, Vector3 pos, float scale = 1, float yawDeg = -1, Vector3? vineScale = null)
        {
            int idx = -1, tries = 0;
            while (idx < 0 && tries++ < 20) { int i = rng.Next(variants.Count); if (variants[i].kind == k) idx = i; }
            if (idx < 0) return;
            if (yawDeg < 0) yawDeg = R() * 360;
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0, yawDeg, 0), vineScale ?? Vector3.one * scale);
            long key = ((long)Mathf.FloorToInt(pos.x / 120f) << 32) ^ (uint)Mathf.FloorToInt(pos.z / 120f);
            if (!cells.TryGetValue(key, out var cell)) { cell = new Cell(variants.Count); cell.b = new Bounds(pos, Vector3.one); cells[key] = cell; }
            cell.perVariant[idx].Add(m);
            float h = k == Kind.Vine ? 1 : 22 * scale;
            cell.b.Encapsulate(new Bounds(pos + Vector3.up * (k == Kind.Vine ? -(vineScale?.y ?? 4) / 2 : h / 2), new Vector3(16 * scale, h + 4, 16 * scale)));
        }

        void LateUpdate()
        {
            if (cam == null || variants.Count == 0) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            var cp = cam.transform.position;
            foreach (var cell in cells.Values)
            {
                float d = Mathf.Sqrt(cell.b.SqrDistance(cp));
                if (d > drawDistance || !GeometryUtility.TestPlanesAABB(planes, cell.b)) continue;
                bool shadows = d < shadowDistance;
                for (int v = 0; v < variants.Count; v++)
                {
                    var list = cell.perVariant[v]; if (list.Count == 0) continue;
                    var mesh = d > lodDistance && variants[v].lod != null ? variants[v].lod : variants[v].mesh; bool vine = variants[v].kind == Kind.Vine;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var mat = vine ? Lib.Vines : sub == 0 ? Lib.Bark : Lib.Leaves;
                        var rp = new RenderParams(mat) { worldBounds = cell.b, shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = true, layer = 0 };
                        for (int s = 0; s < list.Count; s += 1023)
                        {
                            int n = Mathf.Min(1023, list.Count - s); list.CopyTo(s, batch, 0, n);
                            Graphics.RenderMeshInstanced(rp, mesh, sub, batch, n);
                        }
                    }
                }
            }
        }

        // ---------- generators ----------
        class Build { public readonly List<Vector3> v = new(), n = new(); public readonly List<Vector4> tan = new(); public readonly List<Vector2> uv = new(); public readonly List<Color> c = new(); public readonly List<int> bark = new(), leaf = new(); }

        static void Limb(Build b, Vector3 a, Vector3 d, float len, float r0, float r1, float flex0, float flex1, int sides = 7, int segs = 5, float bend = .15f, System.Random rr = null)
        {
            var dir = d.normalized; var side = Vector3.Cross(dir, Mathf.Abs(dir.y) > .9f ? Vector3.right : Vector3.up).normalized; var up2 = Vector3.Cross(side, dir);
            int baseIdx = b.v.Count;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var c = a + dir * (len * t) + Vector3.down * (bend * len * t * t) + side * (rr != null ? ((float)rr.NextDouble() - .5f) * .1f * len * t : 0);
                float r = Mathf.Lerp(r0, r1, t);
                for (int s = 0; s <= sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2; var nrm = side * Mathf.Cos(ang) + up2 * Mathf.Sin(ang);
                    b.v.Add(c + nrm * r); b.n.Add(nrm); var tg = Vector3.Cross(dir, nrm).normalized; b.tan.Add(new Vector4(tg.x, tg.y, tg.z, 1));
                    b.uv.Add(new Vector2(s / (float)sides * Mathf.Max(1, r * 6), len * t * .6f)); b.c.Add(new Color(1, 1, 1, 1)); // bark: alpha is AO in the surface shader
                }
            }
            for (int i = 0; i < segs; i++) for (int s = 0; s < sides; s++)
            {
                int p = baseIdx + i * (sides + 1) + s, q = p + sides + 1;
                b.bark.AddRange(new[] { p, q, p + 1, p + 1, q, q + 1 });
            }
        }
        static void Cards(Build b, Vector3 c, float size, int tile, float flex, System.Random rr)
        {
            // three crossed leaf cards facing random directions; atlas tile (2×2)
            float u0 = (tile % 2) * .5f, v0 = (tile / 2) * .5f;
            for (int k = 0; k < 3; k++)
            {
                float yaw = (float)rr.NextDouble() * Mathf.PI * 2, tilt = ((float)rr.NextDouble() - .5f) * 1.2f;
                var rot = Quaternion.Euler(tilt * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, ((float)rr.NextDouble() - .5f) * 40);
                var right = rot * Vector3.right * size * .5f; var upv = rot * Vector3.up * size * .5f; var nrm = rot * Vector3.forward;
                int i0 = b.v.Count;
                Vector3[] P = { c - right - upv, c + right - upv, c + right + upv, c - right + upv }; Vector2[] U = { new(u0, 1 - v0 - .5f), new(u0 + .5f, 1 - v0 - .5f), new(u0 + .5f, 1 - v0), new(u0, 1 - v0) };
                for (int j = 0; j < 4; j++)
                {
                    b.v.Add(P[j]); b.n.Add((nrm + (P[j] - c).normalized * .6f).normalized); var tg = right.normalized; b.tan.Add(new Vector4(tg.x, tg.y, tg.z, 1));
                    b.uv.Add(U[j]); float tint = .85f + (float)rr.NextDouble() * .25f; b.c.Add(new Color(tint, tint, tint * .96f, flex));
                }
                b.leaf.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }
        }

        public static Mesh BuildTree(Kind kind, int seed, float height, bool lod = false)
        {
            var rr = new System.Random(seed * 7919 + 13); float R() => (float)rr.NextDouble();
            var b = new Build(); int tile = kind == Kind.Tall ? 2 : seed % 4 == 3 ? 3 : seed % 2;
            if (kind == Kind.Shrub)
            {
                for (int k = 0; k < 9; k++) Cards(b, new Vector3((R() - .5f) * height * .7f, height * (.25f + R() * .5f), (R() - .5f) * height * .7f), height * (.55f + R() * .3f), tile, .25f + R() * .2f, rr);
            }
            else
            {
                float trunkR = height * (kind == Kind.Tall ? .022f : .03f), trunkH = height * (kind == Kind.Tall ? .9f : .62f);
                var lean = new Vector3((R() - .5f) * .12f, 1, (R() - .5f) * .12f);
                Limb(b, Vector3.zero + Vector3.down * .3f, lean, trunkH + .3f, trunkR * 1.25f, trunkR * .55f, 0, .15f, lod ? 5 : 9, lod ? 2 : 6, .0f, rr);
                int branches = kind == Kind.Tall ? 9 : 7;
                for (int i = 0; i < branches; i++)
                {
                    float t = kind == Kind.Tall ? .3f + i / (float)branches * .65f : .45f + R() * .45f;
                    var start = lean.normalized * (trunkH * t);
                    float ang = i / (float)branches * Mathf.PI * 2 + R() * .7f, upAng = kind == Kind.Tall ? .9f + R() * .3f : .45f + R() * .45f;
                    var dir = new Vector3(Mathf.Cos(ang) * Mathf.Cos(upAng), Mathf.Sin(upAng), Mathf.Sin(ang) * Mathf.Cos(upAng));
                    float len = height * (kind == Kind.Tall ? .16f : .32f) * (1.1f - t * .4f);
                    Limb(b, start, dir, len, trunkR * .45f, trunkR * .12f, .15f, .6f, lod ? 4 : 6, lod ? 1 : 4, .18f, rr);
                    var end = start + dir * len + Vector3.down * (.18f * len);
                    int sub = 2 + rr.Next(2);
                    for (int s = 0; s < sub; s++)
                    {
                        var d2 = (dir + new Vector3(R() - .5f, R() * .6f, R() - .5f)).normalized; float l2 = len * (.45f + R() * .2f);
                        var p2 = start + dir * (len * (.5f + R() * .4f));
                        if (!lod) Limb(b, p2, d2, l2, trunkR * .16f, trunkR * .05f, .5f, .8f, 5, 3, .2f, rr);
                        Cards(b, p2 + d2 * l2, height * (kind == Kind.Tall ? .22f : .3f) * (.8f + R() * .4f), tile, .9f, rr);
                    }
                    Cards(b, end, height * (kind == Kind.Tall ? .24f : .34f), tile, .85f, rr);
                }
                // crown top
                for (int k = 0; k < (kind == Kind.Tall ? 6 : 4); k++)
                    Cards(b, lean.normalized * trunkH + new Vector3((R() - .5f) * height * .25f, height * (.02f + R() * .2f) - (kind == Kind.Tall ? height * .1f : 0), (R() - .5f) * height * .25f), height * .3f, tile, .9f, rr);
            }
            var m = new Mesh { indexFormat = IndexFormat.UInt32, subMeshCount = 2 };
            m.SetVertices(b.v); m.SetNormals(b.n); m.SetTangents(b.tan); m.SetUVs(0, b.uv); m.SetColors(b.c);
            m.SetTriangles(b.bark, 0); m.SetTriangles(b.leaf, 1); m.RecalculateBounds();
            if (b.bark.Count == 0) { m.subMeshCount = 2; }
            return m;
        }
        static Mesh BuildVine()
        {
            // a 1 m wide, 1 m long hanging strip of leaf cards, top at y = 0; scaled per instance
            var b = new Build(); var rr = new System.Random(4242);
            for (int k = 0; k < 2; k++)
            {
                var rot = Quaternion.Euler(0, k * 90, 0); var right = rot * Vector3.right * .5f; var nrm = rot * Vector3.forward;
                for (int s = 0; s < 4; s++)
                {
                    float y0 = -s * .25f, y1 = y0 - .25f; int i0 = b.v.Count;
                    Vector3[] P = { -right + Vector3.up * y1, right + Vector3.up * y1, right + Vector3.up * y0, -right + Vector3.up * y0 };
                    Vector2[] U = { new(0, 0), new(.5f, 0), new(.5f, .5f), new(0, .5f) }; // bottom-left atlas tile (deep green)
                    for (int j = 0; j < 4; j++) { b.v.Add(P[j]); b.n.Add(nrm); b.tan.Add(new Vector4(right.normalized.x, 0, right.normalized.z, 1)); b.uv.Add(U[j]); b.c.Add(new Color(1, 1, 1, Mathf.Clamp01(-P[j].y))); }
                    b.leaf.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
                }
            }
            var m = new Mesh { subMeshCount = 1 };
            m.SetVertices(b.v); m.SetNormals(b.n); m.SetTangents(b.tan); m.SetUVs(0, b.uv); m.SetColors(b.c); m.SetTriangles(b.leaf, 0); m.RecalculateBounds();
            return m;
        }
    }
}
