using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Parkology.Editor
{
    /// <summary>Batch-mode exports: -executeMethod Parkology.Editor.MobileBuild.ExportIOS (or ExportIOSSimulator / BuildAndroidAPK).</summary>
    public static class MobileBuild
    {
        public static void ExportIOS() => ExportIOSTo("Builds/iOS", iOSSdkVersion.DeviceSDK);

        public static void ExportIOSSimulator() => ExportIOSTo("Builds/iOSSimulator", iOSSdkVersion.SimulatorSDK);

        static void ExportIOSTo(string path, iOSSdkVersion sdk)
        {
            var previous = PlayerSettings.iOS.sdkVersion;
            var architecture = typeof(PlayerSettings.iOS).GetProperty("simulatorSdkArchitecture");
            object previousArch = architecture?.GetValue(null);
            try
            {
                PlayerSettings.iOS.sdkVersion = sdk;
                // Smartlogy team (same as Roller Sort); Xcode manages the provisioning profile.
                PlayerSettings.iOS.appleDeveloperTeamID = "9VSUBY2W33";
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
                // Each App Store Connect upload needs a higher build number: pass -buildNumber N.
                string build = Arg("-buildNumber");
                if (build != null) PlayerSettings.iOS.buildNumber = build;
                if (sdk == iOSSdkVersion.SimulatorSDK && architecture != null)
                {
                    string arm64 = System.Array.Find(System.Enum.GetNames(architecture.PropertyType), n => n.ToUpperInvariant().Contains("ARM64"));
                    if (arm64 != null) architecture.SetValue(null, System.Enum.Parse(architecture.PropertyType, arm64));
                }
                ProjectSetup.Branding();
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath },
                    locationPathName = path,
                    target = BuildTarget.iOS,
                    options = BuildOptions.None
                });
                if (result.summary.result != BuildResult.Succeeded) throw new System.Exception("iOS export failed: " + result.summary.result);
                Debug.Log("PARKOLOGY_IOS_EXPORT_OK " + path);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previous;
                if (architecture != null && previousArch != null) architecture.SetValue(null, previousArch);
            }
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static void BuildAndroidAPK()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            ProjectSetup.Branding();
            System.IO.Directory.CreateDirectory("Builds/Android");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/Android/Parkology.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (result.summary.result != BuildResult.Succeeded) throw new System.Exception("Android build failed: " + result.summary.result);
            Debug.Log("PARKOLOGY_ANDROID_APK_OK");
        }
    }
}
