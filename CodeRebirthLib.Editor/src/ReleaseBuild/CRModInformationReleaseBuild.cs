using System.IO;
using System.IO.Compression;
using Microsoft.Win32.SafeHandles;
using UnityEditor;
using UnityEngine;

namespace CodeRebirthLib.Editor.PropertyDrawers;

[CustomEditor(typeof(CRModInformation))]
public class CRModInformationReleaseBuild : UnityEditor.Editor
{
    // add a couple of fields for TextAsset for README and CHANGELOG files? both can likely be empty
    // add field for mod description, which would just be a string, can be empty
    // add field for website url, which would just be a string, can be empty
    // add path to icon image -> error handling for resolution of image being a max of 256x256
    // add path to assetbundles -> error handling for there being only one *.crmod bundle and atleast one other non .crmod bundle
    // add button to do a build 

    [field: SerializeField]
    public string AssetBundleFolderPath { get; private set; }

    [field: SerializeField]
    public string BuildOutputPath { get; private set; }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        CRModInformation modInfo = (CRModInformation)target;
        EditorGUILayout.Space(2.5f);

        EditorGUILayout.BeginHorizontal();
        string newAssetBundleDirectory = EditorGUILayout.TextField("AssetBundle Directory:", AssetBundleFolderPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Select", EditorStyles.miniButton))
        {
            newAssetBundleDirectory = EditorUtility.OpenFolderPanel("Select AssetBundle Directory", AssetBundleFolderPath, "");
            if (!string.IsNullOrEmpty(newAssetBundleDirectory))
            {
                AssetBundleFolderPath = newAssetBundleDirectory;
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        string newBuildOutputPath = EditorGUILayout.TextField("Build Output Directory:", BuildOutputPath, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Select", EditorStyles.miniButton))
        {
            newBuildOutputPath = EditorUtility.OpenFolderPanel("Select Build Output Directory", BuildOutputPath, "");
            if (!string.IsNullOrEmpty(newBuildOutputPath))
            {
                BuildOutputPath = newBuildOutputPath;
            }
        }
        EditorGUILayout.EndHorizontal();

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
        string zipPath = BuildOutputPath;
        FileStream readmeFileStream = new FileStream(AssetDatabase.GetAssetPath(modInfo.READMEFile), FileMode.CreateNew, FileAccess.Read);
        ZipArchive readmeZipArchive = new ZipArchive(readmeFileStream, ZipArchiveMode.Create);

        FileStream changelogFileStream = new FileStream(AssetDatabase.GetAssetPath(modInfo.ChangelogFile), FileMode.CreateNew, FileAccess.Read);
        ZipArchive changelogZipArchive = new ZipArchive(changelogFileStream, ZipArchiveMode.Create);

        // FileStream iconFileStream = new FileStream(AssetDatabase.GetAssetPath(modInfo.ModIcon), FileMode.CreateNew, FileAccess.Read);
        // ZipArchive iconZipArchive = new ZipArchive(iconFileStream, ZipArchiveMode.Create);

        ZipArchiveEntry readmeZipEntry = readmeZipArchive.CreateEntry("README.md");
        readmeZipEntry.Open().Write(modInfo.READMEFile.bytes, 0, modInfo.READMEFile.bytes.Length);
        ZipArchiveEntry changelogZipEntry = changelogZipArchive.CreateEntry("CHANGELOG.md");
        changelogZipEntry.Open().Write(modInfo.ChangelogFile.bytes, 0, modInfo.ChangelogFile.bytes.Length);
        // ZipArchiveEntry iconZipEntry = iconZipArchive.CreateEntry("icon.png");
        // iconZipEntry.Open().Write(modInfo.ModIcon.bytes, 0, modInfo.ModIcon.bytes.Length);
        readmeZipArchive.Dispose();
        changelogZipArchive.Dispose();
        // iconZipArchive.Dispose();

        // Put the README, changelog and icon next to eachother, create a manifest.json file with a specific format.
        // Create the manifest.json file using a format with unfilled fields that can be filled with AuthorName, ModName, Version, ModDescription and WebsiteUrl from CRModInformation.
        // Then make a folder called plugins and put a folder called Assets inside it.
        // Inside the plugins folder put the assetbundles with the .crmod extension in it.
        // Inside the Assets folder put the other assetbundles without the .crmod extension in it.
        // if possible check whether any of the bundles contain a CRWeatherDefinition ScriptableObject in them and log that.

    }
}