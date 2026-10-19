using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click itch.io packaging helpers for Cassette Dash!.
///   Tools ▸ Cassette Dash ▸ 1. Apply itch.io Player Settings
///   Tools ▸ Cassette Dash ▸ 2. Build Windows (itch.io)
/// Both can also run head-less:
///   Unity.exe -batchmode -quit -projectPath "…" -buildTarget Win64 \
///             -executeMethod CassetteBuildScript.BuildWindows -logFile build.log
/// </summary>
public static class CassetteBuildScript
{
    const string OutputDir = "Builds/Windows";
    const string ExeName = "CassetteDash.exe";
    const string AppId = "com.nico0622.cassettedash";

    [MenuItem("Tools/Cassette Dash/1. Apply itch.io Player Settings")]
    public static void ApplyPublishSettingsMenu()
    {
        ApplyPublishSettings();
    }

    [MenuItem("Tools/Cassette Dash/2. Build Windows (itch.io)")]
    public static void BuildWindowsMenu()
    {
        BuildWindows();
    }

    /// <summary>Publish-friendly player settings (windowed 1280×720, resizable, own company/app id).</summary>
    public static void ApplyPublishSettings()
    {
        PlayerSettings.companyName = "nico0622";
        PlayerSettings.productName = "Cassette Dash!";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);

        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        AssetDatabase.SaveAssets();
        Debug.Log("[CassetteBuild] player settings applied (company=nico0622, 1280x720 windowed, appId=" + AppId + ")");
    }

    public static void BuildWindows()
    {
        ApplyPublishSettings();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        {
            Debug.Log("[CassetteBuild] switching active build target to StandaloneWindows64 …");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        }

        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s != null && s.enabled && !string.IsNullOrEmpty(s.path)) scenes.Add(s.path);
        if (scenes.Count == 0) scenes.Add("Assets/Scenes/SampleScene.unity");

        Directory.CreateDirectory(OutputDir);
        string outPath = Path.Combine(OutputDir, ExeName);

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.CompressWithLz4HC
        };

        Debug.Log("[CassetteBuild] building " + scenes.Count + " scene(s) -> " + outPath);
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        BuildSummary summary = report.summary;

        Debug.Log(string.Format("[CassetteBuild] result={0} size={1:F1} MB time={2} errors={3}",
            summary.result, summary.totalSize / (1024f * 1024f), summary.totalTime, summary.totalErrors));

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[CassetteBuild] BUILD FAILED: " + summary.result);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[CassetteBuild] build OK -> " + summary.outputPath);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}