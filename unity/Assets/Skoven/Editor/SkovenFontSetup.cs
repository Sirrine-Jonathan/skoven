using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

public static class SkovenFontSetup
{
    private static readonly (string ttfRel, string assetRel)[] Fonts = new[]
    {
        ("Assets/Skoven/Fonts/JetBrainsMono-Regular.ttf", "Assets/Skoven/Fonts/JetBrainsMono-Regular SDF.asset"),
        ("Assets/Skoven/Fonts/JetBrainsMono-Bold.ttf", "Assets/Skoven/Fonts/JetBrainsMono-Bold SDF.asset"),
        ("Assets/Skoven/Fonts/Inter-Regular.ttf", "Assets/Skoven/Fonts/Inter-Regular SDF.asset"),
        ("Assets/Skoven/Fonts/Inter-Bold.ttf", "Assets/Skoven/Fonts/Inter-Bold SDF.asset")
    };

    [MenuItem("Skoven/Bake Font Assets")]
    public static void BakeFontAssets()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var (ttfRel, assetRel) in Fonts)
        {
            if (File.Exists(assetRel))
            {
                Debug.Log($"[SkovenFontSetup] Asset already exists: {assetRel}");
                continue;
            }

            string fullPath = Path.GetFullPath(ttfRel);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[SkovenFontSetup] TTF not found: {fullPath}");
                continue;
            }

            Debug.Log($"[SkovenFontSetup] Creating TMP_FontAsset from file path: {fullPath}");
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(fullPath, 0, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
            if (fontAsset == null)
            {
                Debug.LogError($"[SkovenFontSetup] CreateFontAsset returned null for {fullPath}");
                continue;
            }

            fontAsset.name = Path.GetFileNameWithoutExtension(assetRel);
            AssetDatabase.CreateAsset(fontAsset, assetRel);

            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            Debug.Log($"[SkovenFontSetup] Successfully saved: {assetRel}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== Finished baking Skoven Font Assets ===");
    }
}
