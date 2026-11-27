using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidStunts
{
    /// <summary>Persistent choices (PlayerPrefs) and the three quality tiers, tuned for A15 (iPhone 13 Pro / Pro Max) as the baseline.</summary>
    public static class Settings
    {
        public static int level, livery, plume, look, atmos = 1, quality = 1; public static bool sound = true, haptics = true, hz120, autoLooks = true, coach = true;
        public static int[] favs = { 0, 2 };
        public static readonly string[] QUALITY = { "PERFORMANCE", "BALANCED", "FIDELITY" };

        public static void Load()
        {
            level = PlayerPrefs.GetInt("vs.level", 0); livery = PlayerPrefs.GetInt("vs.livery", 0); plume = PlayerPrefs.GetInt("vs.plume", 0);
            look = PlayerPrefs.GetInt("vs.look", 0); atmos = PlayerPrefs.GetInt("vs.atmos", 1); quality = PlayerPrefs.GetInt("vs.quality", 1);
            sound = PlayerPrefs.GetInt("vs.sound", 1) == 1; haptics = PlayerPrefs.GetInt("vs.haptics", 1) == 1; hz120 = PlayerPrefs.GetInt("vs.hz120", 0) == 1;
            autoLooks = PlayerPrefs.GetInt("vs.auto", 1) == 1; coach = PlayerPrefs.GetInt("vs.coach", 1) == 1;
            var f = PlayerPrefs.GetString("vs.favs", "0,2"); var parts = f.Split(','); var l = new System.Collections.Generic.List<int>();
            foreach (var p in parts) if (int.TryParse(p, out var v) && v >= 0 && v < Looks.NAMES.Length) l.Add(v); favs = l.ToArray();
        }
        public static void Save()
        {
            PlayerPrefs.SetInt("vs.level", level); PlayerPrefs.SetInt("vs.livery", livery); PlayerPrefs.SetInt("vs.plume", plume); PlayerPrefs.SetInt("vs.look", look);
            PlayerPrefs.SetInt("vs.atmos", atmos); PlayerPrefs.SetInt("vs.quality", quality); PlayerPrefs.SetInt("vs.sound", sound ? 1 : 0); PlayerPrefs.SetInt("vs.haptics", haptics ? 1 : 0);
            PlayerPrefs.SetInt("vs.hz120", hz120 ? 1 : 0); PlayerPrefs.SetInt("vs.auto", autoLooks ? 1 : 0); PlayerPrefs.SetInt("vs.coach", coach ? 1 : 0);
            PlayerPrefs.SetString("vs.favs", string.Join(",", favs)); PlayerPrefs.Save();
        }
        public static int Best(string levelId) => PlayerPrefs.GetInt("vs.best." + levelId, 0);
        public static void SetBest(string levelId, int v) { PlayerPrefs.SetInt("vs.best." + levelId, v); PlayerPrefs.Save(); }

        /// <summary>Applies the quality tier to the live URP asset, the renderer's SSAO feature, vegetation and the frame rate.</summary>
        public static void ApplyQuality(Vegetation veg)
        {
            var a = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float[] rs = { .78f, .9f, 1f }, sd = { 130, 190, 280 }, vd = { 520, 760, 1100 }, vs = { 80, 150, 230 }; int[] msaa = { 1, 1, 2 };
            if (a != null) { a.renderScale = rs[quality]; a.shadowDistance = sd[quality]; a.msaaSampleCount = msaa[quality]; a.shadowCascadeCount = quality == 0 ? 2 : 4; }
            var rd = Resources.Load<UniversalRendererData>("Rendering/VSRenderer");
            #if UNITY_WEBGL
            bool ao = quality > 1;   // browser: screen-space AO shimmers on WebGL, keep it for the sharpest tier only
#else
            bool ao = quality > 0;
#endif
            if (rd != null) foreach (var f in rd.rendererFeatures) if (f != null && f.name.Contains("SSAO")) f.SetActive(ao);
            if (veg != null) { veg.drawDistance = vd[quality]; veg.shadowDistance = vs[quality]; veg.lodDistance = new[] { 150f, 230f, 330f }[quality]; }
            Application.targetFrameRate = hz120 ? 120 : 60; QualitySettings.vSyncCount = 0;
            Haptics.enabled = haptics;
        }
    }
}
