using UnityEngine;

namespace VoidStunts
{
    /// <summary>Low chase camera from the web build (portrait and landscape tuned separately) plus the menu turntable.</summary>
    public class ChaseCamera : MonoBehaviour
    {
        public Camera cam; public CarController car;
        Vector3 head = Vector3.forward, pos; float fov = 62, shake, fovKick, orbit = .7f, menuLift;
        public bool menu = true; public bool sheetOpen;

        public void Kick() => fovKick = 1;
        public void Shake(float s) => shake = Mathf.Max(shake, s);
        public void Snap(float yawDeg) { head = Quaternion.Euler(0, yawDeg, 0) * Vector3.forward; Step(0, true); }

        void LateUpdate() => Step(Time.deltaTime, false);

        void Step(float dt, bool snap)
        {
            if (car == null) return;
            var t = car.transform; var c = t.position; bool portrait = cam.aspect < 1;
            if (menu)
            {
                orbit += dt * .06f; menuLift = Mathf.Lerp(menuLift, sheetOpen ? 1 : 0, 1 - Mathf.Exp(-dt * 5));
                float r = portrait ? 11.2f : 9.2f, s = Mathf.Sin(orbit), co = Mathf.Cos(orbit);
                cam.transform.position = new Vector3(c.x + s * r, c.y + 1.7f + menuLift * .4f, c.z + co * r);
                float side = portrait ? 0 : 2.4f - menuLift * 1.2f, drop = portrait ? .95f + menuLift * 1.55f : .25f;
                // landscape: aim to the right of the car so it sits in the left column
                var right = new Vector3(co, 0, -s);
                cam.transform.LookAt(new Vector3(c.x, c.y - drop, c.z) - right * side);
                cam.fieldOfView = portrait ? 52 : 36;
                return;
            }
            var vel = car.rb.linearVelocity; float speed = vel.magnitude;
            Vector3? target = null;
            if (car.grounded >= 2) { var f = new Vector3(t.forward.x, 0, t.forward.z); if (f.magnitude > .3f) target = f.normalized; }
            else { var h = new Vector3(vel.x, 0, vel.z); if (h.magnitude > 5) target = h.normalized; }
            if (target.HasValue) head = Vector3.Lerp(head, target.Value, snap ? 1 : 1 - Mathf.Exp(-dt * (car.grounded >= 2 ? 3 : 1.1f))).normalized;
            float sp = Mathf.Min(speed, 45);
            float dist = portrait ? 9.8f + sp * .05f : 5.3f + sp * .035f, ht = portrait ? 3.8f + sp * .025f : 1.95f + sp * .018f;
            var want = c - head * dist; want.y = Mathf.Max(c.y + ht, 1.2f);
            // keep the camera out of walls: pull in along the boom if something is between car and camera
            var from = c + Vector3.up * 1.2f; var dir = want - from;
            if (Physics.SphereCast(from, .35f, dir.normalized, out var hit, dir.magnitude, ~(1 << CarController.CarLayer), QueryTriggerInteraction.Ignore))
                want = from + dir.normalized * Mathf.Max(1.5f, hit.distance - .2f);
            pos = snap ? want : Vector3.Lerp(pos, want, 1 - Mathf.Exp(-dt * 6.5f));
            cam.transform.position = pos;
            if (shake > 0) { cam.transform.position += Random.insideUnitSphere * shake * .22f; shake = Mathf.Max(0, shake - dt * 2.5f); }
            var look = c + head * 2.8f; look.y += 1.1f; cam.transform.LookAt(look);
            fovKick = Mathf.Max(0, fovKick - dt * 1.5f);
            float ft = portrait ? 72 + Mathf.Min(speed, 50) * .2f + (car.boosting ? 5 : 0) + fovKick * 8 : 50 + Mathf.Min(speed, 50) * .15f + (car.boosting ? 4 : 0) + fovKick * 6;
            // Unity's fieldOfView is vertical, like three.js
            fov = snap ? ft : Mathf.Lerp(fov, ft, 1 - Mathf.Exp(-dt * 3)); cam.fieldOfView = fov;
        }
    }
}
