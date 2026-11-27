using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// Run scoring ported from the web build: air tracking in the car's frame, trick naming, combo multiplier, bails, goals,
    /// rings, directional boost pads, drift scoring, last-safe-spot respawn and bounds.
    /// </summary>
    public class Scoring
    {
        public const float RUN_TIME = 180;
        public int score, best, mult = 1; public float time = RUN_TIME, clock, comboT, lastBank = -99, crashT;
        public AirInfo air; float airT, upsideT, safeT; public bool scoringOn;
        public readonly HashSet<string> goals = new();
        public int statRings, statFlips; public float statBestLand, statLongAir;
        public bool driftOn; public float driftD, driftOff;
        public Vector3 safeP; public float safeYaw;
        readonly CarController car; readonly Level level; readonly LevelContext ctx;

        public System.Action<string, string, bool> onBank; public System.Action<string> onToast, onGoal, onSfx; public System.Action<float, Vector3, Vector3> onLand;
        public System.Action<Ring> onRing; public System.Action<Pad> onPad; public System.Action onRespawn;

        static readonly HashSet<string> SAFE = new() { "safe", "top", "via", "spur", "rooftail", "market", "stacks", "garage", "sidewalk", "street", "ground", "strip", "land_mesa", "land_tower", "land_hangar", "roof" };

        public Scoring(CarController c, Level l, LevelContext x) { car = c; level = l; ctx = x; safeP = x.spawn; safeYaw = x.spawnYaw; }

        public void Goal(string id)
        {
            if (!scoringOn || goals.Contains(id)) return; string text = null;
            foreach (var g in level.Goals) if (g.id == id) text = g.text; if (text == null) return;
            goals.Add(id); score += 1000; onToast?.Invoke("GOAL · " + text + " · +1,000"); onGoal?.Invoke(id); onSfx?.Invoke("goal");
        }
        int Bank(float pts, string what)
        {
            mult = clock - lastBank < 4 ? Mathf.Min(8, mult + 1) : 1;
            int total = Mathf.RoundToInt(pts * mult); score += total; lastBank = clock; comboT = 4;
            onBank?.Invoke("+" + total.ToString("N0") + (mult > 1 ? " ×" + mult : ""), what, false); onSfx?.Invoke("bank" + mult);
            if (mult >= 4) Goal("combo"); return total;
        }
        void Bail(string why)
        {
            mult = 1; comboT = 0; lastBank = -99; crashT = 1; if (air != null) air.crashed = true;
            onBank?.Invoke("BAILED", why, true); onSfx?.Invoke("bail");
        }

        public (List<string> names, float pts, float rot, int flips, int maxAxis) Trick(AirInfo a)
        {
            float TAU = Mathf.PI * 2, P = Mathf.Abs(a.pitch), Y = Mathf.Abs(a.yaw), Rr = Mathf.Abs(a.roll);
            int flips = Mathf.FloorToInt((P + .75f) / TAU), rolls = Mathf.FloorToInt((Rr + .75f) / TAU), half = Mathf.FloorToInt((Y + .5f) / Mathf.PI);
            var names = new List<string>(); float pts = 0;
            if (flips > 0) { names.Add((a.pitch > 0 ? "FRONTFLIP" : "BACKFLIP") + (flips > 1 ? " ×" + flips : "")); pts += 650 * flips * (1 + .3f * (flips - 1)); }
            if (rolls > 0) { names.Add("BARREL ROLL" + (rolls > 1 ? " ×" + rolls : "")); pts += 480 * rolls * (1 + .3f * (rolls - 1)); }
            if (half > 0) { names.Add(half * 180 + " SPIN"); pts += 230 * half * (1 + .15f * (half - 1)); }
            if ((flips > 0 ? 1 : 0) + (rolls > 0 ? 1 : 0) + (half > 0 ? 1 : 0) >= 2) pts *= 1.3f;
            float h = a.maxY - Mathf.Max(a.start.y, car.rb.position.y);
            if (a.t > .5f) { pts += a.t * 140; if (a.t > 1.4f) names.Add("AIR " + a.t.ToString("0.0") + "S"); }
            if (h > 8) { names.Add("BIG AIR " + Mathf.RoundToInt(h) + "M"); pts += h * 18; }
            return (names, pts, flips + rolls + half / 2f, flips + rolls, Mathf.Max(flips, rolls, half / 2));
        }

        public void Respawn()
        {
            car.Place(safeP, safeYaw, 1.0f); car.fuel = Mathf.Max(car.fuel, .5f); air = null; airT = 0; onRespawn?.Invoke();
        }

        public void Tick(float dt)
        {
            clock += dt; if (crashT > 0) crashT -= dt;
            var t = car.transform; var up = t.up; var pos = car.rb.position; var vel = car.rb.linearVelocity; float speed = vel.magnitude;
            if (car.grounded == 0)
            {
                airT += dt;
                if (air == null && airT > .12f) air = new AirInfo { start = pos, maxY = pos.y };
                if (air != null)
                {
                    air.t += dt; air.maxY = Mathf.Max(air.maxY, pos.y);
                    // integrate rotation in the car's frame; convert to the web build's sign conventions (+pitch = front flip, +yaw = right)
                    var wl = t.InverseTransformDirection(car.rb.angularVelocity);
                    air.pitch += wl.x * dt; air.yaw += wl.y * dt; air.roll += wl.z * dt;
                    if (car.roofHit && !air.crashed && scoringOn) Bail("ROOF FIRST");
                    if (air.t > 3) Goal("air3");
                }
            }
            else
            {
                if (air != null && car.grounded >= 2)
                {
                    var a = air; air = null; var nAvg = Vector3.zero; string landTag = null;
                    foreach (var w in car.wheels) if (w.hit) { nAvg += w.n; if (w.tag != null) landTag = w.tag.tagName; }
                    nAvg.Normalize(); float align = Vector3.Dot(up, nAvg);
                    if (landTag != null) landTag = level.LandTag(landTag, pos);
                    if (a.t > .5f && scoringOn && !a.crashed)
                    {
                        if (landTag == "water") Bail("IN THE CANAL");
                        else if (align < .55f || car.roofHit) Bail("SKETCHY LANDING");
                        else
                        {
                            var tp = Trick(a);
                            if (tp.pts > 120)
                            {
                                float mc = 1; if (align > .95f) { tp.names.Add("CLEAN"); mc = 1.2f; }
                                int tot = Bank(tp.pts * mc, tp.names.Count > 0 ? string.Join(" · ", tp.names) : "AIR");
                                statBestLand = Mathf.Max(statBestLand, tot); statFlips += tp.flips;
                                if (tp.flips > 0) Goal("flip"); if (tp.maxAxis >= 2 || tp.rot >= 2) Goal("double");
                            }
                            if (level.Gap(a, pos)) Goal("gap");
                            if (landTag != null && landTag.StartsWith("land_")) Goal(landTag); else if (landTag != null) Goal("land_" + landTag);
                        }
                        statLongAir = Mathf.Max(statLongAir, a.t);
                    }
                    if (a.t > .35f) onLand?.Invoke(Mathf.Max(0, -Vector3.Dot(vel, nAvg)) + a.t * 3, pos, nAvg);
                }
                if (car.grounded >= 2) airT = 0;
            }
            if (car.roofHit && air == null && scoringOn && crashT <= 0 && up.y < .2f) Bail("ON THE ROOF");

            foreach (var h in ctx.rings)
            {
                if (!h.active) { h.cool -= dt; if (h.cool <= 0) h.active = true; }
                float side = Vector3.Dot(pos - h.c, h.n);
                if (h.active && !float.IsNaN(h.side) && Mathf.Sign(side) != Mathf.Sign(h.side))
                {
                    float inPlane = (pos - h.c - h.n * side).magnitude;
                    if (inPlane < h.r + .4f)
                    {
                        h.active = false; h.cool = 20; h.flash = 1; car.fuel = Mathf.Min(1, car.fuel + .45f); onRing?.Invoke(h);
                        if (air != null) air.rings++;
                        if (scoringOn) { score += 600; statRings++; lastBank = clock; comboT = 4; onBank?.Invoke("+600", "RING · BOOST +45%", false); if (statRings >= 5) Goal("rings"); }
                        onSfx?.Invoke("hoop");
                    }
                }
                h.side = side;
            }
            var fwd = t.forward; var fh = new Vector3(fwd.x, 0, fwd.z); float fl = Mathf.Max(.001f, fh.magnitude);
            var hv = new Vector3(vel.x, 0, vel.z); float hs = hv.magnitude;
            foreach (var p in ctx.pads)
            {
                p.cool = Mathf.Max(0, p.cool - dt);
                if (car.grounded < 2 || p.cool > 0) continue;
                var rel = pos - p.c; float al = rel.x * p.d.x + rel.z * p.d.z, sd = rel.x * p.d.z - rel.z * p.d.x;
                if (Mathf.Abs(al) < p.l / 2 + 1 && Mathf.Abs(sd) < p.w / 2 + .6f && Mathf.Abs(rel.y) < 2.5f)
                {
                    // a pad only fires for a car running with it (within ~40°); crossing it or driving it backwards does nothing
                    float along = hs > 3 ? (vel.x * p.d.x + vel.z * p.d.z) / hs : (fh.x * p.d.x + fh.z * p.d.z) / fl, head = (fh.x * p.d.x + fh.z * p.d.z) / fl;
                    if (along < .76f || head < .7f) { p.cool = .25f; p.ok = 0; continue; }
                    var dir = Vector3.Lerp(hs > .1f ? hv / hs : p.d, p.d, .5f).normalized;
                    float v = Vector3.Dot(vel, dir); if (v < p.v) car.rb.linearVelocity = vel + dir * (p.v - Mathf.Max(v, 0));
                    car.fuel = 1; p.cool = .8f; onPad?.Invoke(p); onSfx?.Invoke("pad");
                }
            }
            // drift
            float slip = hs > 1 ? Mathf.Acos(Mathf.Clamp(Vector3.Dot(hv, fh / fl) / hs, -1, 1)) : 0;
            if (car.grounded >= 3 && hs > 9 && slip > 15 * Mathf.Deg2Rad && slip < 110 * Mathf.Deg2Rad) { driftOn = true; driftD += hs * dt; driftOff = 0; }
            else if (driftOn) { driftOff += dt; if (driftOff > .35f || car.grounded < 2) { if (driftD > 15 && scoringOn) { Bank(driftD * 8, "DRIFT " + Mathf.RoundToInt(driftD) + " M"); if (driftD >= 40) Goal("drift40"); } driftOn = false; driftD = 0; } }
            if (comboT > 0) { comboT -= dt; if (comboT <= 0) mult = 1; }
            // safe spot
            bool allSafe = car.grounded == 4; if (allSafe) foreach (var w in car.wheels) if (w.tag == null || !w.tag.safe || !SAFE.Contains(w.tag.tagName)) allSafe = false;
            if (allSafe && up.y > .95f && speed < 35 && Mathf.Abs(pos.x) < ctx.extent - 10 && Mathf.Abs(pos.z) < ctx.extent - 10) { safeT += dt; if (safeT > .4f) { safeT = 0; safeP = pos; safeYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg; } }
            // stuck / upside down
            if ((up.y < .35f && speed < 3 && car.grounded < 3) || (speed < .4f && car.grounded < 3 && !car.frozen)) upsideT += dt; else upsideT = 0;
            if (upsideT > 1.4f) { upsideT = 0; Respawn(); onToast?.Invoke("RESET TO LAST SAFE SPOT"); }
            if (pos.y < -40 || Mathf.Abs(pos.x) > ctx.extent + 80 || Mathf.Abs(pos.z) > ctx.extent + 80) { if (scoringOn) Bail("OUT OF BOUNDS"); Respawn(); }
            car.ClearEvents();
        }
    }
}
