using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// iOS test build of Item Collector 3D (a personal iPhone build, not a store release). One place that applies the iOS player settings, creates the iOS
    /// Build Profile and generates the Xcode project into the git-ignored <c>Builds/iOS</c> folder. Everything here only touches iOS settings (the iOS player
    /// settings, the iPhone graphics API list, the iOS Build Profile); the Android and Standalone settings are left exactly as they are. Signing is NOT set up
    /// here: the Apple team and certificate are chosen in Xcode.
    /// </summary>
    public static class IosBuildSetup
    {
        /// <summary>A unique, test-only bundle identifier for signing the app with a personal Apple account (changeable in Xcode).</summary>
        public const string BundleId = "com.elifebrarkavak.itemcollector3d";

        public const string DisplayName = "Item Collector 3D";
        public const string MinimumIosVersion = "17.0";   // the iPhone 15 Pro Max shipped with iOS 17.0
        public const string ProfilePath = "Assets/Settings/Build Profiles/iOS.asset";
        public const string OutputFolder = "Builds/iOS";

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/GameplayTest.unity",
        };

        // ================================================================ player settings

        /// <summary>Applies the iOS player settings (idempotent). Returns what the settings read after applying them.</summary>
        public static string ApplyPlayerSettings()
        {
            NamedBuildTarget ios = NamedBuildTarget.iOS;

            PlayerSettings.SetApplicationIdentifier(ios, BundleId);
            // NOT PlayerSettings.iOS.applicationDisplayName: that setter rewrites the project-wide productName (Android app name, save folder). The iOS home-screen
            // name is set in the generated Info.plist by IosPostProcess instead.
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = MinimumIosVersion;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

            // IL2CPP + ARM64 (the only combination iOS supports); Metal only: see the end of this method
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(ios, 1); // 1 = ARM64

            // landscape only (Left and Right). These flags are shared with Android and were already exactly this.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // the home indicator fades out and the bottom-edge swipe needs a second swipe, so a thumb on the joystick does not leave the game
            PlayerSettings.iOS.hideHomeButton = true;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.BottomEdge;

            // Metal only. Keep these two calls LAST: some of the iOS setters above reset the "use default graphics APIs" flag in memory, and a later Editor save
            // (for example when PlayMode tests run) would then write "automatic" back into ProjectSettings.asset.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

            AssetDatabase.SaveAssets();
            return Describe();
        }

        public static string Describe()
        {
            NamedBuildTarget ios = NamedBuildTarget.iOS;
            return $"bundle={PlayerSettings.GetApplicationIdentifier(ios)} productName='{PlayerSettings.productName}' build={PlayerSettings.iOS.buildNumber} " +
                   $"minOS={PlayerSettings.iOS.targetOSVersionString} device={PlayerSettings.iOS.targetDevice} sdk={PlayerSettings.iOS.sdkVersion} " +
                   $"backend={PlayerSettings.GetScriptingBackend(ios)} arch={PlayerSettings.GetArchitecture(ios)} defaultApis={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS)} " +
                   $"apis={string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS))} orientation={PlayerSettings.defaultInterfaceOrientation} " +
                   $"landscapeL={PlayerSettings.allowedAutorotateToLandscapeLeft} landscapeR={PlayerSettings.allowedAutorotateToLandscapeRight} " +
                   $"portrait={PlayerSettings.allowedAutorotateToPortrait}/{PlayerSettings.allowedAutorotateToPortraitUpsideDown} " +
                   $"hideHome={PlayerSettings.iOS.hideHomeButton} deferGestures={PlayerSettings.iOS.deferSystemGesturesMode} " +
                   $"camera='{PlayerSettings.iOS.cameraUsageDescription}' mic='{PlayerSettings.iOS.microphoneUsageDescription}' location='{PlayerSettings.iOS.locationUsageDescription}'";
        }

        // ================================================================ build profile

        /// <summary>Creates the iOS Build Profile asset (once) and gives it the two game scenes. Needs the iOS module to be loaded by the Editor.</summary>
        public static string EnsureBuildProfile()
        {
            BuildProfile profile = BuildProfile.GetBuildProfileAtPath(ProfilePath);
            if (profile == null)
            {
                GUID iosPlatform = default;
                bool found = false;
                foreach (InstalledPlatformInfo platform in BuildProfile.GetInstalledPlatformModules())
                {
                    if (platform.displayName == "iOS")
                    {
                        iosPlatform = platform.platformGuid;
                        found = true;
                    }
                }

                if (!found)
                {
                    return "iOS platform module not loaded by this Editor (restart the Editor after installing iOS Build Support)";
                }

                BuildProfile.CreateBuildProfile(iosPlatform, "iOS", null); // no callback: a persistent UnityEvent listener cannot be a lambda
                AssetDatabase.Refresh();
                profile = BuildProfile.GetBuildProfileAtPath(ProfilePath);
                if (profile == null)
                {
                    return "the iOS Build Profile was requested but is not at " + ProfilePath + " yet; call EnsureBuildProfile again";
                }
            }

            var scenes = new EditorBuildSettingsScene[Scenes.Length];
            for (int i = 0; i < Scenes.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(Scenes[i], true);
            }

            profile.overrideGlobalScenes = true;
            profile.scenes = scenes;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return $"profile '{profile.name}' at {ProfilePath}: overrideGlobalScenes={profile.overrideGlobalScenes} scenes={string.Join(", ", Array.ConvertAll(profile.scenes, s => s.path))}";
        }

        // ================================================================ xcode project

        /// <summary>
        /// Generates the Xcode project with the iOS Build Profile. Batch entry point:
        /// <c>Unity -batchmode -projectPath P -executeMethod ItemCollector3D.EditorTools.IosBuildSetup.BuildXcodeProject -ic3dOutput /path/Builds/iOS -quit</c>.
        /// Exits with 0 on success.
        /// </summary>
        public static void BuildXcodeProject()
        {
            string output = OutputFolder;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ic3dOutput")
                {
                    output = args[i + 1];
                }
            }

            BuildProfile profile = BuildProfile.GetBuildProfileAtPath(ProfilePath);
            if (profile == null)
            {
                Debug.LogError("IosBuildSetup: no iOS Build Profile at " + ProfilePath);
                EditorApplication.Exit(2);
                return;
            }

            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = output,
                options = BuildOptions.None,
            });

            BuildSummary summary = report.summary;
            Debug.Log($"IosBuildSetup: result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} output={summary.outputPath} time={summary.totalTime}");
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
