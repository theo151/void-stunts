using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// three.js-compatible primitive generators (same axes and parameters), so the frozen VS-02 design ports number for number.
    /// Every triangle is wound to agree with its analytic normal, so meshes are correct regardless of handedness.
    /// </summary>
    public static class Geo
    {
        public class M { public readonly List<Vector3> v = new(); public readonly List<Vector3> n = new(); public readonly List<Vector2> uv = new(); public readonly List<int> t = new(); }

        public static Mesh Finish(M m, bool fixWinding = true)
        {
            if (fixWinding)
                for (int i = 0; i < m.t.Count; i += 3)
                {
                    Vector3 a = m.v[m.t[i]], b = m.v[m.t[i + 1]], c = m.v[m.t[i + 2]];
                    var nn = m.n[m.t[i]] + m.n[m.t[i + 1]] + m.n[m.t[i + 2]];
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), nn) < 0) (m.t[i + 1], m.t[i + 2]) = (m.t[i + 2], m.t[i + 1]);
                }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(m.v); mesh.SetNormals(m.n); mesh.SetUVs(0, m.uv);
            var cols = new Color[m.v.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; mesh.colors = cols;
            mesh.SetTriangles(m.t, 0); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Box(float w, float h, float d)
        {
            var m = new M(); var half = new Vector3(w, h, d) / 2;
            for (int ax = 0; ax < 3; ax++) for (int s = -1; s <= 1; s += 2)
            {
                var n = Vector3.zero; n[ax] = s; int u = (ax + 1) % 3, v = (ax + 2) % 3; int i0 = m.v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float su = (k == 1 || k == 2) ? 1 : -1, sv = k >= 2 ? 1 : -1;
                    var p = Vector3.zero; p[ax] = s * half[ax]; p[u] = su * half[u]; p[v] = sv * half[v];
                    m.v.Add(p); m.n.Add(n);
                    // three BoxGeometry +y face: u along +x, v = 1 at +z
                    m.uv.Add(ax == 1 ? new Vector2((p.x / w) + .5f, .5f + (p.z / d)) : new Vector2(su * .5f + .5f, sv * .5f + .5f));
                }
                m.t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 });
            }
            return Finish(m);
        }

        /// <summary>three CylinderGeometry: axis Y, x = r·sinθ, z = r·cosθ.</summary>
        public static Mesh Cylinder(float rTop, float rBot, float h, int seg, bool open = false, float th0 = 0, float thLen = Mathf.PI * 2, int hSeg = 1)
        {
            var m = new M(); float slope = (rBot - rTop) / h;
            for (int i = 0; i <= seg; i++)
            {
                float th = th0 + i / (float)seg * thLen, s = Mathf.Sin(th), c = Mathf.Cos(th);
                var n = new Vector3(s, slope, c).normalized;
                for (int k = 0; k <= hSeg; k++)
                {
                    float f = k / (float)hSeg, r = Mathf.Lerp(rTop, rBot, f);
                    m.v.Add(new Vector3(r * s, h / 2 - f * h, r * c)); m.n.Add(n); m.uv.Add(new Vector2(i / (float)seg, 1 - f));
                }
            }
            for (int i = 0; i < seg; i++) for (int k = 0; k < hSeg; k++) { int a = i * (hSeg + 1) + k, b = a + hSeg + 1; m.t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 }); }
            if (!open)
                for (int cap = 0; cap < 2; cap++)
                {
                    float y = cap == 0 ? h / 2 : -h / 2, r = cap == 0 ? rTop : rBot; var n = new Vector3(0, cap == 0 ? 1 : -1, 0);
                    int ci = m.v.Count; m.v.Add(new Vector3(0, y, 0)); m.n.Add(n); m.uv.Add(new Vector2(.5f, .5f));
                    for (int i = 0; i <= seg; i++) { float th = th0 + i / (float)seg * thLen; m.v.Add(new Vector3(r * Mathf.Sin(th), y, r * Mathf.Cos(th))); m.n.Add(n); m.uv.Add(new Vector2(Mathf.Sin(th) * .5f + .5f, Mathf.Cos(th) * .5f + .5f)); }
                    for (int i = 0; i < seg; i++) m.t.AddRange(new[] { ci, ci + 1 + i, ci + 2 + i });
                }
            return Finish(m);
        }

        /// <summary>three CircleGeometry: a disc in the XY plane facing +Z.</summary>
        public static Mesh Circle(float r, int seg)
        {
            var m = new M(); m.v.Add(Vector3.zero); m.n.Add(Vector3.forward); m.uv.Add(new Vector2(.5f, .5f));
            for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; m.v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0)); m.n.Add(Vector3.forward); m.uv.Add(new Vector2(Mathf.Cos(a) * .5f + .5f, Mathf.Sin(a) * .5f + .5f)); }
            for (int i = 1; i <= seg; i++) m.t.AddRange(new[] { 0, i, i + 1 });
            return Finish(m);
        }

        /// <summary>three TorusGeometry: ring in the XY plane around Z.</summary>
        public static Mesh Torus(float R, float r, int radial, int tubular, float arc = Mathf.PI * 2)
        {
            var m = new M();
            for (int j = 0; j <= radial; j++) for (int i = 0; i <= tubular; i++)
            {
                float u = i / (float)tubular * arc, v = j / (float)radial * Mathf.PI * 2;
                var c = new Vector3(R * Mathf.Cos(u), R * Mathf.Sin(u), 0);
                var p = new Vector3((R + r * Mathf.Cos(v)) * Mathf.Cos(u), (R + r * Mathf.Cos(v)) * Mathf.Sin(u), r * Mathf.Sin(v));
                m.v.Add(p); m.n.Add((p - c).normalized); m.uv.Add(new Vector2(i / (float)tubular, j / (float)radial));
            }
            for (int j = 1; j <= radial; j++) for (int i = 1; i <= tubular; i++)
            {
                int a = (tubular + 1) * j + i - 1, b = (tubular + 1) * (j - 1) + i - 1, c = (tubular + 1) * (j - 1) + i, d = (tubular + 1) * j + i;
                m.t.AddRange(new[] { a, b, d, b, c, d });
            }
            return Finish(m);
        }

        /// <summary>three LatheGeometry: profile (x = radius, y = height) revolved around Y; normals from the profile.</summary>
        public static Mesh Lathe(List<Vector2> pts, int seg)
        {
            var m = new M(); int np = pts.Count;
            var pn = new Vector2[np];
            for (int i = 0; i < np; i++) { var d = pts[Mathf.Min(np - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; pn[i] = new Vector2(d.y, -d.x).normalized; }
            for (int s = 0; s <= seg; s++)
            {
                float phi = s / (float)seg * Mathf.PI * 2, sn = Mathf.Sin(phi), cs = Mathf.Cos(phi);
                for (int i = 0; i < np; i++)
                {
                    m.v.Add(new Vector3(pts[i].x * sn, pts[i].y, pts[i].x * cs));
                    m.n.Add(new Vector3(pn[i].x * sn, pn[i].y, pn[i].x * cs).normalized);
                    m.uv.Add(new Vector2(s / (float)seg, i / (float)(np - 1)));
                }
            }
            for (int s = 0; s < seg; s++) for (int i = 0; i < np - 1; i++)
            {
                int a = s * np + i, b = a + np;
                m.t.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            }
            return Finish(m);
        }

        /// <summary>A tube along a uniform Catmull-Rom curve through the points.</summary>
        public static Mesh Tube(List<Vector3> pts, int tubularSeg, float r, int radial)
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= tubularSeg; i++) path.Add(CatmullRom(pts, i / (float)tubularSeg));
            var m = new M(); Vector3 prevN = Vector3.zero;
            for (int i = 0; i < path.Count; i++)
            {
                var t = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector3 n = prevN == Vector3.zero ? Vector3.Cross(t, Mathf.Abs(t.y) < .9f ? Vector3.up : Vector3.right).normalized : Vector3.Cross(Vector3.Cross(t, prevN), t).normalized;
                prevN = n; var b = Vector3.Cross(t, n);
                for (int j = 0; j <= radial; j++)
                {
                    float a = j / (float)radial * Mathf.PI * 2; var d = n * Mathf.Cos(a) + b * Mathf.Sin(a);
                    m.v.Add(path[i] + d * r); m.n.Add(d); m.uv.Add(new Vector2(i / (float)tubularSeg, j / (float)radial));
                }
            }
            for (int i = 0; i < path.Count - 1; i++) for (int j = 0; j < radial; j++)
            {
                int a = i * (radial + 1) + j, c = a + radial + 1;
                m.t.AddRange(new[] { a, c, a + 1, c, c + 1, a + 1 });
            }
            return Finish(m);
        }
        public static Vector3 CatmullRom(List<Vector3> p, float t)
        {
            int n = p.Count - 1; float f = t * n; int i = Mathf.Min(n - 1, Mathf.FloorToInt(f)); float u = f - i;
            Vector3 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(n, i + 2)];
            return .5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
        }

        /// <summary>three RoundedBoxGeometry equivalent: a subdivided box whose corners are pushed out to radius r.</summary>
        public static Mesh RoundedBox(float w, float h, float d, int seg, float r)
        {
            var m = new M(); var half = new Vector3(w, h, d) / 2; var inner = half - Vector3.one * r; int N = seg * 2 + 2;
            for (int ax = 0; ax < 3; ax++) for (int s = -1; s <= 1; s += 2)
            {
                int u = (ax + 1) % 3, v = (ax + 2) % 3, i0 = m.v.Count;
                for (int j = 0; j <= N; j++) for (int i = 0; i <= N; i++)
                {
                    var p = Vector3.zero; p[ax] = s * half[ax]; p[u] = Mathf.Lerp(-half[u], half[u], i / (float)N); p[v] = Mathf.Lerp(-half[v], half[v], j / (float)N);
                    var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    var dir = (p - c); var n = dir.sqrMagnitude > 1e-9f ? dir.normalized : Vector3.zero; if (n == Vector3.zero) n[ax] = s;
                    m.v.Add(c + n * r); m.n.Add(n); m.uv.Add(new Vector2(i / (float)N, j / (float)N));
                }
                for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) { int a = i0 + j * (N + 1) + i; m.t.AddRange(new[] { a, a + 1, a + N + 2, a, a + N + 2, a + N + 1 }); }
            }
            return Finish(m);
        }

        public static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 eulerRadXYZ = default, bool shadows = true)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            go.transform.localRotation = EulerXYZ(eulerRadXYZ);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        /// <summary>three.js Euler 'XYZ' (radians): matrix = Rx · Ry · Rz. The rotation matrices coincide with Unity's per axis.</summary>
        public static Quaternion EulerXYZ(Vector3 r) =>
            Quaternion.AngleAxis(r.x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(r.y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(r.z * Mathf.Rad2Deg, Vector3.forward);
    }
}
