using UnityEditor;
using UnityEngine;
using System.Linq;

public class BuildAPKMenu {
    [MenuItem("Tools/Build APK (Click Here)")]
    public static void Build() {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        string buildPath = "Build/SolitaireSort.apk";
        
        if (!System.IO.Directory.Exists("Build")) {
            System.IO.Directory.CreateDirectory("Build");
        }
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        
        Debug.Log("Starting APK Build...");
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;
        
        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) {
            Debug.Log($"BUILD APK SUCCESS: {summary.totalSize / 1024 / 1024} MB. Saved to: {buildPath}");
            EditorUtility.RevealInFinder(buildPath);
        } else {
            Debug.LogError("BUILD APK FAILED! Check console for reasons.");
        }
    }
}
