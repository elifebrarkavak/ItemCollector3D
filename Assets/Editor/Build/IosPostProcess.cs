#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace ItemCollector3D.EditorTools
{
    /// <summary>
    /// Touches the generated Xcode project only: gives the app its home-screen name ("Item Collector 3D") in Info.plist and aligns the IL2CPP library's deployment target. The project-wide product name is
    /// deliberately left alone (it is shared with Android and decides the save-file folder). Compiled out unless the active build target is iOS, so it
    /// cannot affect Android, Standalone or the Editor.
    /// </summary>
    public static class IosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("CFBundleDisplayName", IosBuildSetup.DisplayName);
            plist.WriteToFile(plistPath);

            // Unity leaves the IL2CPP "GameAssembly" static library at a hard-coded iOS 15.0 while the app is built for the player setting (17.0). With the
            // current Xcode SDK that mismatch makes libc++ warn "selected platform is no longer supported" about 600 times: line the library up with the app.
            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            string gameAssembly = project.TargetGuidByName("GameAssembly");
            if (!string.IsNullOrEmpty(gameAssembly))
            {
                project.SetBuildProperty(gameAssembly, "IPHONEOS_DEPLOYMENT_TARGET", PlayerSettings.iOS.targetOSVersionString);
                project.WriteToFile(projectPath);
            }
        }
    }
}
#endif
