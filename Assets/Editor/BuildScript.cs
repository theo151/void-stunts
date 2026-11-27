using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_IOS
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace VoidStunts.EditorTools
{
    /// <summary>
    /// One entry point for the cloud build: configures the project from scratch (URP asset + renderer with SSAO and the Looks pass,
    /// player settings, icon, scene) and exports the Xcode project. Run with
    ///   -executeMethod VoidStunts.EditorTools.BuildScript.BuildIOS
    /// Environment: BUNDLE_ID (default com.voidstunts.game), BUILD_NUMBER (CI run number), APPLE_TEAM_ID, OUTPUT (default Builds/iOS).
    /// </summary>
    public static class BuildScript
    {
        const string RenderDir = "Assets/Resources/Rendering";
        public const string Version = "0.10.0";

        [MenuItem("Void Stunts/Configure Project")]
        public static void Configure()
        {
            Directory.CreateDirectory(RenderDir);
            var rendererPath = RenderDir + "/VSRenderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rd == null)
            {
                // URP's own factory: fills postProcessData and the shader resources
                var create = typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
                rd = (UniversalRendererData)create.Invoke(null, new object[] { rendererPath, RendererType.UniversalRenderer, false, "Renderer" });
            }
            rd.renderingMode = RenderingMode.ForwardPlus; rd.depthPrimingMode = DepthPrimingMode.Disabled; rd.copyDepthMode = CopyDepthMode.AfterOpaques;
            // features: SSAO (toggled by quality tier) and the Looks full-screen pass
            if (!rd.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion))
            {
                var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ao.name = "SSAO"; AddFeature(rd, ao);
                var so = new SerializedObject(ao); var st = so.FindProperty("m_Settings");
                if (st != null)
                {
                    Set(st, "Intensity", .9f); Set(st, "Radius", .45f); Set(st, "DirectLightingStrength", .2f); Set(st, "Falloff", 120f);
                    var ds = st.FindPropertyRelative("Downsample"); if (ds != null) ds.boolValue = true;
                    var m = st.FindPropertyRelative("AOMethod"); if (m != null) m.enumValueIndex = 1; // interleaved gradient: cheaper on mobile
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            var looksMatPath = RenderDir + "/Looks.mat";
            var looksMat = AssetDatabase.LoadAssetAtPath<Material>(looksMatPath);
            if (looksMat == null) { looksMat = new Material(Shader.Find("VoidStunts/Looks")); AssetDatabase.CreateAsset(looksMat, looksMatPath); }
            if (!rd.rendererFeatures.Any(f => f is FullScreenPassRendererFeature))
            {
                var fs = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>(); fs.name = "Looks";
                fs.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing; fs.fetchColorBuffer = true;
                fs.requirements = ScriptableRenderPassInput.Depth; fs.passMaterial = looksMat; fs.passIndex = 0;
                AddFeature(rd, fs);
            }
            EditorUtility.SetDirty(rd);

            var assetPath = RenderDir + "/VSPipeline.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null) { urp = UniversalRenderPipelineAsset.Create(rd); AssetDatabase.CreateAsset(urp, assetPath); }
            var u = new SerializedObject(urp);
            SetB(u, "m_SupportsHDR", true); SetI(u, "m_HDRColorBufferPrecision", 0); SetI(u, "m_MSAA", 1); SetF(u, "m_RenderScale", .9f);
            SetB(u, "m_RequireDepthTexture", true); SetB(u, "m_RequireOpaqueTexture", false);
            SetI(u, "m_MainLightRenderingMode", 1); SetB(u, "m_MainLightShadowsSupported", true); SetI(u, "m_MainLightShadowmapResolution", 2048);
            SetI(u, "m_AdditionalLightsRenderingMode", 1); SetB(u, "m_AdditionalLightShadowsSupported", false);
            SetF(u, "m_ShadowDistance", 190); SetI(u, "m_ShadowCascadeCount", 4); SetB(u, "m_SoftShadowsSupported", true); SetB(u, "m_AnyShadowsSupported", true);
            SetF(u, "m_ShadowDepthBias", 1.2f); SetF(u, "m_ShadowNormalBias", .8f);
            SetB(u, "m_UseSRPBatcher", true); SetB(u, "m_SupportsDynamicBatching", false); SetI(u, "m_ColorGradingMode", 1); SetI(u, "m_ColorGradingLutSize", 32);
            SetB(u, "m_ReflectionProbeBlending", false); SetB(u, "m_ReflectionProbeBoxProjection", false); SetB(u, "m_UseFastSRGBLinearConversion", true);
            u.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = urp; }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 0;
            var ensure = typeof(UniversalRenderPipelineGlobalSettings).GetMethod("Ensure", BindingFlags.NonPublic | BindingFlags.Static);
            try { ensure?.Invoke(null, new object[] { true }); } catch (Exception e) { Debug.LogWarning("URP global settings: " + e.Message); }

            ConfigurePlayer();
            ConfigureScene();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[VoidStunts] project configured");
        }

        static void AddFeature(ScriptableRendererData rd, ScriptableRendererFeature f)
        {
            AssetDatabase.AddObjectToAsset(f, rd); AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out _, out long id);
            var so = new SerializedObject(rd);
            var list = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = f;
            map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            so.ApplyModifiedPropertiesWithoutUndo(); rd.SetDirty(); EditorUtility.SetDirty(rd);
        }
        static void Set(SerializedProperty st, string n, float v) { var p = st.FindPropertyRelative(n); if (p != null) p.floatValue = v; }
        static void SetB(SerializedObject o, string n, bool v) { var p = o.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("missing " + n); }
        static void SetI(SerializedObject o, string n, int v) { var p = o.FindProperty(n); if (p == null) { Debug.LogWarning("missing " + n); return; } if (p.propertyType == SerializedPropertyType.Enum) p.intValue = v; else p.intValue = v; }
        static void SetF(SerializedObject o, string n, float v) { var p = o.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("missing " + n); }

        static void ConfigurePlayer()
        {
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.companyName = "Void Stunts"; PlayerSettings.productName = "Void Stunts";
            PlayerSettings.SetApplicationIdentifier(ios, Env("BUNDLE_ID", "com.voidstunts.game"));
            PlayerSettings.bundleVersion = Version; PlayerSettings.iOS.buildNumber = Env("BUILD_NUMBER", "1");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Low);
            PlayerSettings.SetArchitecture(ios, 1); // ARM64
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true; PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;
            PlayerSettings.accelerometerFrequency = 0;
            PlayerSettings.stripUnusedMeshComponents = false; // meshes are built at runtime
            PlayerSettings.SplashScreen.backgroundColor = new Color(.94f, .935f, .92f);
            var team = Env("APPLE_TEAM_ID", ""); if (team.Length > 0) { PlayerSettings.iOS.appleDeveloperTeamID = team; PlayerSettings.iOS.appleEnableAutomaticSigning = true; }
            // the game reads Input.touches: make sure the classic input manager is active
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null) { var so = new SerializedObject(ps); var p = so.FindProperty("activeInputHandler"); if (p != null) { p.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); } }
            // icon: one 1024 px master; Unity scales it for every slot
            AssetDatabase.ImportAsset("Assets/Icons/icon_1024.png", ImportAssetOptions.ForceUpdate);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/icon_1024.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            // car layer for raycast masks
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers"); if (layers != null && layers.arraySize > 8) { layers.GetArrayElementAtIndex(8).stringValue = "Car"; tm.ApplyModifiedPropertiesWithoutUndo(); }
        }

        static void ConfigureScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            const string path = "Assets/Scenes/Main.unity";
            if (!File.Exists(path))
            {
                var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("VoidStunts").AddComponent<Game>();
                EditorSceneManager.SaveScene(s, path);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }

        static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        /// <summary>CI entry point: configure, then export the Xcode project.</summary>
        public static void BuildIOS()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            Configure();
            var outDir = Env("OUTPUT", "Builds/iOS");
            var opts = new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = outDir, target = BuildTarget.iOS, targetGroup = BuildTargetGroup.iOS, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[VoidStunts] build {report.summary.result} · {report.summary.totalErrors} errors · {report.summary.totalSize / 1048576f:0.0} MB · {report.summary.totalTime}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Browser build for testing before Apple enrolment: same game, WebGL 2, served from GitHub Pages and playable in Safari on iPhone.
        /// Differences from iOS: no sound (the synth runs on the audio thread, which WebGL does not have) and no haptics.
        /// </summary>
        public static void BuildWebGL()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            Configure();
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // GitHub Pages does not send Content-Encoding headers
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            var outDir = Env("OUTPUT", "Builds/WebGL");
            var opts = new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), locationPathName = outDir, target = BuildTarget.WebGL, targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.None };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[VoidStunts] webgl build {report.summary.result} · {report.summary.totalErrors} errors · {report.summary.totalSize / 1048576f:0.0} MB");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>CI compile check: configures without building (fast, any runner).</summary>
        public static void Validate() { Configure(); Debug.Log("[VoidStunts] validate ok"); }

#if UNITY_IOS
        [PostProcessBuild(100)]
        public static void OnPostprocess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = Path.Combine(path, "Info.plist"); var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);   // no export-compliance prompt in TestFlight
            root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true); // allow 120 Hz ProMotion
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetString("UIUserInterfaceStyle", "Light");
            plist.WriteToFile(plistPath);
        }
#endif
    }
}
