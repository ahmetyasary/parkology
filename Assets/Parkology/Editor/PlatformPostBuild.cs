using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace Parkology.Editor
{
    /// <summary>
    /// iOS: links CoreHaptics and installs a branded launch storyboard (sky + logo) that stays up until the
    /// in-game splash renders, via PKLaunchScene.mm. Android: forces Unity's splash off in the manifest.
    /// </summary>
    public static class IOSPostBuild
    {
        [PostProcessBuild(100)]
        public static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreHaptics.framework", false);

            File.Copy("Assets/Resources/Art/splash-bg.png", Path.Combine(path, "PKSplashBackground.png"), true);
            File.Copy("Assets/Resources/Art/logo.png", Path.Combine(path, "PKSplashLogo.png"), true);
            string main = project.GetUnityMainTargetGuid();
            foreach (string file in new[] { "PKSplashBackground.png", "PKSplashLogo.png" })
            {
                string guid = project.FindFileGuidByProjectPath(file);
                if (string.IsNullOrEmpty(guid)) guid = project.AddFile(file, file, PBXSourceTree.Source);
                project.AddFileToBuild(main, guid);
            }
            const string storyboard = "PKLaunchScreen.storyboard";
            File.WriteAllText(Path.Combine(path, storyboard), Storyboard);
            string sbGuid = project.FindFileGuidByProjectPath(storyboard);
            if (string.IsNullOrEmpty(sbGuid)) sbGuid = project.AddFile(storyboard, storyboard, PBXSourceTree.Source);
            project.AddFileToBuild(main, sbGuid);
            project.WriteToFile(projectPath);

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            foreach (string key in new[] { "UILaunchStoryboardName", "UILaunchStoryboardName~iphone", "UILaunchStoryboardName~ipod", "UILaunchStoryboardName~ipad" })
                plist.root.SetString(key, "PKLaunchScreen");
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            if (plist.root["UIApplicationSceneManifest"] is PlistElementDict manifest &&
                manifest["UISceneConfigurations"] is PlistElementDict configs &&
                configs["UIWindowSceneSessionRoleApplication"] is PlistElementArray roles)
            {
                foreach (var entry in roles.values)
                {
                    entry.AsDict().SetString("UISceneDelegateClassName", "PKLaunchScene");
                    entry.AsDict().SetString("UISceneStoryboardFile", "PKLaunchScreen");
                }
            }
            else Debug.LogWarning("No UIScene manifest: branded launch hand-off skipped (static launch screen still used).");
            plist.WriteToFile(plistPath);
            foreach (string device in new[] { "iPhone", "iPad" })
                File.WriteAllText(Path.Combine(path, "LaunchScreen-" + device + ".storyboard"), Storyboard);
        }

        // Sky background fills the screen; the logo is 88.9% of the width, centred 32% from the top — the same
        // placement App.Splash uses, so the hand-off is seamless.
        const string Storyboard = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<document type=""com.apple.InterfaceBuilder3.CocoaTouch.Storyboard.XIB"" version=""3.0"" toolsVersion=""15702"" targetRuntime=""iOS.CocoaTouch"" propertyAccessControl=""none"" useAutolayout=""YES"" launchScreen=""YES"" useTraitCollections=""YES"" colorMatched=""YES"" initialViewController=""pk-controller"">
 <device id=""retina6_7"" orientation=""portrait"" appearance=""light""/>
 <dependencies><deployment identifier=""iOS""/><plugIn identifier=""com.apple.InterfaceBuilder.IBCocoaTouchPlugin"" version=""15704""/><capability name=""documents saved in the Xcode 8 format"" minToolsVersion=""8.0""/></dependencies>
 <scenes><scene sceneID=""pk-scene""><objects><viewController id=""pk-controller"" sceneMemberID=""viewController""><view key=""view"" contentMode=""scaleToFill"" id=""pk-root""><rect key=""frame"" x=""0"" y=""0"" width=""430"" height=""932""/><autoresizingMask key=""autoresizingMask"" widthSizable=""YES"" heightSizable=""YES""/>
 <subviews>
 <imageView userInteractionEnabled=""NO"" contentMode=""scaleAspectFill"" image=""PKSplashBackground.png"" translatesAutoresizingMaskIntoConstraints=""NO"" id=""pk-background""><rect key=""frame"" x=""0"" y=""0"" width=""430"" height=""932""/></imageView>
 <imageView userInteractionEnabled=""NO"" contentMode=""scaleAspectFit"" image=""PKSplashLogo.png"" translatesAutoresizingMaskIntoConstraints=""NO"" id=""pk-logo""><rect key=""frame"" x=""24"" y=""248"" width=""382"" height=""100""/><constraints><constraint firstAttribute=""height"" secondItem=""pk-logo"" secondAttribute=""width"" multiplier=""0.2625"" id=""logo-aspect""/></constraints></imageView>
 </subviews><color key=""backgroundColor"" red=""0.35"" green=""0.55"" blue=""0.96"" alpha=""1"" colorSpace=""custom"" customColorSpace=""sRGB""/>
 <constraints>
 <constraint firstItem=""pk-background"" firstAttribute=""top"" secondItem=""pk-root"" secondAttribute=""top"" id=""bg-top""/><constraint firstItem=""pk-background"" firstAttribute=""bottom"" secondItem=""pk-root"" secondAttribute=""bottom"" id=""bg-bottom""/><constraint firstItem=""pk-background"" firstAttribute=""leading"" secondItem=""pk-root"" secondAttribute=""leading"" id=""bg-leading""/><constraint firstItem=""pk-background"" firstAttribute=""trailing"" secondItem=""pk-root"" secondAttribute=""trailing"" id=""bg-trailing""/>
 <constraint firstItem=""pk-logo"" firstAttribute=""centerX"" secondItem=""pk-root"" secondAttribute=""centerX"" id=""logo-x""/><constraint firstItem=""pk-logo"" firstAttribute=""centerY"" secondItem=""pk-root"" secondAttribute=""centerY"" multiplier=""0.64"" id=""logo-y""/><constraint firstItem=""pk-logo"" firstAttribute=""width"" secondItem=""pk-root"" secondAttribute=""width"" multiplier=""0.888889"" id=""logo-width""/>
 </constraints></view></viewController><placeholder placeholderIdentifier=""IBFirstResponder"" id=""pk-responder"" sceneMemberID=""firstResponder""/></objects></scene></scenes>
 <resources><image name=""PKSplashBackground.png"" width=""1080"" height=""2340""/><image name=""PKSplashLogo.png"" width=""1600"" height=""420""/></resources>
</document>";
    }

    public sealed class AndroidPostBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifest)) return;
            string xml = File.ReadAllText(manifest);
            xml = Regex.Replace(xml, "(<meta-data[^>]*android:name=\"unity\\.splash-enable\"[^>]*android:value=\")[^\"]*(\")", "$1false$2");
            File.WriteAllText(manifest, xml);
        }
    }
}
