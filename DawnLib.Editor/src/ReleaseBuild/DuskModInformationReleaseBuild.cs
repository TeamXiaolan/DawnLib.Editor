using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
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
            AssetBundleFolderPath = EditorGUILayout.TextField(AssetBundleFolderPath);
            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select AssetBundle Directory", AssetBundleFolderPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    EditorPrefs.SetString("DawnLibEditor.AssetBundlePath." + modInfo.name, path);
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
                    EditorPrefs.SetString("DawnLibEditor.BuildOutputPath." + modInfo.name, path);
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

    private void BuildZipPackage(DuskModInformation modInfo)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"DuskodPack_{Guid.NewGuid()}");
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

            var sprite = modInfo.ModIcon;

            if (sprite.texture.width > 256 || sprite.texture.height > 256)
            {
                EditorUtility.DisplayDialog("Error", $"Mod Icon is {sprite.texture.width}x{sprite.texture.height}, it needs to be resized to a maximum of 256x256, aborting.", "OK");
                // var resized = new Texture2D(256, 256, tex.format, mipChain: false);
                // Graphics.ConvertTexture(tex, resized);
                // File.WriteAllBytes(Path.Combine(tempRoot, "icon.png"), resized.EncodeToPNG());
                return;
            }

            Texture2D decompressedTexture = sprite.texture.DeCompress();
            File.WriteAllBytes(Path.Combine(tempRoot, "icon.png"), decompressedTexture.EncodeToPNG());

            bool includeWR = false;
            string[] allBundleFiles = Directory.GetFiles(AssetBundleFolderPath);
            bool WRExists = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "WeatherRegistry");
            foreach (var potentialBundleFile in allBundleFiles)
            {
                string fileExtension = Path.GetExtension(potentialBundleFile).ToLowerInvariant();
                if (fileExtension == ".meta")
                    continue;

                if (fileExtension == ".lethalbundle")
                    continue;

                string destDir = fileExtension == ".duskmod" ? pluginsDir : assetsSubDir;
                string dest = Path.Combine(destDir, Path.GetFileName(potentialBundleFile));
                File.Copy(potentialBundleFile, dest, true);

                AssetBundle? assetBundle = AssetBundle.LoadFromFile(dest);
                if (assetBundle == null)
                    continue;


                bool DuskWeatherInHere = TryGetWeathers(assetBundle);
                if (DuskWeatherInHere)
                {
                    includeWR = true;
                    Debug.Log($"[DawnLib Editor] Bundle '{Path.GetFileName(potentialBundleFile)}' contains a DuskWeatherDefinition!");
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

            Debug.Log($"[DawnLib Editor] Mod package built successfully at: {zipPath}");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to build mod package: {ex.Message}", "OK");
            Debug.LogError($"[DawnLib Editor] Failed to build mod package: {ex.Message}");
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

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    static bool TryGetWeathers(AssetBundle bundle)
    {
        if (bundle.LoadAllAssets<DuskWeatherDefinition>().Length > 0)
        {
            return true;
        }
        return false;
    }
    
}
