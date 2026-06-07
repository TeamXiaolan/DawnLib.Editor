using System;
using System.IO;
using System.IO.Compression;
using Dawn.Editor.Extensions;
using Dusk;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.ReleaseBuild;

[CustomEditor(typeof(DuskModInformation))]
public class DuskModInformationReleaseBuild : UnityEditor.Editor
{
    [field: SerializeField]
    public string AssetBundleFolderPath { get; private set; } = string.Empty;
    [field: SerializeField]
    public string BuildOutputPath { get; private set; } = string.Empty;

    private void OnEnable()
    {
        DuskModInformation modInfo = (DuskModInformation)target;
        AssetBundleFolderPath = EditorPrefs.GetString("DawnLibEditor.AssetBundlePath." + modInfo.name, string.Empty);
        BuildOutputPath = EditorPrefs.GetString("DawnLibEditor.BuildOutputPath." + modInfo.name, string.Empty);
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        DuskModInformation modInfo = (DuskModInformation)target;
        EditorGUILayout.Space(2.5f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("AssetBundle Directory:", GUILayout.Width(140));

            EditorGUI.BeginChangeCheck();
            AssetBundleFolderPath = EditorGUILayout.TextField(AssetBundleFolderPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString("DawnLibEditor.AssetBundlePath." + modInfo.name, AssetBundleFolderPath);
            }

            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select AssetBundle Directory", AssetBundleFolderPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetBundleFolderPath = path;
                    EditorPrefs.SetString("DawnLibEditor.AssetBundlePath." + modInfo.name, path);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Build Output Directory:", GUILayout.Width(140));

            EditorGUI.BeginChangeCheck();
            BuildOutputPath = EditorGUILayout.TextField(BuildOutputPath);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString("DawnLibEditor.BuildOutputPath." + modInfo.name, BuildOutputPath);
            }

            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Build Output Directory", BuildOutputPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    BuildOutputPath = path;
                    EditorPrefs.SetString("DawnLibEditor.BuildOutputPath." + modInfo.name, path);
                }
            }
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Build Package"))
        {
            if (EditorUtility.DisplayDialog("Build Package", "Are you sure you want to build your mod package?", "Yes", "No"))
            {
                BuildZipPackage(modInfo);
            }
        }
    }

    private void BuildZipPackage(DuskModInformation modInfo)
    {
        if (!Directory.Exists(AssetBundleFolderPath))
        {
            EditorUtility.DisplayDialog("Error", "AssetBundle directory does not exist.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(BuildOutputPath))
        {
            EditorUtility.DisplayDialog("Error", "Build output directory has not been set.", "OK");
            return;
        }

        Directory.CreateDirectory(BuildOutputPath);

        var tempRoot = Path.Combine(Path.GetTempPath(), $"DuskModPack_{Guid.NewGuid()}");
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        var assetsSubDir = Path.Combine(pluginsDir, "Assets");
        Directory.CreateDirectory(assetsSubDir);

        try
        {
            WriteTextFiles(modInfo, tempRoot);

            if (!TryWriteIcon(modInfo, tempRoot))
            {
                return;
            }

            CopyBuiltAssetBundles(pluginsDir, assetsSubDir);

            CopyLooseDlls(AssetBundleFolderPath, pluginsDir);

            ThunderstoreManifest manifest = new ThunderstoreManifest(modInfo);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(manifest, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Path.Combine(tempRoot, "manifest.json"), json);

            string zipName = $"{modInfo.AuthorName}.{modInfo.ModName}_{modInfo.Version}.zip";
            string zipPath = Path.Combine(BuildOutputPath, zipName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(tempRoot, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            Debug.Log($"[DawnLib Editor] Mod package built successfully at: {zipPath}");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to build mod package: {ex.Message}", "OK");
            Debug.LogError($"[DawnLib Editor] Failed to build mod package: {ex}");
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch { /* ignore */ }
        }
    }

    private static void WriteTextFiles(DuskModInformation modInfo, string tempRoot)
    {
        if (modInfo.READMEFile != null)
        {
            File.WriteAllBytes(Path.Combine(tempRoot, "README.md"), modInfo.READMEFile.bytes);
        }

        if (modInfo.ChangelogFile != null)
        {
            File.WriteAllBytes(Path.Combine(tempRoot, "CHANGELOG.md"), modInfo.ChangelogFile.bytes);
        }
    }

    private static bool TryWriteIcon(DuskModInformation modInfo, string tempRoot)
    {
        if (modInfo.ModIcon == null)
        {
            EditorUtility.DisplayDialog("Error", "Mod icon not provided, aborting.", "OK");
            return false;
        }

        Sprite sprite = modInfo.ModIcon;

        if (sprite.texture.width > 256 || sprite.texture.height > 256)
        {
            EditorUtility.DisplayDialog(
                "Error",
                $"Mod Icon is {sprite.texture.width}x{sprite.texture.height}. It needs to be a maximum of 256x256, aborting.",
                "OK"
            );

            return false;
        }

        Texture2D decompressedTexture = sprite.texture.DeCompress();
        File.WriteAllBytes(Path.Combine(tempRoot, "icon.png"), decompressedTexture.EncodeToPNG());

        return true;
    }

    private void CopyBuiltAssetBundles(string pluginsDir, string assetsSubDir)
    {
        string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();

        foreach (string bundleName in bundleNames)
        {
            string sourcePath = GetBuiltAssetBundlePath(AssetBundleFolderPath, bundleName);

            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[DawnLib Editor] AssetBundle '{bundleName}' is registered in the project, but no built file was found at: {sourcePath}");
                continue;
            }

            string destinationRoot = bundleName.EndsWith(".duskmod", StringComparison.OrdinalIgnoreCase)
                ? pluginsDir
                : assetsSubDir;

            string relativeBundlePath = ToPlatformPath(bundleName);
            string destinationPath = Path.Combine(destinationRoot, relativeBundlePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void CopyLooseDlls(string assetBundleFolderPath, string pluginsDir)
    {
        foreach (string dllPath in Directory.EnumerateFiles(assetBundleFolderPath, "*.dll", SearchOption.AllDirectories))
        {
            string destinationPath = Path.Combine(pluginsDir, Path.GetFileName(dllPath));
            File.Copy(dllPath, destinationPath, overwrite: true);
        }
    }

    private static string GetBuiltAssetBundlePath(string assetBundleFolderPath, string bundleName)
    {
        return Path.Combine(assetBundleFolderPath, ToPlatformPath(bundleName));
    }

    private static string ToPlatformPath(string assetBundleName)
    {
        return assetBundleName.Replace('/', Path.DirectorySeparatorChar)
                              .Replace('\\', Path.DirectorySeparatorChar);
    }
}