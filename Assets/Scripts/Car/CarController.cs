using UnityEngine;

namespace VoidStunts
{
    /// <summary>Inputs written by TouchInput each frame.</summary>
    public class CarInput
    {
        public float steer, gas, brake, pitch, roll, spin; public bool boost, drift, @catch; public float hop;
        public Vector3 flick; // local angular impulse (rad/s): x pitch (+ front flip), y yaw (+ right), z roll (+ left)
    }

    /// <summary>
    /// The VS-02 raycast vehicle, ported from the web build's physStep(): four suspension rays, per-wheel tyre forces with a
    /// friction circle, drift, rocket, hop, and real-torque air control with flick impulses. Body contacts are PhysX.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public const float M = 1200, G = 13, REST = .5f, WR = .36f, RAYLEN = REST + WR, KS = 30000, CD_C = 3800, CD_R = 5200, KAR = 16000, MU = 1.45f;
        static readonly Vector3 IL = new(1761 * 1.5f, 2041 * 1.3f, 442 * 2.4f);
        public const int CarLayer = 8;
        static readonly int Mask = ~(1 << CarLayer);

        public class Wheel
        {
            public Vector3 local; public bool front; public bool hit; public float comp, load, vlat, vlong, spin; public Vector3 n = Vector3.up; public Collider col; public SurfaceTag tag; public Vector3 contact;
        }
        public readonly Wheel[] wheels = new Wheel[4];
        public readonly CarInput inp = new();
        public Rigidbody rb;
        public float steerAngle, fuel = 1;
        public int grounded; public bool boosting, drifting, frozen = true;
        // events read by scoring
        public float impact; public bool roofHit, bodyHit;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            var x = new[] { -.82f, .82f, -.82f, .82f }; var z = new[] { 1.32f, 1.32f, -1.28f, -1.28f };
            for (int i = 0; i < 4; i++) wheels[i] = new Wheel { local = new Vector3(x[i], -.05f, z[i]), front = i < 2 };
        }

        public void Setup()
        {
            gameObject.layer = CarLayer;
            var pm = new PhysicsMaterial("Car") { dynamicFriction = .15f, staticFriction = .15f, bounciness = .05f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            var body = gameObject.AddComponent<BoxCollider>(); body.center = new Vector3(0, .06f, -.04f); body.size = new Vector3(1.38f, .64f, 4.3f); body.sharedMaterial = pm;
            var cab = gameObject.AddComponent<BoxCollider>(); cab.center = new Vector3(0, .5f, -.3f); cab.size = new Vector3(.86f, .5f, 1.8f); cab.sharedMaterial = pm;
            foreach (var w in wheels)
            {
                var s = gameObject.AddComponent<SphereCollider>(); s.radius = .33f; s.center = new Vector3(w.local.x * 1.2f, -.3f, w.local.z); s.sharedMaterial = pm;
            }
            rb.mass = M; rb.linearDamping = 0; rb.angularDamping = 0; rb.maxAngularVelocity = 15;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = Vector3.zero; rb.inertiaTensor = IL; rb.inertiaTensorRotation = Quaternion.identity;
            rb.solverIterations = 10;
        }

        public void Place(Vector3 pos, float yawDeg, float lift = .9f)
        {
            var p = pos; if (Physics.Raycast(pos + Vector3.up * 60, Vector3.down, out var hit, 400, Mask, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            p.y += lift;
            rb.position = p; rb.rotation = Quaternion.Euler(0, yawDeg, 0); transform.SetPositionAndRotation(p, rb.rotation);
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; steerAngle = 0; fuel = Mathf.Max(fuel, .4f);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var t = transform; Vector3 fwd = t.forward, up = t.up, right = t.right;
            var vel = rb.linearVelocity; float speed = vel.magnitude, vFwd = Vector3.Dot(vel, fwd);
            float maxSteer = Mathf.Lerp(.6f, .2f, Mathf.Clamp01(Mathf.Abs(vFwd) / 32));
            float tgt = inp.steer * maxSteer, rate = (Mathf.Abs(tgt) < Mathf.Abs(steerAngle) ? 9 : 6) * dt;
            steerAngle += Mathf.Clamp(tgt - steerAngle, -rate, rate);
            bool boostOn = inp.boost && fuel > 0 && !frozen;
            float throttle = frozen ? 0 : Mathf.Max(inp.gas, boostOn ? 1 : 0);
            bool drift = !frozen && ((inp.brake > 0 && Mathf.Abs(inp.steer) > .3f && vFwd > 9) || (inp.drift && vFwd > 7));
            drifting = drift;

            // suspension
            int g = 0; var down = -up;
            foreach (var w in wheels)
            {
                var hp = t.TransformPoint(w.local);
                w.hit = Physics.Raycast(hp, down, out var hit, RAYLEN, Mask, QueryTriggerInteraction.Ignore);
                if (!w.hit) { w.comp = 0; w.load = 0; w.col = null; w.tag = null; continue; }
                g++; w.n = hit.normal; w.contact = hit.point;
                if (w.col != hit.collider) { w.col = hit.collider; w.tag = hit.collider.GetComponent<SurfaceTag>(); }
                float comp = RAYLEN - hit.distance; w.comp = comp;
                float cv = Vector3.Dot(rb.GetPointVelocity(hp), down);
                float fs = KS * comp + (cv > 0 ? CD_C : CD_R) * cv;
                if (comp > REST * .82f) fs += KS * 7 * (comp - REST * .82f);
                fs = Mathf.Max(0, fs); w.load = fs;
                rb.AddForceAtPosition(up * fs, hp);
            }
            for (int pair = 0; pair < 2; pair++)
            {
                var A = wheels[pair * 2]; var Bw = wheels[pair * 2 + 1]; if (!A.hit || !Bw.hit) continue;
                float f = KAR * (A.comp - Bw.comp);
                rb.AddForceAtPosition(up * f, t.TransformPoint(A.local)); rb.AddForceAtPosition(up * -f, t.TransformPoint(Bw.local));
            }
            // tyres
            foreach (var w in wheels)
            {
                if (!w.hit) continue;
                var n = w.n; float a = w.front ? steerAngle : 0;
                var wf = fwd * Mathf.Cos(a) + right * Mathf.Sin(a); wf = (wf - n * Vector3.Dot(wf, n)).normalized;
                var side = Vector3.Cross(n, wf).normalized;
                var p = t.TransformPoint(w.local) - up * .15f;
                var pv = rb.GetPointVelocity(p);
                float vl = Vector3.Dot(pv, wf), vs = Vector3.Dot(pv, side); w.vlat = vs; w.vlong = vl;
                float maxF = MU * w.load * (!w.front && drift ? .52f : 1);
                float Fy = Mathf.Clamp(-vs * (M / 4) / dt * .5f, -maxF, maxF), Fx;
                float drive = 13 * Mathf.Pow(Mathf.Max(0, 1 - Mathf.Max(0, vFwd) / 34), .62f);
                if (drift) Fx = (w.front ? 0 : 1) * Mathf.Max(throttle, .85f) * drive * M / 2;
                else if (inp.brake > 0 && !frozen && vFwd > 1.2f) Fx = -Mathf.Sign(vl) * Mathf.Min(18 * M / 4, Mathf.Abs(vl) * (M / 4) / dt * .5f);
                else if (inp.brake > 0 && !frozen && vFwd > -8) Fx = -7 * M / 4;
                else if (throttle > 0) Fx = throttle * drive * M / 4;
                else Fx = -Mathf.Sign(vl) * Mathf.Min(1.2f * M / 4, Mathf.Abs(vl) * (M / 4) / dt * .5f);
                Fx -= vl * M * .01f;
                if (frozen) Fx = -Mathf.Sign(vl) * Mathf.Min(25 * M / 4, Mathf.Abs(vl) * (M / 4) / dt * .5f);
                float tot = Mathf.Sqrt(Fx * Fx + Fy * Fy), lim = MU * w.load * 1.05f;
                if (tot > lim) { Fx *= lim / tot; Fy *= lim / tot; }
                rb.AddForceAtPosition(wf * Fx + side * Fy, p);
            }
            grounded = g;
            if (drift && g >= 3) { float yr = Vector3.Dot(rb.angularVelocity, up), limY = 1.7f; if (Mathf.Abs(yr) > limY) rb.AddTorque(up * (-(yr - Mathf.Sign(yr) * limY) * IL.y * 14)); }
            // aero
            rb.AddForce(vel * (-.0012f * M * speed));
            if (g >= 3) rb.AddForce(up * (-M * Mathf.Min(.006f * speed * speed, 10)));
            // rocket
            boosting = boostOn;
            if (boostOn) { rb.AddForce(fwd * (22 * M)); fuel = Mathf.Max(0, fuel - dt * .3f); }
            else if (g >= 2) fuel = Mathf.Min(1, fuel + dt * .2f);
            // hop, flicks
            if (inp.hop > 0 && g >= 2 && !frozen) { rb.AddForce(up * inp.hop, ForceMode.VelocityChange); inp.hop = 0; } else if (g < 2) inp.hop = 0;
            var wl = t.InverseTransformDirection(rb.angularVelocity);
            if (inp.flick.sqrMagnitude > 0)
            {
                if (g == 0 && !frozen) { wl += inp.flick; wl = new Vector3(Mathf.Clamp(wl.x, -9.5f, 9.5f), Mathf.Clamp(wl.y, -9.5f, 9.5f), Mathf.Clamp(wl.z, -10, 10)); rb.angularVelocity = t.TransformDirection(wl); }
                inp.flick = Vector3.zero;
            }
            // air control: real torque, stronger with the rocket lit; released axes keep momentum, a still thumb catches
            if (g == 0 && !frozen)
            {
                float k = boostOn ? 1.3f : 1;
                var al = new Vector3(inp.pitch * 8 * k, inp.spin * 8.5f * k, -inp.roll * 9.5f * k);
                if (Mathf.Abs(wl.x) > 7 && Mathf.Sign(al.x) == Mathf.Sign(wl.x)) al.x = 0;
                if (Mathf.Abs(wl.y) > 7 && Mathf.Sign(al.y) == Mathf.Sign(wl.y)) al.y = 0;
                if (Mathf.Abs(wl.z) > 8 && Mathf.Sign(al.z) == Mathf.Sign(wl.z)) al.z = 0;
                if (up.y > .64f && wl.magnitude < 2.6f)
                {
                    var e = t.InverseTransformDirection(Vector3.Cross(up, Vector3.up));
                    if (inp.pitch == 0) al.x += e.x * 5; if (inp.roll == 0) al.z += e.z * 5;
                }
                float dmp = inp.@catch ? 9 : .35f;
                if (inp.pitch == 0) al.x -= wl.x * dmp; if (inp.spin == 0) al.y -= wl.y * dmp; if (inp.roll == 0) al.z -= wl.z * dmp;
                rb.AddRelativeTorque(Vector3.Scale(al, IL));
            }
            else if (g > 0) rb.AddTorque(rb.angularVelocity * (-IL.y * .6f));

            foreach (var w in wheels) w.spin += (w.hit ? w.vlong : w.vlong * .98f) * dt / WR;
        }

        void OnCollisionEnter(Collision c) => Contact(c);
        void OnCollisionStay(Collision c) => Contact(c);
        void Contact(Collision c)
        {
            bodyHit = true; impact = Mathf.Max(impact, c.relativeVelocity.magnitude);
            for (int i = 0; i < c.contactCount; i++)
            {
                var lp = transform.InverseTransformPoint(c.GetContact(i).point);
                if (lp.y > .38f && c.relativeVelocity.magnitude > 1.2f) roofHit = true;
            }
        }
        public void ClearEvents() { impact = 0; roofHit = false; bodyHit = false; }

        /// <summary>Visual hub heights in shell space for CarVisual (JS: w.p.y − ext).</summary>
        public void HubHeights(float[] outY, float[] outSpin)
        {
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i]; float ext = w.hit ? RAYLEN - w.comp - WR : REST;
                // shell wheel order follows the web build: JS +x is the car's left, which is our wheel 0 (x = −.82)
                outY[i] = w.local.y - ext; outSpin[i] = w.spin;
            }
        }
    }
}
