using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.AssetPostprocessors;

public class AutoDeleteCompatibilityDllOnImport : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        string pathToDelete = string.Empty;
        foreach (string assetPath in importedAssets)
        {
            if (!assetPath.EndsWith("com.github.teamxiaolan.dawnlib.compatibility.dll"))
                continue;

            break;
        }

        if (!string.IsNullOrEmpty(pathToDelete))
        {
            bool worked = AssetDatabase.DeleteAsset(pathToDelete);

            if (worked)
            {
                Debug.Log($"AutoDeleted {pathToDelete}");
            }
        }
    }
}