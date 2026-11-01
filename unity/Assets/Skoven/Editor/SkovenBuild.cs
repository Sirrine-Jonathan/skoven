#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Skoven.Editor;

/// <summary>
/// Dedicated Meta Quest APK builder for Skoven.
/// Programmatically generates the scene hierarchy if missing,
/// configures PlayerSettings for Quest 3 (ARM64, Linear, API 29..32),
/// and builds the installable Quest APK.
/// </summary>
public static class SkovenBuild
{
    public const string PackageName    = "com.sirrine.skoven";
    public const string ProductName    = "Skoven";
    public const string AppId          = "1122808136998612";
    public const string VersionString  = "0.1.0";
    public const int VersionCode       = 1;

    [MenuItem("Skoven/Build Meta Quest APK")]
    public static void BuildAPK()
    {
        string outputPath = GetArg("-customBuildPath") ?? "Builds/Skoven.apk";
        BuildInternal(outputPath, buildAppBundle: false);
    }

    [MenuItem("Skoven/Build Development APK")]
    public static void BuildDevelopmentApk()
    {
        string outputPath = GetArg("-customBuildPath") ?? "Builds/Skoven.apk";
        BuildInternal(outputPath, buildAppBundle: false);
    }

    [MenuItem("Skoven/Build Meta Quest AAB (Store Bundle)")]
    public static void BuildAAB()
    {
        string outputPath = GetArg("-customBuildPath") ?? "Builds/Skoven.aab";
        BuildInternal(outputPath, buildAppBundle: true);
    }

    private static void BuildInternal(string outputPath, bool buildAppBundle)
    {
        Debug.Log($"=== Starting Skoven Meta Quest Build (v{VersionString} code {VersionCode}) → {outputPath} ===");

        // 1. Ensure scene exists or rebuild fresh
        if (!File.Exists(SkovenProjectSetup.ScenePath))
        {
            Debug.Log("[SkovenBuild] Scene file not found. Generating fresh scene...");
            SkovenProjectSetup.BuildScene();
        }

        // 2. Configure target
        EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = buildAppBundle;

        // 3. Configure Player Settings for Quest 3
        PlayerSettings.companyName                     = "Sirrine-Jonathan";
        PlayerSettings.productName                     = ProductName;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
        PlayerSettings.bundleVersion                   = VersionString;
        PlayerSettings.Android.bundleVersionCode       = VersionCode;

        PlayerSettings.colorSpace                      = ColorSpace.Linear;
        PlayerSettings.Android.minSdkVersion           = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion        = (AndroidSdkVersions)32;
        PlayerSettings.Android.targetArchitectures     = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.Android.useCustomKeystore       = false;

        Debug.Log($"[SkovenBuild] App ID: {AppId} | Package: {PackageName}");

        string parentDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes           = new[] { SkovenProjectSetup.ScenePath },
            locationPathName = outputPath,
            target           = BuildTarget.Android,
            options          = BuildOptions.None
        };

        Debug.Log($"[SkovenBuild] Invoking BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            float sizeMb = summary.totalSize / (1024f * 1024f);
            Debug.Log($"=== SKOVEN BUILD SUCCEEDED: {outputPath} ({sizeMb:F2} MB) in {summary.totalTime.TotalSeconds:F1}s ===");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
        else
        {
            Debug.LogError($"=== SKOVEN BUILD FAILED: {summary.totalErrors} errors ===");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    private static string? GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
#endif
