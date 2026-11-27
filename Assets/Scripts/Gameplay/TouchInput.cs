using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// Two thumbs, three verbs each — hold, slide, flick (the web build's scheme, unchanged).
    /// LEFT = BODY   ground: slide sideways to steer · flick up to hop      air: slide to flip/roll · flick to throw a rotation · hold still to catch
    /// RIGHT = POWER ground: hold for gas · slide up for rocket, down to brake · flick up = rocket burst · flick down = drift   air: slide sideways to spin · flick to spin
    /// Keyboard works in the editor. Distances are in points (iOS pt), not pixels.
    /// </summary>
    public class TouchInput : MonoBehaviour
    {
        public CarController car; public bool active; public System.Action<string> onVerb; public System.Func<Vector2, bool> blocked;
        public class Thumb { public int id; public bool left; public Vector2 p0, p; public List<(float t, Vector2 p)> hist = new(); }
        public readonly Dictionary<int, Thumb> thumbs = new();
        public readonly List<(List<Vector2> pts, float t)> streaks = new();
        const float GZ = 55;
        float burstT, driftT; bool wasAir; float ppp = 3;
        public static float PointsPerPixel => 1f / Mathf.Max(1, (Screen.dpi > 0 ? Screen.dpi : 460) / 153f);

        void Start() { ppp = 1 / PointsPerPixel; }
        float Now => Time.unscaledTime * 1000;

        void Down(int id, Vector2 px)
        {
            if (!active || (blocked != null && blocked(px))) return;
            bool left = px.x < Screen.width / 2f; foreach (var t in thumbs.Values) if (t.left == left) return;
            var p = px / ppp; var th = new Thumb { id = id, left = left, p0 = p, p = p }; th.hist.Add((Now, p)); thumbs[id] = th;
        }
        void Move(int id, Vector2 px)
        {
            if (!thumbs.TryGetValue(id, out var t)) return; t.p = px / ppp; float now = Now; t.hist.Add((now, t.p));
            while (t.hist.Count > 2 && now - t.hist[0].t > 160) t.hist.RemoveAt(0);
        }
        void Up(int id)
        {
            if (!thumbs.TryGetValue(id, out var t)) return; thumbs.Remove(id);
            float now = Now; var h = t.hist.FindAll(q => now - q.t < 110); if (h.Count < 2) return;
            // screen y is up in Unity; the web build had it down — flip once here
            float dx = t.p.x - h[0].p.x, dy = -(t.p.y - h[0].p.y), dtm = Mathf.Max(16, now - h[0].t), v = Mathf.Sqrt(dx * dx + dy * dy) / dtm;
            if (v < .5f || Mathf.Sqrt(dx * dx + dy * dy) < 22) return;
            float vx = dx / dtm, vy = dy / dtm; bool air = car.grounded == 0, vert = Mathf.Abs(dy) > Mathf.Abs(dx) * 1.2f, horiz = Mathf.Abs(dx) > Mathf.Abs(dy) * 1.2f;
            string did = null;
            if (t.left)
            {
                if (!air && vert && dy < 0) { car.inp.hop = 7; did = "hop"; }
                else if (air) { car.inp.flick += new Vector3(Mathf.Clamp(-vy * 4.5f, -9, 9), 0, -Mathf.Clamp(vx * 4.5f, -9, 9)); did = "flick"; }
            }
            else
            {
                if (vert && dy < 0) { burstT = .6f; did = "boost"; }
                else if (!air && vert && dy > 0) { driftT = .9f; did = "drift"; }
                else if (air && horiz) { car.inp.flick += new Vector3(0, Mathf.Clamp(vx * 4.5f, -9, 9), 0); did = "flick"; }
            }
            if (did != null) { onVerb?.Invoke(did); var pts = new List<Vector2>(); foreach (var q in t.hist) pts.Add(q.p); streaks.Add((pts, 0)); }
        }

        public void Clear() { thumbs.Clear(); burstT = driftT = 0; }

        void Update()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var tc = Input.GetTouch(i);
                if (tc.phase == TouchPhase.Began) Down(tc.fingerId, tc.position);
                else if (tc.phase == TouchPhase.Moved || tc.phase == TouchPhase.Stationary) Move(tc.fingerId, tc.position);
                else Up(tc.fingerId);
            }
            if (Input.touchCount == 0)
            {
                if (Input.GetMouseButtonDown(0)) Down(-1, Input.mousePosition); else if (Input.GetMouseButton(0)) Move(-1, Input.mousePosition); else if (Input.GetMouseButtonUp(0)) Up(-1);
            }
            for (int i = streaks.Count - 1; i >= 0; i--) { var s = streaks[i]; s.t += Time.unscaledDeltaTime; if (s.t > .45f) streaks.RemoveAt(i); else streaks[i] = s; }
            if (!active) { Clear(); return; }
            Read(Time.deltaTime);
        }

        static float K(KeyCode k) => Input.GetKey(k) ? 1 : 0;
        static float Dz(float v) => Mathf.Abs(v) < .1f ? 0 : (v - Mathf.Sign(v) * .1f) / .9f;

        void Read(float dt)
        {
            var inp = car.inp; bool air = car.grounded == 0;
            Thumb L = null, R = null; foreach (var t in thumbs.Values) { if (t.left) L = t; else R = t; }
            // on take-off both thumbs re-anchor where they are — nothing rotates until you move on purpose
            if (air && !wasAir) foreach (var t in thumbs.Values) t.p0 = t.p;
            wasAir = air;
            float lx = L != null ? Mathf.Clamp((L.p.x - L.p0.x) / GZ, -1, 1) : 0, ly = L != null ? Mathf.Clamp((L.p.y - L.p0.y) / GZ, -1, 1) : 0;
            float rx = R != null ? Mathf.Clamp((R.p.x - R.p0.x) / GZ, -1, 1) : 0, rdy = R != null ? -(R.p.y - R.p0.y) : 0;
            burstT = Mathf.Max(0, burstT - dt); driftT = Mathf.Max(0, driftT - dt);
            inp.steer = Mathf.Clamp(Dz(lx) + K(KeyCode.D) - K(KeyCode.A) + K(KeyCode.RightArrow) - K(KeyCode.LeftArrow), -1, 1);
            inp.gas = Mathf.Max(R != null && rdy < 34 ? 1 : 0, K(KeyCode.W), K(KeyCode.UpArrow));
            inp.brake = (R != null && rdy > 34) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0;
            inp.boost = (R != null && rdy < -34) || burstT > 0 || Input.GetKey(KeyCode.Space);
            inp.drift = driftT > 0 || (Input.GetKey(KeyCode.LeftShift) && Mathf.Abs(inp.steer) > .3f);
            inp.pitch = air ? Dz(ly) + K(KeyCode.I) - K(KeyCode.K) : 0; inp.roll = air ? Dz(lx) + K(KeyCode.L) - K(KeyCode.J) : 0; inp.spin = air ? Dz(rx) + K(KeyCode.O) - K(KeyCode.U) : 0;
            bool still = false;
            if (L != null && air) { float now = Now; var h = L.hist; still = (now - h[0].t > 150 || h.Count < 3) && (L.p - h[0].p).magnitude < 8 && (L.p - L.p0).magnitude < 14; }
            inp.@catch = still || (air && Input.GetKey(KeyCode.S));
            if (Input.GetKeyDown(KeyCode.X) && car.grounded >= 2) inp.hop = 6.4f;
            if (Input.GetKeyDown(KeyCode.E)) burstT = .6f;
        }
    }
}
