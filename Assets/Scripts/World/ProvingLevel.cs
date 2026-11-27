using System.Collections.Generic;
using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// PROVING — a 1.1 km white test facility: a 400 m banked oval, a 60 m ski-jump tower, a bowl field, a 200 m mega-ramp,
    /// a stair-set plaza, monolith slalom, a hangar you can drive through, and a canal gap. Clean Nothing-grey on dot grid.
    /// </summary>
    public class ProvingLevel : Level
    {
        public override string Id => "proving";
        public override string Name => "PROVING";
        public override string Blurb => "1.1 KM TEST GROUND";
        public override (string, string)[] Goals => new[] {
            ("flip", "LAND A FLIP OR ROLL"), ("double", "LAND A DOUBLE ROTATION"), ("air3", "3 SECONDS OF AIR"), ("gap", "MEGA · CLEAR THE CANAL"),
            ("land_tower", "LAND THE SKI JUMP"), ("land_hangar", "LAND ON THE HANGAR"), ("rings", "FLY THROUGH 5 RINGS"), ("combo", "HIT A ×4 COMBO") };

        const float E = 560;
        LevelContext C; MeshKit K;

        public override bool Gap(AirInfo a, Vector3 land) => a.start.z > 5 && a.start.z < 40 && Mathf.Abs(a.start.x - 240) < 20 && land.z > 95;

        public override void Build(LevelContext c)
        {
            C = c; K = c.kit; K.chunkSize = 200; c.extent = E;
            var floor = Lib.Surf("concrete", 6f, new Color(.93f, .93f, .91f), macro: 1.6f, grime: .3f); floor.name = "Floor"; floor.SetFloat("_Grid", 1);
            for (float x = -E; x < E; x += 200) for (float z = -E; z < E; z += 200) { K.Region(new Vector3(x + 100, 0, z + 100)); K.Quad(floor, new Vector3(x, 0, z), new Vector3(x + 200, 0, z), new Vector3(x + 200, 0, z + 200), new Vector3(x, 0, z + 200), Vector3.up, 1); }
            K.Box(new Vector3(0, -1, 0), new Vector3(E + 100, 1, E + 100), Quaternion.identity, floor, tag: "ground", render: false);
            Walls(); Oval(new Vector3(-230, 0, -200)); SkiJump(new Vector3(-420, 0, 120)); Bowls(new Vector3(120, 0, -240)); Mega(new Vector3(240, 0, 0)); Stairs(new Vector3(-40, 0, 80)); Monoliths(); Hangar(new Vector3(-20, 0, -420));
            c.spawn = new Vector3(0, 0, -60); c.spawnYaw = 0;
        }

        void Walls()
        {
            // a ring of tall pale slabs with dot-grid numbering, like a set at the edge of the world
            for (int s = 0; s < 4; s++) for (float t = -E; t < E; t += 40)
            {
                var q = Quaternion.Euler(0, s * 90, 0); var p = q * new Vector3(t + 20, 0, E + 6); float h = 30 + 18 * Mathf.PerlinNoise(t * .02f, s);
                K.Box(p + Vector3.up * h / 2, new Vector3(19.8f, h / 2, 6), q, Lib.Facade, Lib.Concrete, tag: "wall", safe: false, facade: new FacadeParams(40, h, t + s * 9, 3));
                if (((int)(t / 40) & 3) == 0) C.Sign(p + q * Vector3.back * 6, q * Vector3.back, h * .6f, ((int)(t / 40 + 20 + s * 40)).ToString("00"), 2, false);
            }
        }

        void Oval(Vector3 o)
        {
            // two 120 m straights and two banked 60 m radius turns (32°), 20 m wide, with an LED lip
            float R = 60, W = 20, bank = 32;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                K.Box(o + new Vector3(sgn * R, .1f, 0), new Vector3(W / 2, .1f, 60), Quaternion.identity, Lib.Asphalt, Lib.Asphalt, tag: "safe");
                for (int k = 0; k < 18; k++)
                {
                    float a0 = k * 10, am = a0 + 5; var cc = o + new Vector3(0, 0, sgn * 60);
                    var d = Quaternion.Euler(0, sgn > 0 ? -am : -am - 180, 0); // d·right = radial direction, sweeping over the north (sgn +) or south end
                    var p = cc + d * Vector3.right * R; var q = Quaternion.LookRotation(d * Vector3.forward, Vector3.up) * Quaternion.Euler(0, 0, bank); // outer edge (local +x) up
                    float lift = Mathf.Sin(bank * Mathf.Deg2Rad) * W / 2;
                    K.Box(p + Vector3.up * (lift + .2f), new Vector3(W / 2, .5f, R * Mathf.PI / 18 * .56f), q, Lib.Concrete, Lib.Asphalt, Lib.Dark, "safe");
                    K.Strip(p + d * Vector3.right * (W / 2 * Mathf.Cos(bank * Mathf.Deg2Rad)) + Vector3.up * (2 * lift + .7f), p + d * Vector3.right * (W / 2 * Mathf.Cos(bank * Mathf.Deg2Rad)) + Vector3.up * (2 * lift + .7f) + d * Vector3.forward * 3, Vector3.up, .25f, .05f, Lib.LedVolt);
                }
            }
            C.AddPad(o + new Vector3(-R, .2f, -30), Vector3.forward, 34); C.AddPad(o + new Vector3(R, .2f, 30), Vector3.back, 34);
            K.Prism(CityLevel.Curve(14, 22, 5), 12, o + new Vector3(0, 0, -6), 0, Lib.Concrete, Lib.ConcreteWarm);
            C.AddRing(o + new Vector3(0, 10, 10), Vector3.forward, 5.5f);
        }

        void SkiJump(Vector3 o)
        {
            // a stepped 60 m tower; ramp up the back, steep in-run down the front, a table and a long landing hill
            float H = 60;
            for (int k = 0; k < 6; k++) K.Box(o + new Vector3(0, k * 10 + 5, -k * 3), new Vector3(16 - k, 5, 16 - k * 1.2f), Quaternion.identity, Lib.Facade, Lib.Concrete, tag: k == 5 ? "land_tower" : "wall", safe: k == 5, facade: new FacadeParams(4, 5, k, 1));
            K.Prism(CityLevel.WithDeck(CityLevel.Straight(220, H, 4), 0), 14, o + new Vector3(30, 0, -235), 0, Lib.Asphalt, Lib.Concrete, "ramp");
            K.Box(o + new Vector3(22, H - .7f, -15), new Vector3(15, .7f, 6), Quaternion.identity, Lib.Concrete, Lib.Asphalt, tag: "safe");
            // in-run (concave), facing +z
            var inrun = new List<Vector2>(); for (int i = 0; i <= 18; i++) { float t = i / 18f; inrun.Add(new Vector2(t * 110, H * (1 - t) * (1 - t) * .9f + H * .1f * (1 - t) + (t > .9f ? (t - .9f) * 30 : 0))); }
            var rev = new List<Vector2>(); for (int i = inrun.Count - 1; i >= 0; i--) rev.Add(new Vector2(110 - inrun[i].x, inrun[i].y));
            // rev goes low→high along +x; high end meets the tower top (z = o.z − 5), the lip is at z + 105, so place it reversed (yaw 180)
            K.Prism(rev, 14, o + new Vector3(0, 0, 105), 180, Lib.Asphalt, Lib.ConcreteWarm, "ramp");
            C.AddPad(o + new Vector3(0, H, -14), Vector3.forward, 30);
            // landing hill
            var hill = new List<Vector2>(); for (int i = 0; i <= 14; i++) { float t = i / 14f; hill.Add(new Vector2(t * 160, 18 * Mathf.Sin(t * Mathf.PI) * (t < .5f ? 1 : 1 - (t - .5f) * .4f))); }
            K.Prism(hill, 40, o + new Vector3(0, 0, 150), 0, Lib.Moss, Lib.ConcreteWarm, "safe", true, true);
            C.AddRing(o + new Vector3(0, 32, 170), new Vector3(0, -.1f, 1), 7);
        }

        void Bowls(Vector3 o)
        {
            // a field of concrete quarter-pipes and spines
            for (int k = 0; k < 4; k++) K.Prism(CityLevel.WithDeck(CityLevel.Curve(10, 84, 12), 4), 70, o + Quaternion.Euler(0, k * 90, 0) * new Vector3(0, 0, -44), k * 90 + 180, Lib.Concrete, Lib.ConcreteWarm, "qp");
            K.Prism(CityLevel.Curve(8, 40, 8), 30, o + new Vector3(0, 0, -6), 0, Lib.Concrete, Lib.ConcreteWarm); K.Prism(CityLevel.Curve(8, 40, 8), 30, o + new Vector3(0, 0, 6), 180, Lib.Concrete, Lib.ConcreteWarm);
            C.AddRing(o + new Vector3(0, 14, 0), Vector3.right, 5);
        }

        void Mega(Vector3 o)
        {
            // a 200 m mega-ramp: drop-in tower, roll-in, 30 m canal gap, quarter-pipe exit
            float H = 44;
            K.Box(o + new Vector3(0, H / 2, -170), new Vector3(10, H / 2, 10), Quaternion.identity, Lib.Facade, Lib.Concrete, tag: "safe", facade: new FacadeParams(4, 4, 3, 2));
            K.Prism(CityLevel.Straight(180, H, 3), 12, o + new Vector3(20, 0, -350), 0, Lib.Asphalt, Lib.Concrete, "ramp");
            K.Box(o + new Vector3(17, H - .7f, -170), new Vector3(9, .7f, 6), Quaternion.identity, Lib.Concrete, Lib.Asphalt, tag: "safe");
            var roll = new List<Vector2>(); for (int i = 0; i <= 16; i++) { float t = i / 16f; roll.Add(new Vector2(t * 180, H * (1 - Mathf.SmoothStep(0, 1, t)) + (t > .88f ? (t - .88f) * 60 : 0))); }
            var rev = new List<Vector2>(); for (int i = roll.Count - 1; i >= 0; i--) rev.Add(new Vector2(180 - roll[i].x, roll[i].y));
            K.Prism(rev, 16, o + new Vector3(0, 0, 20), 180, Lib.Asphalt, Lib.ConcreteWarm, "ramp");
            C.AddPad(o + new Vector3(0, H - .2f, -156), Vector3.forward, 30);
            // the canal
            K.Box(o + new Vector3(0, -2.94f, 75), new Vector3(120, 3, 15), Quaternion.identity, Lib.Dark, Lib.GlassSmoke, tag: "water", safe: false);
            var landing = new List<Vector2>(); for (int i = 0; i <= 10; i++) { float t = i / 10f; landing.Add(new Vector2(t * 70, 9 * (1 - t))); }
            K.Prism(landing, 30, o + new Vector3(0, 0, 92), 0, Lib.Asphalt, Lib.ConcreteWarm, "safe", true, true);
            K.Prism(CityLevel.WithDeck(CityLevel.Curve(12, 80, 12), 4), 30, o + new Vector3(0, 0, 230), 0, Lib.Concrete, Lib.ConcreteWarm, "qp");
            C.AddRing(o + new Vector3(0, 20, 70), new Vector3(0, -.15f, 1), 7); C.AddRing(o + new Vector3(0, 30, -60), Vector3.forward, 6);
        }

        void Stairs(Vector3 o)
        {
            for (int k = 0; k < 6; k++) K.Box(o + new Vector3(0, k * .6f + .3f, k * 2.4f), new Vector3(28, .3f + k * .6f, 1.2f + (5 - k) * 1.2f) + new Vector3(0, 0, 0), Quaternion.identity, Lib.Concrete, Lib.ConcreteWarm, tag: "safe");
            K.Box(o + new Vector3(0, 1.8f, 26), new Vector3(28, 1.8f, 12), Quaternion.identity, Lib.Concrete, Lib.ConcreteWarm, tag: "safe");
            K.Prism(CityLevel.Wedge(10, 3.6f), 14, o + new Vector3(-20, 0, -12), 0, Lib.Concrete, Lib.ConcreteWarm);
            K.Box(o + new Vector3(18, 1.2f, 46), new Vector3(.15f, .05f, 10), Quaternion.identity, Lib.Steel, tag: "rail", safe: false);
            for (int k = -2; k <= 2; k++) C.veg.Add(Vegetation.Kind.Tall, o + new Vector3(k * 12, 3.6f, 30), .8f);
        }

        void Monoliths()
        {
            for (int k = 0; k < 18; k++)
            {
                var p = new Vector3(-120 + (k % 6) * 40 + (k / 6 % 2) * 20, 0, 230 + (k / 6) * 34); float h = 14 + (k * 37 % 11);
                K.Box(p + Vector3.up * h / 2, new Vector3(2, h / 2, 5), Quaternion.Euler(0, k * 13, 0), k % 5 == 0 ? Lib.Volt : Lib.Ink, Lib.Ink, tag: "wall", safe: false);
            }
        }

        void Hangar(Vector3 o)
        {
            // a 120 × 60 m hangar, open at both ends, roof drivable from a side ramp
            float H = 22;
            foreach (var s in new[] { -1, 1 }) K.Box(o + new Vector3(s * 31, H / 2, 0), new Vector3(1, H / 2, 60), Quaternion.identity, Lib.Facade, Lib.Concrete, tag: "wall", safe: false, facade: new FacadeParams(6, H, s, 3));
            K.Box(o + new Vector3(0, H + .6f, 0), new Vector3(32, .6f, 60), Quaternion.identity, Lib.Concrete, Lib.ConcreteWarm, Lib.Dark, "land_hangar");
            for (float z = -56; z <= 56; z += 8) K.Strip(o + new Vector3(-30, H - .1f, z), o + new Vector3(30, H - .1f, z), Vector3.down, .3f, .05f, Lib.LedWhite);
            K.Prism(CityLevel.Straight(110, H + 1.2f, 3), 14, o + new Vector3(46, 0, -60), 0, Lib.Asphalt, Lib.Concrete, "ramp");
            K.Box(o + new Vector3(40, H + .6f, 52), new Vector3(8, .6f, 4), Quaternion.identity, Lib.Concrete, Lib.Asphalt, tag: "safe");
            K.Prism(CityLevel.Curve(12, 18, 4), 20, o + new Vector3(0, H + 1.2f, 54), 0, Lib.Asphalt, Lib.Concrete);
            C.Sign(o + new Vector3(0, 0, -60), Vector3.back, H - 6, "HANGAR 2", 0, false);
            C.AddRing(o + new Vector3(0, 10, 0), Vector3.forward, 6);
        }
    }
}
