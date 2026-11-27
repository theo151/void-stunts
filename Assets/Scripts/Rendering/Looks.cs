using UnityEngine;

namespace VoidStunts
{
    /// <summary>
    /// The ten looks (VSLooks.shader runs as a URP Full Screen Pass set up by the build script) and the momentum ladder:
    /// sustained momentum wipes the world into your next starred look, never sooner than every 9 s.
    /// </summary>
    public class Looks : MonoBehaviour
    {
        public static readonly string[] NAMES = { "STUDIO", "DRAFT", "RISO", "GRAPHITE", "MATRIX", "OIL", "GHOST", "VINYL", "GLYPH", "PIXEL" };
        public static readonly string[] SUBS = { "CLEAN RENDER", "INK ON GRID", "TWO-INK PRINT", "NIGHT LINE", "LED DISPLAY", "PAINTED", "OPTICAL HUD", "DESIGNER TOY", "DOT + RED", "16-BIT DUSK" };
        static readonly int[] LADDER_ORDER = { 2, 1, 9, 5, 4, 7, 0, 8, 6, 3 };
        public static readonly bool[] DARK = { false, false, false, true, true, false, true, false, false, true };

        public int baseLook, cur, to; public bool auto = true; public float M, flash;
        public System.Collections.Generic.List<int> favs = new() { 0, 2 };
        float wipe, dwell, below; int lvl; Camera cam;
        public System.Action<int> onLook;

        public void Init(Camera c) { cam = c; Shader.SetGlobalVector("_VS_Volt", new Vector4(.831f, 1, 0, 1)); Shader.SetGlobalFloat("_VS_LineAmt", 1); Snap(baseLook); }

        public int[] Ladder()
        {
            var l = new System.Collections.Generic.List<int> { baseLook };
            foreach (var m in LADDER_ORDER) if (m != baseLook && favs.Contains(m)) l.Add(m);
            return l.ToArray();
        }
        public void Snap(int m) { cur = to = m; wipe = 0; Shader.SetGlobalFloat("_VS_From", m); Shader.SetGlobalFloat("_VS_To", m); Shader.SetGlobalFloat("_VS_WipeR", 0); onLook?.Invoke(m); }
        public void SetBase(int m) { baseLook = m; lvl = 0; Go(m, true); }
        public void ResetLadder() { M = 0; lvl = 0; dwell = 0; Snap(baseLook); }

        /// <summary>Wipe to a look, expanding from the car (or the centre).</summary>
        public void Go(int m, bool fromCenter, Vector3? world = null)
        {
            if (m == to && wipe == 0 && m == cur) return;
            if (wipe > 0) { cur = to; Shader.SetGlobalFloat("_VS_From", cur); }
            to = m; wipe = .0001f; Shader.SetGlobalFloat("_VS_To", m);
            Vector2 c = new(Screen.width / 2f, Screen.height / 2f);
            if (!fromCenter && world.HasValue && cam) { var sp = cam.WorldToScreenPoint(world.Value); c = new Vector2(sp.x, sp.y); }
            float rs = RenderScale();
            Shader.SetGlobalVector("_VS_WipeC", new Vector4(c.x * rs, c.y * rs, 0, 0));
            onLook?.Invoke(m);
        }
        static float RenderScale() { var a = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset; return a ? a.renderScale : 1; }

        /// <summary>Called by scoring when a landing banks points.</summary>
        public void Bank(float total) { M = Mathf.Min(1, M + Mathf.Min(.22f, total / 6500f)); flash = .35f; }
        public void Bail() { M *= .5f; }

        public void Tick(float dt, bool scoring, bool air, bool drifting, Vector3 carPos, System.Action<int> announce)
        {
            if (wipe > 0)
            {
                wipe += dt / 1.3f; float maxR = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) * RenderScale() * 1.15f;
                float e = wipe < .5f ? 4 * wipe * wipe * wipe : 1 - Mathf.Pow(-2 * wipe + 2, 3) / 2;
                Shader.SetGlobalFloat("_VS_WipeR", Mathf.Min(1, e) * maxR);
                if (wipe >= 1) { cur = to; Shader.SetGlobalFloat("_VS_From", cur); Shader.SetGlobalFloat("_VS_WipeR", 0); wipe = 0; }
            }
            flash = Mathf.Max(0, flash - dt * 3); Shader.SetGlobalFloat("_VS_Flash", flash);
            if (!scoring) return;
            M = Mathf.Clamp01(M + dt * ((air ? .015f : 0) + (drifting ? .02f : 0) - .012f));
            if (!auto) return;
            dwell += dt; var L = Ladder();
            float[] th = { .25f, .5f, .72f, .9f }; int want = 0; for (int i = 0; i < 4; i++) if (M > th[i]) want = i + 1;
            want = Mathf.Min(want, L.Length - 1); if (lvl > L.Length - 1) lvl = L.Length - 1;
            if (want > lvl && dwell > 9) { lvl++; dwell = 0; below = 0; Go(L[lvl], false, carPos); announce?.Invoke(L[lvl]); }
            else if (want < lvl) { below += dt; if (below > 6 && dwell > 9) { lvl--; dwell = 0; below = 0; Go(L[lvl], false, carPos); announce?.Invoke(L[lvl]); } }
            else below = 0;
        }
    }
}
