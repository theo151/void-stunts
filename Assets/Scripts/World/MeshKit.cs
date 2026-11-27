using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidStunts
{
    /// <summary>Marks a collider with a gameplay tag (landing goals) and whether it is a safe respawn surface.</summary>
    public class SurfaceTag : MonoBehaviour { public string tagName = "safe"; public bool safe = true; }

    /// <summary>Facade parameters for the procedural window shader: bay width, storey height, seed, style (0 punched, 1 ribbon, 2 curtain wall, 3 slits).</summary>
    public struct FacadeParams { public float bay, storey, seed, style; public FacadeParams(float b, float s, float seed, float st) { bay = b; storey = s; this.seed = seed; style = st; } }

    /// <summary>
    /// Procedural geometry builder. Primitives are merged into one mesh per (material, 200 m chunk) so a whole city is
    /// a few hundred SRP-batched draws with frustum culling per chunk. Colliders are real Unity colliders.
    /// </summary>
    public class MeshKit
    {
        class Buf
        {
            public readonly List<Vector3> v = new(); public readonly List<Vector3> n = new(); public readonly List<Vector2> uv0 = new(), uv1 = new();
            public readonly List<Vector4> uv2 = new(); public readonly List<Color> c = new(); public readonly List<int> t = new();
        }
        readonly Dictionary<(Material, int), Buf> bufs = new();
        public readonly Transform root, colliders;
        public float chunkSize = 200f;
        int chunk;
        public PhysicsMaterial worldPhys;
        // when set, primitives also write their triangles here (for mesh colliders of curved ramps)
        List<Vector3> capV; List<int> capT;

        public MeshKit(Transform root)
        {
            this.root = root;
            colliders = new GameObject("Colliders").transform; colliders.SetParent(root, false);
            worldPhys = new PhysicsMaterial("World") { dynamicFriction = .6f, staticFriction = .6f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Average };
        }

        public void Region(Vector3 p) { chunk = (Mathf.FloorToInt(p.x / chunkSize) + 64) * 1000 + (Mathf.FloorToInt(p.z / chunkSize) + 64); }

        Buf B(Material m) { var k = (m, chunk); if (!bufs.TryGetValue(k, out var b)) { b = new Buf(); bufs[k] = b; } return b; }

        static Vector2 WorldUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
            if (ax >= az) return new Vector2(p.z, p.y);
            return new Vector2(p.x, p.y);
        }
        static float GroundAO(float y) => Mathf.Lerp(.62f, 1f, Mathf.Clamp01((y + .2f) / 2.6f));

        /// <summary>A triangle; if <paramref name="want"/> is given the winding is flipped so the normal agrees with it.</summary>
        public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3? want = null, float ao = -1, Vector2? f0 = null, Vector2? f1 = null, Vector2? f2 = null, FacadeParams? fp = null, Color? tint = null)
        {
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) return; n.Normalize();
            if (want.HasValue && Vector3.Dot(n, want.Value) < 0) { (b, c) = (c, b); (f1, f2) = (f2, f1); n = -n; }
            var buf = B(m); int i0 = buf.v.Count;
            var col = tint ?? Color.white;
            Vector3[] P = { a, b, c }; Vector2?[] F = { f0, f1, f2 };
            for (int k = 0; k < 3; k++)
            {
                buf.v.Add(P[k]); buf.n.Add(n); buf.uv0.Add(WorldUV(P[k], n));
                buf.uv1.Add(F[k] ?? Vector2.zero);
                buf.uv2.Add(fp.HasValue ? new Vector4(fp.Value.bay, fp.Value.storey, fp.Value.seed, fp.Value.style) : Vector4.zero);
                col.a = ao >= 0 ? ao : GroundAO(P[k].y); buf.c.Add(col);
            }
            buf.t.Add(i0); buf.t.Add(i0 + 1); buf.t.Add(i0 + 2);
            if (capV != null) { int j = capV.Count; capV.Add(a); capV.Add(b); capV.Add(c); capT.Add(j); capT.Add(j + 1); capT.Add(j + 2); }
        }
        /// <summary>A quad with explicit texture coordinates (signs, screens): merged into the chunk like everything else.</summary>
        public void QuadUV(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Region((a + c) / 2); var n = Vector3.Cross(b - a, c - a).normalized; var buf = B(m); int i0 = buf.v.Count;
            Vector3[] P = { a, b, c, d }; Vector2[] U = { ua, ub, uc, ud };
            for (int k = 0; k < 4; k++) { buf.v.Add(P[k]); buf.n.Add(n); buf.uv0.Add(U[k]); buf.uv1.Add(Vector2.zero); buf.uv2.Add(Vector4.zero); buf.c.Add(Color.white); }
            buf.t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
        }
        public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3? want = null, float ao = -1, Color? tint = null)
        { Tri(m, a, b, c, want, ao, tint: tint); Tri(m, a, c, d, want, ao, tint: tint); }

        /// <summary>An oriented box. Sides use <paramref name="side"/> (with facade coordinates if given), top and bottom their own materials.</summary>
        public void Box(Vector3 center, Vector3 half, Quaternion q, Material side, Material top = null, Material bottom = null, string tag = null, bool safe = true,
            FacadeParams? facade = null, bool render = true, Color? tint = null)
        {
            Region(center);
            var ax = new[] { q * Vector3.right, q * Vector3.up, q * Vector3.forward }; var h = new[] { half.x, half.y, half.z };
            Vector3 Corner(float sx, float sy, float sz) => center + ax[0] * (sx * h[0]) + ax[1] * (sy * h[1]) + ax[2] * (sz * h[2]);
            if (render)
            {
                float baseY = center.y - half.y;
                for (int i = 0; i < 3; i++) for (int s = -1; s <= 1; s += 2)
                {
                    int j = (i + 1) % 3, k = (i + 2) % 3;
                    Vector3 F(float u, float v) { var cc = new float[3]; cc[i] = s; cc[j] = u; cc[k] = v; return Corner(cc[0], cc[1], cc[2]); }
                    var n = ax[i] * s;
                    var mat = n.y > .6f ? (top ?? side) : n.y < -.6f ? (bottom ?? Lib.Dark) : side;
                    Vector3 p0 = F(-1, -1), p1 = F(1, -1), p2 = F(1, 1), p3 = F(-1, 1);
                    if (facade.HasValue && Mathf.Abs(n.y) < .3f && mat == side)
                    {
                        // facade coords: u along the wall from its left edge, v height above the building base (metres)
                        var right = Vector3.Cross(Vector3.up, n).normalized;
                        float uMin = Mathf.Min(Vector3.Dot(p0, right), Mathf.Min(Vector3.Dot(p1, right), Mathf.Min(Vector3.Dot(p2, right), Vector3.Dot(p3, right))));
                        Vector2 FU(Vector3 p) => new Vector2(Vector3.Dot(p, right) - uMin, p.y - baseY);
                        Tri(mat, p0, p1, p2, n, -1, FU(p0), FU(p1), FU(p2), facade, tint);
                        Tri(mat, p0, p2, p3, n, -1, FU(p0), FU(p2), FU(p3), facade, tint);
                    }
                    else Quad(mat, p0, p1, p2, p3, n, tint: tint);
                }
            }
            if (tag != null)
            {
                var go = new GameObject(tag); go.transform.SetParent(colliders, false);
                go.transform.SetPositionAndRotation(center, q);
                var bc = go.AddComponent<BoxCollider>(); bc.size = half * 2; bc.sharedMaterial = worldPhys;
                var st = go.AddComponent<SurfaceTag>(); st.tagName = tag; st.safe = safe;
            }
        }

        /// <summary>
        /// A ramp-like prism: a top profile of (z, y) points extruded across width W, placed by a transform (pos, yaw degrees).
        /// Curved tops get smooth normals. Gets a mesh collider tagged <paramref name="tag"/>.
        /// </summary>
        public void Prism(List<Vector2> top, float W, Vector3 pos, float yaw, Material topMat, Material sideMat, string tag = "ramp", bool smooth = true, bool safe = false)
        {
            var q = Quaternion.Euler(0, yaw, 0); Region(pos);
            Vector3 P(float x, float y, float z) => pos + q * new Vector3(x, y, z);
            float hw = W / 2;
            capV = new List<Vector3>(); capT = new List<int>();
            // top surface strip with smooth normals along the profile
            var tb = B(topMat); int start = tb.v.Count;
            int nPts = top.Count;
            var nrm = new Vector3[nPts];
            for (int i = 0; i < nPts; i++)
            {
                Vector2 a = top[Mathf.Max(0, i - 1)], b = top[Mathf.Min(nPts - 1, i + 1)];
                var t2 = (b - a).normalized; var nl = new Vector3(0, t2.x, -t2.y);
                nrm[i] = q * (smooth ? nl : nl);
            }
            for (int i = 0; i < nPts - 1; i++)
            {
                Vector2 a = top[i], b = top[i + 1];
                if (!smooth) { var t2 = (b - a).normalized; nrm[i] = nrm[i + 1] = q * new Vector3(0, t2.x, -t2.y); }
                Vector3 A0 = P(-hw, a.y, a.x), A1 = P(hw, a.y, a.x), B1 = P(hw, b.y, b.x), B0 = P(-hw, b.y, b.x);
                int i0 = tb.v.Count;
                foreach (var (p, nn) in new[] { (A0, nrm[i]), (A1, nrm[i]), (B1, nrm[i + 1]), (B0, nrm[i + 1]) })
                { tb.v.Add(p); tb.n.Add(nn); tb.uv0.Add(new Vector2(Vector3.Dot(p - pos, q * Vector3.right), (i == 0 && p == A0 ? 0 : 0) + PathLen(top, p == A0 || p == A1 ? i : i + 1))); tb.uv1.Add(Vector2.zero); tb.uv2.Add(Vector4.zero); tb.c.Add(Color.white); }
                // winding so the normal points up/out
                var gn = Vector3.Cross(A1 - A0, B0 - A0);
                if (Vector3.Dot(gn, nrm[i]) >= 0) { tb.t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 }); }
                else { tb.t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 }); }
                int j = capV.Count; capV.AddRange(new[] { A0, A1, B1, B0 }); capT.AddRange(new[] { j, j + 1, j + 2, j, j + 2, j + 3 });
            }
            // sides, front face and underside down to y = -1
            var prof = new List<Vector2>();
            if (top[0].y > .001f) prof.Add(new Vector2(top[0].x, -.5f));
            prof.Add(new Vector2(top[nPts - 1].x, -.5f));
            for (int i = nPts - 1; i >= 0; i--) prof.Add(top[i]);
            var tris = Triangulate(prof);
            for (int s = -1; s <= 1; s += 2)
            {
                var want = q * new Vector3(s, 0, 0);
                for (int k = 0; k < tris.Count; k += 3)
                    Tri(sideMat, P(s * hw, prof[tris[k]].y, prof[tris[k]].x), P(s * hw, prof[tris[k + 1]].y, prof[tris[k + 1]].x), P(s * hw, prof[tris[k + 2]].y, prof[tris[k + 2]].x), want);
            }
            var last = top[nPts - 1];
            if (last.y > .05f) Quad(sideMat, P(-hw, -.5f, last.x), P(hw, -.5f, last.x), P(hw, last.y, last.x), P(-hw, last.y, last.x), q * Vector3.forward);
            // collider
            var go = new GameObject(tag); go.transform.SetParent(colliders, false);
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 }; mesh.SetVertices(capV); mesh.SetTriangles(capT, 0); mesh.RecalculateNormals();
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = worldPhys;
            var st = go.AddComponent<SurfaceTag>(); st.tagName = tag; st.safe = safe;
            capV = null; capT = null;
        }
        static float PathLen(List<Vector2> p, int upto) { float s = 0; for (int i = 0; i < upto; i++) s += Vector2.Distance(p[i], p[i + 1]); return s; }

        /// <summary>A vertical n-gon prism (columns, planters, dots), with a mesh collider unless tag is null.</summary>
        public void Cylinder(Vector3 c, float r, float top, float bottom, int n, Material sideMat, Material topMat, string tag = null, bool safe = true, bool render = true)
        {
            Region(c);
            Vector3 P(int k, float y) { float a = k / (float)n * Mathf.PI * 2; return new Vector3(c.x + Mathf.Cos(a) * r, y, c.z + Mathf.Sin(a) * r); }
            if (render)
            {
                var C = new Vector3(c.x, top, c.z);
                for (int k = 0; k < n; k++)
                {
                    if (topMat != null) Tri(topMat, C, P(k, top), P(k + 1, top), Vector3.up);
                    float a = (k + .5f) / n * Mathf.PI * 2; var nn = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Quad(sideMat, P(k, bottom), P(k + 1, bottom), P(k + 1, top), P(k, top), nn);
                }
            }
            if (tag != null)
            {
                var go = new GameObject(tag); go.transform.SetParent(colliders, false); go.transform.position = new Vector3(c.x, (top + bottom) / 2, c.z);
                if (n >= 16 && r > 3)
                {
                    var mesh = new Mesh(); var vv = new List<Vector3>(); var tt = new List<int>();
                    for (int k = 0; k <= n; k++) { vv.Add(P(k, top) - go.transform.position); vv.Add(P(k, bottom) - go.transform.position); }
                    for (int k = 0; k < n; k++) { int a = k * 2; tt.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
                    int ci = vv.Count; vv.Add(new Vector3(0, (top - bottom) / 2, 0));
                    for (int k = 0; k < n; k++) tt.AddRange(new[] { ci, k * 2 + 2, k * 2 });
                    mesh.SetVertices(vv); mesh.SetTriangles(tt, 0); mesh.RecalculateNormals();
                    var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = worldPhys;
                }
                else
                {
                    var cc = go.AddComponent<CapsuleCollider>(); cc.radius = r; cc.height = Mathf.Max(top - bottom, r * 2); cc.direction = 1; cc.sharedMaterial = worldPhys;
                }
                var st = go.AddComponent<SurfaceTag>(); st.tagName = tag; st.safe = safe;
            }
        }

        /// <summary>A light strip: a thin box from a to b, lifted along <paramref name="up"/>. Render only.</summary>
        public void Strip(Vector3 a, Vector3 b, Vector3 up, float w, float h, Material m)
        {
            var dir = b - a; float L = dir.magnitude; if (L < .05f) return; dir /= L; Region(a);
            var x = Vector3.Cross(up, dir).normalized; var y = Vector3.Cross(dir, x);
            Vector3 C(float sx, float sy, float sz) => a + dir * (sz * L) + x * (sx * w / 2) + y * (sy * h);
            Quad(m, C(-1, 1, 0), C(1, 1, 0), C(1, 1, 1), C(-1, 1, 1), y, 1);
            Quad(m, C(1, 0, 0), C(1, 1, 0), C(1, 1, 1), C(1, 0, 1), x, 1);
            Quad(m, C(-1, 0, 0), C(-1, 1, 0), C(-1, 1, 1), C(-1, 0, 1), -x, 1);
        }

        /// <summary>A tube along points (cables, pipes, roots). Render only.</summary>
        public void Tube(IList<Vector3> pts, float r, int sides, Material m)
        {
            if (pts.Count < 2) return; Region(pts[0]);
            var buf = B(m);
            var rings = new List<int>();
            Vector3 prevN = Vector3.up;
            for (int i = 0; i < pts.Count; i++)
            {
                var t = (pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                var nrm = Vector3.Cross(t, Mathf.Abs(t.y) > .9f ? Vector3.right : Vector3.up).normalized; var bn = Vector3.Cross(t, nrm);
                rings.Add(buf.v.Count);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2; var d = nrm * Mathf.Cos(a) + bn * Mathf.Sin(a);
                    var p = pts[i] + d * r;
                    buf.v.Add(p); buf.n.Add(d); buf.uv0.Add(new Vector2(s / (float)sides, i)); buf.uv1.Add(Vector2.zero); buf.uv2.Add(Vector4.zero); buf.c.Add(new Color(1, 1, 1, 1));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = rings[i] + s, b = rings[i + 1] + s;
                    buf.t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
        }

        /// <summary>Ear-clipping for simple polygons in the (x, y) plane.</summary>
        public static List<int> Triangulate(List<Vector2> poly)
        {
            var idx = new List<int>(); var V = new List<int>(); for (int i = 0; i < poly.Count; i++) V.Add(i);
            float area = 0; for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; area += a.x * b.y - b.x * a.y; }
            if (area < 0) V.Reverse();
            int guard = 0;
            while (V.Count > 2 && guard++ < 5000)
            {
                bool cut = false;
                for (int i = 0; i < V.Count; i++)
                {
                    int i0 = V[(i + V.Count - 1) % V.Count], i1 = V[i], i2 = V[(i + 1) % V.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-7f) continue;
                    bool inside = false;
                    foreach (var j in V) { if (j == i0 || j == i1 || j == i2) continue; if (InTri(poly[j], a, b, c)) { inside = true; break; } }
                    if (inside) continue;
                    idx.Add(i0); idx.Add(i1); idx.Add(i2); V.RemoveAt(i); cut = true; break;
                }
                if (!cut) break;
            }
            return idx;
        }
        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y), d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y), d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0; return !(neg && pos);
        }

        /// <summary>Turns every buffer into a mesh renderer (one per material per chunk).</summary>
        public void Build(bool shadows = true)
        {
            foreach (var kv in bufs)
            {
                var b = kv.Value; if (b.t.Count == 0) continue;
                var go = new GameObject(kv.Key.Item1.name + "_" + kv.Key.Item2); go.transform.SetParent(root, false);
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetUVs(0, b.uv0); mesh.SetUVs(1, b.uv1); mesh.SetUVs(2, b.uv2); mesh.SetColors(b.c);
                mesh.SetTriangles(b.t, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = kv.Key.Item1;
                bool emissive = kv.Key.Item1.shader == Lib.Emissive, glass = kv.Key.Item1.shader == Lib.Glass;
                mr.shadowCastingMode = shadows && !emissive && !glass ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = !emissive;
            }
            bufs.Clear();
        }
    }
}
