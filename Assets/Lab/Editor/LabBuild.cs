using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lab.EditorTools
{
    /// <summary>Batchmode entry points (-executeMethod Lab.EditorTools.LabBuild.X). The one scene is an empty shell; LabApp builds everything.</summary>
    public static class LabBuild
    {
        public const string ScenePath = "Assets/Lab/Scenes/Main.unity";

        public static void EnsureScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "TownWalker";
            PlayerSettings.companyName = "MindAttic";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultScreenWidth = 585;
            PlayerSettings.defaultScreenHeight = 1266;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
        }

        public static void CompileCheck() { EnsureScene(); Debug.Log("[Build] compile ok"); if (Application.isBatchMode) EditorApplication.Exit(0); }

        public static void BuildWindowsDev() => Build(BuildTarget.StandaloneWindows64, "Build/Windows/TownWalker.exe", true);

        public static void BuildWindowsRelease() => Build(BuildTarget.StandaloneWindows64, "Build/WindowsRelease/TownWalker.exe", false);

        public static void BuildWebGL()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            PlayerSettings.defaultWebScreenWidth = 585;
            PlayerSettings.defaultWebScreenHeight = 1266;
            Build(BuildTarget.WebGL, "Build/WebGL", true);
        }

        private static void Build(BuildTarget target, string output, bool dev)
        {
            EnsureScene();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output, target = target,
                options = dev ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[Build] {s.result} {target} → {output} size={s.totalSize} errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
