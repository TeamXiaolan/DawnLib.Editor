using System;
using System.IO;
using System.IO.Compression;
using CodeRebirthLib.ContentManagement.Weathers;
using UnityEditor;
using UnityEngine;

namespace CodeRebirthLib.Editor.ReleaseBuild;
[CustomEditor(typeof(CRModInformation))]
public class CRModInformationReleaseBuild : UnityEditor.Editor
{
    [field: SerializeField]
    public string AssetBundleFolderPath { get; private set; } = string.Empty;
    [field: SerializeField]
    public string BuildOutputPath { get; private set; } = string.Empty;

    private void OnEnable()
    {
        CRModInformation modInfo = (CRModInformation)target;
        AssetBundleFolderPath = EditorPrefs.GetString("CRLibEditor.AssetBundlePath." + modInfo.name, string.Empty);
        BuildOutputPath = EditorPrefs.GetString("CRLibEditor.BuildOutputPath." + modInfo.name, string.Empty);
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CRModInformation modInfo = (CRModInformation)target;
        EditorGUILayout.Space(2.5f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("AssetBundle Directory:", GUILayout.Width(140));
            AssetBundleFolderPath = EditorGUILayout.TextField(AssetBundleFolderPath);
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select AssetBundle Directory", AssetBundleFolderPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    EditorPrefs.SetString("CRLibEditor.AssetBundlePath." + modInfo.name, path);
                    AssetBundleFolderPath = path;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Build Output Directory:", GUILayout.Width(140));
            BuildOutputPath = EditorGUILayout.TextField(BuildOutputPath);
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Build Output Directory", BuildOutputPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    EditorPrefs.SetString("CRLibEditor.BuildOutputPath." + modInfo.name, path);
                    BuildOutputPath = path;
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

    private void BuildZipPackage(CRModInformation modInfo)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"CRModPack_{Guid.NewGuid()}");
        var pluginsDir = Path.Combine(tempRoot, "plugins");
        var assetsSubDir = Path.Combine(pluginsDir, "Assets");
        Directory.CreateDirectory(assetsSubDir);

        try
        {
            if (modInfo.READMEFile != null)
            {
                File.WriteAllBytes(Path.Combine(tempRoot, "README.md"), modInfo.READMEFile.bytes);
            }

            if (modInfo.ChangelogFile != null)
            {
                File.WriteAllBytes(Path.Combine(tempRoot, "CHANGELOG.md"), modInfo.ChangelogFile.bytes);
            }

            if (modInfo.ModIcon == null)
            {
                EditorUtility.DisplayDialog("Error", "Mod icon not provided, aborting.", "OK");
                return;
            }

            var tex = modInfo.ModIcon;

            if (!modInfo.ModIcon.isReadable)
            {
                EditorUtility.DisplayDialog("Error", "Mod icon is not readable, aborting.", "OK");
                return;
            }

            if (tex.width > 256 || tex.height > 256)
            {
                EditorUtility.DisplayDialog("Error", "Mod Icon is {tex.width}x{tex.height}, it needs to be resized to a maximum of 256x256, aborting.", "OK");
                // var resized = new Texture2D(256, 256, tex.format, mipChain: false);
                // Graphics.ConvertTexture(tex, resized);
                // File.WriteAllBytes(Path.Combine(tempRoot, "icon.png"), resized.EncodeToPNG());
                return;
            }

            File.WriteAllBytes(Path.Combine(tempRoot, "icon.png"), tex.EncodeToPNG());

            bool includeWR = false;
            string[] allBundleFiles = Directory.GetFiles(AssetBundleFolderPath);
            foreach (var potentialBundleFile in allBundleFiles)
            {
                string fileExtension = Path.GetExtension(potentialBundleFile).ToLowerInvariant();
                if (fileExtension == ".meta")
                    continue;

                if (fileExtension == ".lethalbundle")
                    continue;

                string destDir = fileExtension == ".crmod" ? pluginsDir : assetsSubDir;
                string dest = Path.Combine(destDir, Path.GetFileName(potentialBundleFile));
                File.Copy(potentialBundleFile, dest, true);

                AssetBundle? assetBundle = AssetBundle.LoadFromFile(dest);
                if (assetBundle == null)
                    continue;

                CRWeatherDefinition[] weathers = assetBundle.LoadAllAssets<CRWeatherDefinition>();
                if (weathers.Length > 0)
                {
                    includeWR = true;
                    Debug.Log($"[CRLib Editor] Bundle '{Path.GetFileName(potentialBundleFile)}' contains a CRWeatherDefinition!");
                }
                assetBundle.Unload(true);
            }

            ThunderstoreManifest manifest = new ThunderstoreManifest(modInfo, includeWR);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
            File.WriteAllText(Path.Combine(tempRoot, "manifest.json"), json);

            var zipName = $"{modInfo.AuthorName}.{modInfo.ModName}_{modInfo.Version}.zip";
            var zipPath = Path.Combine(BuildOutputPath, zipName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(tempRoot, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            Debug.Log($"[CRLib Editor] Mod package built successfully at: {zipPath}");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to build mod package: {ex.Message}", "OK");
            Debug.LogError($"[CRLib Editor] Failed to build mod package: {ex.Message}");
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
}
