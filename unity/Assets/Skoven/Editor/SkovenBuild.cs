using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Skoven build automation — called by build-and-deploy.sh via Unity batchmode.
///
/// Usage (from build-and-deploy.sh):
///   Unity -batchmode -executeMethod SkovenBuild.BuildDevelopmentApk
///         -customBuildPath /path/to/Skoven-dev.apk
/// </summary>
public static class SkovenBuild
{
    private const string PackageName    = "com.sirrine.skoven";
    private const string ProductName    = "Skoven";
    private const string AppId          = "1122808136998612";

    // All scenes to include in the build, in order.
    // Update this list when scenes are added.
    private static readonly string[] Scenes =
    {
        "Assets/Skoven/Scenes/SkovenMain.unity",
    };

    /// <summary>
    /// Development APK — called by CI and the local build script.
    /// Reads the output path from the -customBuildPath command-line argument.
    /// </summary>
    public static void BuildDevelopmentApk()
    {
        string outputPath = GetArg("-customBuildPath") ?? "Builds/Skoven-dev.apk";
        Build(outputPath, BuildOptions.Development | BuildOptions.AllowDebugging);
    }

    /// <summary>
    /// Release APK — for App Lab / Store submission.
    /// Do not call this without a proper release keystore configured.
    /// </summary>
    public static void BuildReleaseApk()
    {
        string outputPath = GetArg("-customBuildPath") ?? "Builds/Skoven-release.apk";
        Build(outputPath, BuildOptions.None);
    }

    // ── Internal ────────────────────────────────────────────────────────

    private static void Build(string outputPath, BuildOptions options)
    {
        Debug.Log($"[SkovenBuild] Starting build → {outputPath}");

        ConfigurePlayerSettings();

        var scenes = GetScenePaths();
        if (scenes.Length == 0)
        {
            Debug.LogError("[SkovenBuild] No scenes found. Add scene paths to SkovenBuild.Scenes.");
            EditorApplication.Exit(1);
            return;
        }

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outputPath,
            target           = BuildTarget.Android,
            options          = options,
        });

        var summary = report.summary;
        Debug.Log($"[SkovenBuild] Result: {summary.result} | " +
                  $"Size: {summary.totalSize / 1024 / 1024} MB | " +
                  $"Errors: {summary.totalErrors} | " +
                  $"Warnings: {summary.totalWarnings}");

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[SkovenBuild] ✓ Build succeeded: {outputPath}");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[SkovenBuild] ✗ Build failed: {summary.result}");
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigurePlayerSettings()
    {
        // Identity
        PlayerSettings.companyName                        = "Sirrine-Jonathan";
        PlayerSettings.productName                        = ProductName;
        PlayerSettings.SetApplicationIdentifier(
            BuildTargetGroup.Android, PackageName);

        // Android / Quest settings
        PlayerSettings.Android.minSdkVersion              = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion           = (AndroidSdkVersions)32;
        PlayerSettings.Android.targetArchitectures        = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(
            BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.internetAccess             = InternetAccess.Require;

        // OpenXR / Meta XR SDK — App ID must be in OculusSettings.asset
        // (already set — this is a verification log only)
        Debug.Log($"[SkovenBuild] App ID: {AppId} | Package: {PackageName}");
    }

    private static string[] GetScenePaths()
    {
        // Use the explicit list; fall back to all enabled scenes in Build Settings
        var existing = Scenes
            .Where(s => System.IO.File.Exists(s))
            .ToArray();

        if (existing.Length > 0) return existing;

        // Fallback: whatever is enabled in Build Settings
        return EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
    }

    private static string? GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
