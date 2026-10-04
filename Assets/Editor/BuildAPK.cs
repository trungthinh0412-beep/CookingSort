using UnityEditor;
using UnityEngine;
using System.Linq;

public class BuildAPK {
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
        
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;
        
        if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) {
            Debug.Log($"BUILD_APK_SUCCESS: {summary.totalSize} bytes");
        } else {
            Debug.LogError("BUILD_APK_FAILED");
        }
    }
}
