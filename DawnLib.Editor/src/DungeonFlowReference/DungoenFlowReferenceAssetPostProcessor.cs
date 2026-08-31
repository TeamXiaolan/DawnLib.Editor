using DunGen;
using DunGen.Graph;
using UnityEditor;

namespace Dawn.Editor.PropertyDrawers;

internal sealed class DungeonFlowReferenceAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
    {
        foreach (string path in importedAssets)
        {
            QueueIfRelevant(path);
        }

        for (int i = 0; i < movedAssets.Length; i++)
        {
            string newPath = movedAssets[i];
            string? previousPath = i < movedFromAssetPaths.Length ? movedFromAssetPaths[i] : null;
            QueueIfRelevant(newPath, previousPath);
        }

        foreach (string deletedPath in deletedAssets)
        {
            if (!deletedPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DungeonFlowReferenceChangeWatcher.QueueFullRefresh();
            break;
        }

        if (didDomainReload)
        {
            DungeonFlowReferenceChangeWatcher.QueueFullRefresh();
        }
    }

    private static void QueueIfRelevant(string assetPath, string? previousAssetPath = null)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        if (asset is not DungeonFlow && asset is not DungeonArchetype && asset is not TileSet)
        {
            return;
        }

        DungeonFlowReferenceChangeWatcher.QueueAsset(assetPath, previousAssetPath);
    }
}