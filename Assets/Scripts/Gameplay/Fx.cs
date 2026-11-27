using UnityEngine;

namespace VoidStunts
{
    /// <summary>Graphic effects, no confetti: shock rings on landings and ring passes, ring and pad light animation.</summary>
    public class Fx : MonoBehaviour
    {
        class P { public Transform t; public Material m; public float age, life, s0, s1, a; public Color c; public bool on; }
        readonly P[] pool = new P[16]; int idx; Mesh annulus;
        static readonly Color VoltHdr = new(1.3f, 1.9f, 0, 1), Ink = new(.08f, .08f, .09f, 1);

        public void Init()
        {
            annulus = new Mesh(); int n = 72; var v = new Vector3[n * 2]; var t = new int[n * 6];
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; v[i * 2] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .965f; v[i * 2 + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); int j = (i + 1) % n; t[i * 6] = i * 2; t[i * 6 + 1] = j * 2; t[i * 6 + 2] = i * 2 + 1; t[i * 6 + 3] = i * 2 + 1; t[i * 6 + 4] = j * 2; t[i * 6 + 5] = j * 2 + 1; }
            annulus.vertices = v; annulus.triangles = t; annulus.RecalculateBounds();
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Pulse"); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = annulus; var mr = go.AddComponent<MeshRenderer>(); var m = new Material(Lib.Pulse); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false); pool[i] = new P { t = go.transform, m = m };
            }
        }
        public void Pulse(Vector3 p, Vector3 n, float s0, float s1, float life, float a, bool additive)
        {
            var q = pool[idx]; idx = (idx + 1) % pool.Length;
            q.t.position = p; q.t.rotation = Quaternion.FromToRotation(Vector3.forward, n); q.age = 0; q.life = life; q.s0 = s0; q.s1 = s1; q.a = a; q.on = true; q.t.gameObject.SetActive(true);
            q.c = additive ? VoltHdr : Ink; q.m.SetFloat("_Dst", additive ? 1 : 10);
        }
        public void Land(Vector3 pos, Vector3 n, float s)
        {
            var p = pos; if (Physics.Raycast(pos, Vector3.down, out var h, 4, ~(1 << CarController.CarLayer), QueryTriggerInteraction.Ignore)) { p = h.point + h.normal * .05f; n = h.normal; }
            Pulse(p, n, 1.2f, 4 + s * .25f, .6f, .75f, false); Pulse(p, n, 1, 2.6f + s * .15f, .45f, .6f, true);
        }
        public void RingPass(Ring r) { Pulse(r.c, r.n, r.r, r.r * 1.7f, .55f, 1, true); Pulse(r.c, r.n, r.r * .9f, r.r * 1.25f, .4f, .8f, true); }

        public void Tick(float dt, LevelContext ctx)
        {
            foreach (var q in pool)
            {
                if (!q.on) continue; q.age += dt / q.life; if (q.age >= 1) { q.on = false; q.t.gameObject.SetActive(false); continue; }
                float e = 1 - Mathf.Pow(1 - q.age, 3); q.t.localScale = Vector3.one * Mathf.Lerp(q.s0, q.s1, e);
                var c = q.c; c.a = q.a * (1 - q.age); q.m.SetColor("_BaseColor", c);
            }
            if (ctx == null) return;
            float time = Time.time;
            foreach (var r in ctx.rings)
            {
                r.flash = Mathf.Max(0, r.flash - dt * 2);
                float k = r.active ? 2.4f + Mathf.Sin(time * 3 + r.c.x) * .4f + r.flash * 6 : .25f;
                r.led.SetFloat("_Intensity", k); r.led.SetColor("_BaseColor", r.active ? new Color(.83f, 1, 0) : new Color(.5f, .5f, .5f));
                if (r.t) r.t.localRotation = Quaternion.FromToRotation(Vector3.forward, r.n) * Quaternion.Euler(0, 0, time * (r.active ? 12 : 2));
            }
            foreach (var p in ctx.pads)
            {
                p.ok = Mathf.MoveTowards(p.ok, 1, dt * 2.5f);
                var m = p.mr.sharedMaterial; m.SetFloat("_Intensity", 1.6f + (p.cool > .3f ? 3 : 0) + Mathf.Sin(time * 6) * .25f);
                m.SetColor("_BaseColor", Color.Lerp(new Color(1, .2f, .2f), new Color(.83f, 1, 0), p.ok));
                m.SetTextureOffset("_BaseMap", new Vector2(0, -time * 1.4f));
            }
        }
    }
}
