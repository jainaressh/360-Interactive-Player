using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace Interactive360.EditorTools
{
    /// <summary>
    /// One-click project setup so the repo can be bootstrapped from code alone:
    /// Android/Quest player settings, XR Management + OpenXR (Meta Quest), the panorama material and the Main scene.
    /// Also exposes batch-mode entry points used by tools/unity-setup.ps1 and tools/unity-build.ps1.
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string MaterialPath = "Assets/Materials/Panorama360.mat";
        public const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        public const string DefaultApkPath = "Builds/Interactive360.apk";

        const string OpenXrLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string MetaQuestFeatureId = "com.unity.openxr.feature.metaquest";
        const string OculusTouchFeatureId = "com.unity.openxr.feature.input.oculustouch";
        const string MetaQuestFeatureSetId = "com.unity.openxr.featureset.metaquest";

        // ------------------------------------------------------------------ menu

        [MenuItem("Interactive360/Setup Project (settings + XR + scene)", priority = 0)]
        public static void Setup()
        {
            ConfigurePlayerSettings();
            ConfigureXr();
            BuildScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Bootstrap] Setup complete. Scene: " + ScenePath);
        }

        [MenuItem("Interactive360/Build Android APK", priority = 1)]
        public static void BuildAndroidMenu() => BuildApk(DefaultApkPath);

        // ------------------------------------------------------------------ batch entry points

        public static void SetupFromCommandLine()
        {
            try { Setup(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                var report = BuildApk(ArgValue("-apkPath") ?? DefaultApkPath);
                EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void SetupAndBuildFromCommandLine()
        {
            try
            {
                Setup();
                var report = BuildApk(ArgValue("-apkPath") ?? DefaultApkPath);
                EditorApplication.Exit(report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        // ------------------------------------------------------------------ player settings

        public static void ConfigurePlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = "Cavin Infotech";
            PlayerSettings.productName = "Interactive 360";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(android, "com.cavininfotech.interactive360");

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.optimizedFramePacing = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.forceInternetPermission = true; // allows http(s) clip URLs
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC };

            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetMobileMTRendering(android, true);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.stripEngineCode = true;

            SetActiveInputHandler(2); // 0 = legacy, 1 = Input System, 2 = both
            QualitySettings.vSyncCount = 0;

            Debug.Log("[Bootstrap] Player settings configured for Quest (Android/ARM64/IL2CPP/Vulkan).");
        }

        static void SetActiveInputHandler(int value)
        {
            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != value)
            {
                prop.intValue = value;
                so.ApplyModifiedProperties();
            }
        }

        // ------------------------------------------------------------------ XR

        public static void ConfigureXr()
        {
            XRGeneralSettingsPerBuildTarget perTarget;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget);
            if (perTarget == null)
            {
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
                if (perTarget == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(XrSettingsPath));
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }

            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var settings = perTarget.SettingsForBuildTarget(group);
                if (settings == null)
                {
                    perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                    settings = perTarget.SettingsForBuildTarget(group);
                }
                if (settings == null) { Debug.LogWarning("[Bootstrap] Could not create XR settings for " + group); continue; }

                settings.InitManagerOnStart = true;
                if (settings.Manager == null)
                {
                    var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    manager.name = group + " Providers";
                    AssetDatabase.AddObjectToAsset(manager, perTarget);
                    settings.Manager = manager;
                }

                bool assigned = XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXrLoaderType, group);
                Debug.Log("[Bootstrap] OpenXR loader for " + group + ": " + (assigned ? "assigned" : "already present / failed"));
                EditorUtility.SetDirty(settings);
                EditorUtility.SetDirty(settings.Manager);

                FeatureHelpers.RefreshFeatures(group);
                if (group == BuildTargetGroup.Android)
                {
                    var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaQuestFeatureSetId);
                    if (featureSet != null)
                    {
                        featureSet.isEnabled = true;
                        OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
                    }
                    EnableFeature(group, MetaQuestFeatureId);
                }
                EnableFeature(group, OculusTouchFeatureId);

                var openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (openXr != null)
                {
                    openXr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                    openXr.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
                    EditorUtility.SetDirty(openXr);
                }
            }

            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bootstrap] XR configured: OpenXR + Meta Quest Support + Oculus Touch profile.");
        }

        static void EnableFeature(BuildTargetGroup group, string featureId)
        {
            var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(group, featureId);
            if (feature == null) { Debug.LogWarning("[Bootstrap] OpenXR feature not found for " + group + ": " + featureId); return; }
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        // ------------------------------------------------------------------ scene

        public static Material GetOrCreatePanoramaMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) throw new InvalidOperationException("Skybox/Panoramic shader not found");
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            mat = new Material(shader) { name = "Panorama360" };
            mat.SetFloat("_Mapping", 1f);
            mat.SetFloat("_ImageType", 0f);
            mat.SetFloat("_Layout", 0f);
            mat.SetFloat("_MirrorOnBack", 0f);
            mat.SetFloat("_Exposure", 1f);
            mat.SetFloat("_Rotation", 0f);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        public static void BuildScene()
        {
            var material = GetOrCreatePanoramaMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Rig: XR Origin / Camera Offset / Main Camera, controllers as siblings of the camera.
            var origin = new GameObject("XR Origin");
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(origin.transform, false);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(offset.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 75f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<HeadTracking>();
            camGo.AddComponent<DesktopLook>();

            var gazeGo = new GameObject("Gaze Pointer");
            gazeGo.transform.SetParent(camGo.transform, false);
            var gaze = gazeGo.AddComponent<GazePointer>();
            gaze.head = cam;

            foreach (var hand in new[] { ControllerPointer.Hand.Left, ControllerPointer.Hand.Right })
            {
                var go = new GameObject(hand + " Controller");
                go.transform.SetParent(offset.transform, false);
                var pointer = go.AddComponent<ControllerPointer>();
                pointer.hand = hand;
            }

            // Player.
            var root = new GameObject("Interactive360");
            var pool = root.AddComponent<VideoPool>();
            var skybox = root.AddComponent<PanoramaSkybox>();
            skybox.template = material;
            var ui = root.AddComponent<ChoiceUI>();
            var player = root.AddComponent<StoryPlayer>();
            player.pool = pool;
            player.skybox = skybox;
            player.ui = ui;
            player.head = cam;

            RenderSettings.skybox = material;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            RenderSettings.fog = false;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Bootstrap] Scene written to " + ScenePath);
        }

        // ------------------------------------------------------------------ build

        public static BuildReport BuildApk(string apkPath)
        {
            if (!File.Exists(ScenePath)) BuildScene();
            var dir = Path.GetDirectoryName(apkPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log("[Bootstrap] Build " + s.result + ": " + apkPath + " (" + (s.totalSize / (1024 * 1024)) + " MB, " + s.totalTime.TotalMinutes.ToString("0.0") + " min, " + s.totalErrors + " errors)");
            return report;
        }
    }
}
