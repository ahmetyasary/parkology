using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Parkology.Editor
{
    /// <summary>Player settings, branding (no Unity splash) and the single bootstrap scene.</summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Parkology/Scenes/Parkology.unity";
        public const string BundleId = "com.smartlogy.parkology";

        [MenuItem("Parkology/Prepare Project")]
        public static void Prepare()
        {
            PlayerSettings.companyName = "Smartlogy";
            PlayerSettings.productName = "Parkology";
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1170;
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            Branding();

            System.IO.Directory.CreateDirectory("Assets/Parkology/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Parkology", typeof(App));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("PARKOLOGY_SETUP_OK");
        }

        /// <summary>No "Made with Unity" anywhere: the splash is off and our own art is the icon.</summary>
        [MenuItem("Parkology/Apply Branding")]
        public static void Branding()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.27f, 0.45f, 0.92f);
            AssetDatabase.ImportAsset("Assets/Resources/Art/app-icon.png", ImportAssetOptions.ForceUpdate);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Art/app-icon.png");
            if (icon == null) throw new System.Exception("App icon missing: run Tools/generate-art.swift");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var target in new[] { NamedBuildTarget.iOS, NamedBuildTarget.Android })
            {
                foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
                {
                    var slots = PlayerSettings.GetPlatformIcons(target, kind);
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var layers = new Texture2D[Mathf.Max(1, slots[i].maxLayerCount)];
                        for (int l = 0; l < layers.Length; l++) layers[l] = icon;
                        slots[i].SetTextures(layers);
                    }
                    PlayerSettings.SetPlatformIcons(target, kind, slots);
                }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
