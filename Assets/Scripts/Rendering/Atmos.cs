using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidStunts
{
    /// <summary>Sky, sun, fog, ambient, window glow and post settings per atmosphere. DAWN is the default.</summary>
    public class Atmos : MonoBehaviour
    {
        public struct Preset
        {
            public string name; public Color top, mid, hor, fog, sun, windowTint; public float sunI, fogDensity, cloud, stars, windowGlow, exposure, bloom; public Vector3 dir;
        }
        public static readonly Preset[] PRESETS = {
            new Preset { name = "STUDIO", top = H("#8e8e8b"), mid = H("#b3b3af"), hor = H("#c9c9c5"), fog = H("#c4c4c0"), sun = H("#fffaf2"), sunI = 2.6f, fogDensity = .0011f, dir = new Vector3(-.42f, .78f, -.46f), cloud = .2f, windowGlow = .25f, windowTint = H("#ffd2a0"), exposure = 0f, bloom = .35f },
            new Preset { name = "DAWN", top = H("#71819a"), mid = H("#cbaaa0"), hor = H("#efcaa8"), fog = H("#e0c3ad"), sun = H("#ffcf9f"), sunI = 2.4f, fogDensity = .0014f, dir = new Vector3(-.62f, .32f, -.71f), cloud = .55f, windowGlow = .55f, windowTint = H("#ffd2a0"), exposure = .1f, bloom = .5f },
            new Preset { name = "DUSK", top = H("#22304a"), mid = H("#6c586a"), hor = H("#dc8a58"), fog = H("#8a6a66"), sun = H("#ff9858"), sunI = 2.1f, fogDensity = .0018f, dir = new Vector3(.7f, .16f, -.68f), cloud = .6f, windowGlow = 1.4f, windowTint = H("#ffc890"), exposure = .15f, bloom = .7f },
            new Preset { name = "NIGHT", top = H("#04060b"), mid = H("#0a0f18"), hor = H("#18202d"), fog = H("#121823"), sun = H("#9db2ff"), sunI = .45f, fogDensity = .0022f, dir = new Vector3(-.3f, .82f, .42f), cloud = .3f, stars = 1.2f, windowGlow = 2.4f, windowTint = H("#ffc68a"), exposure = .35f, bloom = 1f },
            new Preset { name = "HAZE", top = H("#d6d6d2"), mid = H("#e0e0dc"), hor = H("#ebebe7"), fog = H("#e5e5e1"), sun = H("#ffffff"), sunI = 1.6f, fogDensity = .0045f, dir = new Vector3(-.2f, .9f, -.3f), cloud = .1f, windowGlow = .2f, windowTint = H("#ffd2a0"), exposure = .05f, bloom = .3f },
        };
        static Color H(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public Light sun; public Material sky; public int current = 1;
        ReflectionProbe probe; VolumeProfile profile; Bloom bloom; ColorAdjustments color; Vignette vig;

        public void Init()
        {
            var sg = new GameObject("Sun"); sg.transform.SetParent(transform, false);
            sun = sg.AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = .92f; sun.shadowBias = .04f; sun.shadowNormalBias = .4f;
            RenderSettings.sun = sun;
            sky = new Material(Lib.Sky); sky.SetTexture("_NoiseMap", Lib.Noise); RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            // reflections: a realtime probe that sees only the sky, refreshed when the atmosphere changes
            var pg = new GameObject("SkyProbe"); pg.transform.SetParent(transform, false);
            probe = pg.AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing; probe.size = new Vector3(20000, 4000, 20000); probe.resolution = 128; probe.cullingMask = 0; probe.clearFlags = ReflectionProbeClearFlags.Skybox; probe.importance = 0; probe.hdr = true;
            // post: bloom, neutral tonemapping, colour, a whisper of vignette
            var vg = new GameObject("Post"); vg.transform.SetParent(transform, false);
            var vol = vg.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 1;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = profile;
            bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.0f); bloom.scatter.Override(.68f); bloom.intensity.Override(.5f);
            var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            color = profile.Add<ColorAdjustments>(true); color.contrast.Override(6f); color.saturation.Override(-4f);
            vig = profile.Add<Vignette>(true); vig.intensity.Override(.18f); vig.smoothness.Override(.5f);
        }

        public void Apply(int i)
        {
            current = i; var p = PRESETS[i];
            sky.SetColor("_Top", p.top); sky.SetColor("_Mid", p.mid); sky.SetColor("_Hor", p.hor); sky.SetColor("_SunColor", p.sun);
            sky.SetVector("_SunDir", p.dir.normalized); sky.SetFloat("_Cloud", p.cloud); sky.SetFloat("_Stars", p.stars);
            sun.color = p.sun; sun.intensity = p.sunI; sun.transform.rotation = Quaternion.LookRotation(-p.dir.normalized);
            RenderSettings.fogColor = p.fog; RenderSettings.fogDensity = p.fogDensity;
            Shader.SetGlobalFloat("_VS_WindowGlow", p.windowGlow); Shader.SetGlobalColor("_VS_WindowTint", p.windowTint);
            color.postExposure.Override(p.exposure); bloom.intensity.Override(p.bloom);
            DynamicGI.UpdateEnvironment(); probe.RenderProbe();
        }
    }
}
