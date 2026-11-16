using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace AutoSceneVR.Editor
{
    public static class RoomSceneBuilder
    {
        public const string ScenePath = "Assets/AutoScene/Scenes/PhotoRoom.unity";

        [MenuItem("AutoScene/Rebuild photo room")]
        public static void Generate()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Assets/AutoScene/Scenes");
            var sim = RoomFactory.Create();
            EditorSceneManager.SaveScene(sim.gameObject.scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "AutoSceneResearch";
            PlayerSettings.productName = "Room Intention Lab";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            QualitySettings.vSyncCount = 0;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sim.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("AUTOSCENE_GENERATED " + ScenePath);
        }

        [MenuItem("AutoScene/Configure Android phone VR")]
        public static void ConfigurePhone()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "org.autoscene.roomlab");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.optimizedFramePacing = false;
            PlayerSettings.Android.androidTVCompatibility = false;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.forceInternetPermission = true;
            var all = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (all == null) {
                var perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
                var general = ScriptableObject.CreateInstance<XRGeneralSettings>();
                var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                AssetDatabase.AddObjectToAsset(general, perTarget);
                AssetDatabase.AddObjectToAsset(manager, perTarget);
                general.Manager = manager;
                perTarget.SetSettingsForBuildTarget(BuildTargetGroup.Android, general);
                all = general;
            }
            all.InitManagerOnStart = true;
            all.Manager.automaticLoading = true;
            all.Manager.automaticRunning = true;
            if (!XRPackageMetadataStore.AssignLoader(all.Manager, "Google.XR.Cardboard.XRLoader", BuildTargetGroup.Android))
                throw new Exception("Could not assign the Cardboard loader");
            EditorUtility.SetDirty(all); EditorUtility.SetDirty(all.Manager);
            AssetDatabase.SaveAssets();
            Debug.Log("AUTOSCENE_PHONE_CONFIGURED");
        }

        [MenuItem("AutoScene/Build Windows demo")]
        public static void BuildWindows()
        {
            EnsureScene();
            string path = Path.GetFullPath("Builds/Windows/RoomIntentionLab.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Build(path, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        }

        [MenuItem("AutoScene/Build Android phone demo")]
        public static void BuildPhone()
        {
            EnsureScene(); ConfigurePhone();
            string path = Path.GetFullPath("Builds/Android/RoomIntentionLab.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Build(path, BuildTarget.Android, BuildOptions.Development);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath)) Generate();
            AssetDatabase.SaveAssets();
        }

        static void Build(string path, BuildTarget target, BuildOptions options)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = path, target = target, options = options
            });
            Debug.Log("AUTOSCENE_BUILD " + report.summary.result + " " + path);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
        }
    }
}
