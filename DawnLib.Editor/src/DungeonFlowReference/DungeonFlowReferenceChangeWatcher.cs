using System;
using System.Collections.Generic;
using DunGen;
using DunGen.Graph;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[InitializeOnLoad]
internal static class DungeonFlowReferenceChangeWatcher
{
    private static readonly HashSet<string> ChangedAssetPaths = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, HashSet<string>> LegacyFlowNamesByGuid = new(StringComparer.Ordinal);

    private static bool _fullRefreshRequested;
    private static bool _processQueued;

    static DungeonFlowReferenceChangeWatcher()
    {
        Undo.postprocessModifications += OnPostprocessModifications;
    }

    private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications)
    {
        foreach (UndoPropertyModification modification in modifications)
        {
            UnityEngine.Object target = modification.currentValue.target;
            if (!IsRelevantObject(target))
                continue;

            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path))
                continue;

            QueueAsset(path);
        }

        return modifications;
    }

    internal static void QueueAsset(string assetPath, string? previousAssetPath = null)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        ChangedAssetPaths.Add(assetPath);
        if (!string.IsNullOrEmpty(previousAssetPath))
        {
            DungeonFlow flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(assetPath);
            if (flow != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                string previousName = System.IO.Path.GetFileNameWithoutExtension(previousAssetPath);
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(previousName))
                {
                    if (!LegacyFlowNamesByGuid.TryGetValue(guid, out HashSet<string> names))
                    {
                        names = new HashSet<string>(StringComparer.Ordinal);
                        LegacyFlowNamesByGuid.Add(guid, names);
                    }

                    names.Add(previousName);
                }
            }
        }

        QueueProcessing();
    }

    internal static void QueueFullRefresh()
    {
        _fullRefreshRequested = true;
        QueueProcessing();
    }

    private static void QueueProcessing()
    {
        if (_processQueued)
            return;

        _processQueued = true;
        EditorApplication.delayCall += ProcessChanges;
    }

    private static void ProcessChanges()
    {
        _processQueued = false;
        bool fullRefresh = _fullRefreshRequested;
        _fullRefreshRequested = false;
        HashSet<string> changedPaths = new(ChangedAssetPaths, StringComparer.OrdinalIgnoreCase);
        ChangedAssetPaths.Clear();

        Dictionary<string, HashSet<string>> legacyNames = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, HashSet<string>> pair in LegacyFlowNamesByGuid)
        {
            legacyNames.Add(pair.Key, new HashSet<string>(pair.Value, StringComparer.Ordinal));
        }

        LegacyFlowNamesByGuid.Clear();
        if (fullRefresh)
        {
            DungeonFlowReferenceEditorUtility.RefreshAllReferences();
            return;
        }

        if (changedPaths.Count == 0)
            return;

        HashSet<DungeonFlow> affectedFlows = FindAffectedDungeonFlows(changedPaths);
        DungeonFlowReferenceEditorUtility.RefreshReferencesForFlows(affectedFlows, legacyNames);
    }

    private static HashSet<DungeonFlow> FindAffectedDungeonFlows(HashSet<string> changedPaths)
    {
        HashSet<DungeonFlow> affectedFlows = new();
        foreach (string changedPath in changedPaths)
        {
            DungeonFlow directlyChangedFlow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(changedPath);

            if (directlyChangedFlow != null)
            {
                affectedFlows.Add(directlyChangedFlow);
            }
        }

        string[] flowGuids = AssetDatabase.FindAssets("t:DungeonFlow");

        foreach (string flowGuid in flowGuids)
        {
            string flowPath = AssetDatabase.GUIDToAssetPath(flowGuid);
            DungeonFlow flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(flowPath);
            if (flow == null)
                continue;

            if (affectedFlows.Contains(flow))
                continue;

            string[] dependencies = AssetDatabase.GetDependencies(flowPath, true);

            foreach (string dependencyPath in dependencies)
            {
                if (!changedPaths.Contains(dependencyPath))
                {
                    continue;
                }

                affectedFlows.Add(flow);
                break;
            }
        }

        return affectedFlows;
    }

    private static bool IsRelevantObject(UnityEngine.Object target)
    {
        return target is DungeonFlow ||
               target is DungeonArchetype ||
               target is TileSet;
    }
}