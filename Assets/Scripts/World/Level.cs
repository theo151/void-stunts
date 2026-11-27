using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    public class Ring { public Vector3 c, n; public float r; public bool active = true; public float cool, flash, side = float.NaN; public Transform t; public Material led; }
    public class Pad { public Vector3 c, d; public float w = 6, l = 11, v = 30, cool; public MeshRenderer mr; public float ok = 1; }
    public class AirInfo { public float t, pitch, yaw, roll, maxY; public Vector3 start; public bool crashed; public int rings; }

    /// <summary>Everything a level hands to the game: geometry kit, vegetation, rings, pads, spawn, goals.</summary>
    public class LevelContext
    {
        public MeshKit kit; public Vegetation veg; public Transform root;
        public readonly List<Ring> rings = new(); public readonly List<Pad> pads = new();
        public Vector3 spawn; public float spawnYaw; public float extent = 600;
        public System.Random rng;
        public float R() => (float)rng.NextDouble();
        public float R(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
        static Texture2D chevron;
        static readonly Dictionary<string, Material> signMats = new();

        public void AddRing(Vector3 c, Vector3 n, float r = 5)
        {
            var ring = new Ring { c = c, n = n.normalized, r = r };
            var go = new GameObject("Ring"); go.transform.SetParent(root, false); go.transform.position = c; go.transform.rotation = Quaternion.FromToRotation(Vector3.forward, ring.n); go.transform.localScale = Vector3.one * r;
            Geo.Obj("Torus", go.transform, Geo.Torus(1, .07f, 14, 96), Lib.Steel, Vector3.zero);
            ring.led = new Material(Lib.LedVolt);
            var arc = Geo.Torus(1, .028f, 6, 12, Mathf.PI * 2 / 8 * .72f);
            for (int i = 0; i < 8; i++) foreach (var z in new[] { .07f, -.07f }) Geo.Obj("Arc", go.transform, arc, ring.led, new Vector3(0, 0, z), new Vector3(0, 0, i / 8f * Mathf.PI * 2), false);
            ring.t = go.transform; rings.Add(ring);
        }

        public void AddPad(Vector3 c, Vector3 dir, float v = 30)
        {
            var pad = new Pad { c = c, d = new Vector3(dir.x, 0, dir.z).normalized, v = v };
            if (chevron == null)
            {
                chevron = new Texture2D(64, 128, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                var px = new Color32[64 * 128];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 64; x++)
                {
                    float u = x / 63f, v2 = y / 127f, ax = Mathf.Abs(u - .5f), vv = Mathf.Repeat(v2 * 3, 1);
                    bool chev = Mathf.Abs(vv - .5f + ax * .9f - .2f) < .07f && ax < .38f, edge = ax > .46f;
                    byte k = (byte)(chev ? 255 : edge ? 150 : 14); px[y * 64 + x] = new Color32(k, k, k, 255);
                }
                chevron.SetPixels32(px); chevron.Apply(true);
            }
            var m = new Material(Lib.LedVolt); m.SetTexture("_BaseMap", chevron); m.SetFloat("_Intensity", 2.4f);
            var go = Geo.Obj("Pad", root, QuadMesh(pad.w, pad.l), m, c + Vector3.up * .04f, default, false);
            go.transform.rotation = Quaternion.LookRotation(pad.d, Vector3.up);
            pad.mr = go.GetComponent<MeshRenderer>(); pads.Add(pad);
        }
        static Mesh QuadMesh(float w, float l)
        {
            var m = new Geo.M();
            m.v.AddRange(new[] { new Vector3(-w / 2, 0, -l / 2), new Vector3(w / 2, 0, -l / 2), new Vector3(w / 2, 0, l / 2), new Vector3(-w / 2, 0, l / 2) });
            for (int i = 0; i < 4; i++) m.n.Add(Vector3.up);
            m.uv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            m.t.AddRange(new[] { 0, 2, 1, 0, 3, 2 }); return Geo.Finish(m);
        }

        /// <summary>A dot-matrix blade sign sticking out of a facade (readable from both sides), or flat on it.</summary>
        public void Sign(Vector3 basePos, Vector3 wallNormal, float y, string word, int theme, bool blade = true)
        {
            string key = word + "|" + theme;
            if (!signMats.TryGetValue(key, out var mat))
            {
                Color[] fg = { new(.83f, 1, 0), new(1, .56f, .72f), new(.96f, .95f, .92f), new(1, .76f, .48f), new(.5f, .91f, .85f) };
                var tex = DotMatrix.Render(word, 12, fg[theme % fg.Length], new Color(.05f, .05f, .055f, 1), true, word.Length > 2, 10);
                tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Trilinear;
                mat = new Material(Lib.Emissive) { name = "Sign" }; mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Intensity", 2.2f); mat.SetFloat("_FarDim", .6f);
                signMats[key] = mat;
            }
            var tx = (Texture2D)mat.GetTexture("_BaseMap"); float aspect = tx.width / (float)tx.height;
            float h = word.Length > 2 ? word.Length * 1.35f : 2.2f, w = h * aspect;
            var n = wallNormal.normalized;
            var right = Vector3.Cross(Vector3.up, n).normalized;
            var center = basePos + Vector3.up * y + (blade ? n * (w / 2 + .5f) : n * .08f);
            var face = blade ? right : n; // blade signs face along the street
            SignFace(mat, center, Quaternion.LookRotation(-face, Vector3.up), w, h);
            if (blade)
            {
                SignFace(mat, center, Quaternion.LookRotation(face, Vector3.up), w, h);
                kit.Box(center, new Vector3(.08f, h / 2 + .15f, w / 2 + .15f), Quaternion.LookRotation(n, Vector3.up), Lib.Dark);
                kit.Box(basePos + Vector3.up * (y + h / 2 - .2f) + n * .3f, new Vector3(.06f, .06f, .3f), Quaternion.LookRotation(n), Lib.Steel);
            }
        }
        /// <summary>One readable face of a sign: viewer on the face's −z side looking +z, texture +u to the viewer's right.</summary>
        void SignFace(Material mat, Vector3 c, Quaternion q, float w, float h)
        {
            var r = q * Vector3.right * (w / 2); var u = q * Vector3.up * (h / 2);
            kit.QuadUV(mat, c - r - u, c + r - u, c + r + u, c - r + u, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        }
        static Mesh QuadMeshV(float w, float h)
        {
            var m = new Geo.M();
            m.v.AddRange(new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0) });
            for (int i = 0; i < 4; i++) m.n.Add(Vector3.back);
            m.uv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }); // viewer on −z looking +z: +x is to the right
            m.t.AddRange(new[] { 0, 1, 2, 0, 2, 3 }); return Geo.Finish(m);
        }

        /// <summary>A sagging cable between two points, optionally hung with warm lanterns.</summary>
        public void Wire(Vector3 a, Vector3 b, float sag, bool lanterns = false)
        {
            var pts = new List<Vector3>(); for (int k = 0; k <= 12; k++) { float t = k / 12f; pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4 * t * (1 - t))); }
            kit.Tube(pts, .035f, 4, Lib.Dark);
            if (lanterns) for (int k = 2; k < 12; k += 2) kit.Box(pts[k] + Vector3.down * .35f, new Vector3(.18f, .24f, .18f), Quaternion.identity, Lib.LedWarm);
        }
    }

    public abstract class Level
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract string Blurb { get; }
        public virtual int Look => 0;
        public abstract (string id, string text)[] Goals { get; }
        public virtual bool Gap(AirInfo a, Vector3 landPos) => false;
        /// <summary>Lets a level refine the surface tag a landing reports (e.g. terrain on a mesa top).</summary>
        public virtual string LandTag(string tag, Vector3 p) => tag;
        public abstract void Build(LevelContext c);
        public virtual void Tick(float dt) { }
    }
}
