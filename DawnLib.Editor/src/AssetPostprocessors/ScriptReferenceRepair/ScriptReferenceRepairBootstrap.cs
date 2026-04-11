using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/*namespace Dawn.Editor.AssetPostprocessors.ScriptReferenceRepair;
internal sealed class ScriptReferenceRepairBootstrap : AssetPostprocessor
{
    private static bool _runQueued;
    private static bool _isRunning;

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (_isRunning || _runQueued)
        {
            return;
        }

        bool registryMissing = !System.IO.File.Exists(ScriptReferenceRegistry.RegistryPath);

        bool relevantChange = registryMissing ||
            importedAssets.Any(IsRelevantPath) ||
            deletedAssets.Any(IsRelevantPath) ||
            movedAssets.Any(IsRelevantPath) ||
            movedFromAssetPaths.Any(IsRelevantPath);

        if (!relevantChange)
        {
            return;
        }

        _runQueued = true;
        EditorApplication.delayCall += DelayedRun;
    }

    private static bool IsRelevantPath(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
    }

    private static void DelayedRun()
    {
        if (_isRunning)
            return;

        _runQueued = false;
        _isRunning = true;

        try
        {
            AssetDatabase.StartAssetEditing();
            ScriptReferenceRepairEngine.Run(verboseLogging: true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScriptReferenceRepair] Automatic pass failed: {ex}");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            _isRunning = false;
        }
    }

    [MenuItem("Tools/Dawn/Script Reference Repair/Run Full Repair")]
    private static void RunFullRepair()
    {
        if (_isRunning)
        {
            Debug.LogWarning("[ScriptReferenceRepair] A repair pass is already running.");
            return;
        }

        try
        {
            _isRunning = true;
            ScriptReferenceRepairEngine.Run(verboseLogging: true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScriptReferenceRepair] Manual full repair failed: {ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    [MenuItem("Tools/Dawn/Script Reference Repair/Rebuild Registry Only")]
    private static void RebuildRegistryOnly()
    {
        try
        {
            ScriptRegistryData existing = ScriptReferenceRegistry.Load();
            var currentScripts = ScriptReferenceRegistry.GatherCurrentScripts();

            foreach (ScriptRecord record in existing.scripts)
                record.existsInProject = false;

            foreach (var discovered in currentScripts)
            {
                ScriptRecord? record = ScriptReferenceRegistry.FindByNameOrAlias(existing, discovered.FullTypeName);

                if (record == null)
                {
                    existing.scripts.Add(ScriptReferenceRegistry.CreateRecord(discovered));
                    continue;
                }

                record.existsInProject = true;
                record.assetPath = discovered.AssetPath;

                bool identityChanged =
                    !string.Equals(record.current.guid, discovered.Guid, StringComparison.OrdinalIgnoreCase) ||
                    record.current.fileId != discovered.FileId;

                if (identityChanged)
                {
                    record.PushCurrentToHistoryIfNeeded();
                    record.current.guid = discovered.Guid;
                    record.current.fileId = discovered.FileId;
                }

                if (!string.Equals(record.fullTypeName, discovered.FullTypeName, StringComparison.Ordinal))
                {
                    if (!record.aliases.Contains(record.fullTypeName))
                        record.aliases.Add(record.fullTypeName);

                    record.fullTypeName = discovered.FullTypeName;
                }
            }

            ScriptReferenceRegistry.Save(existing);
            AssetDatabase.Refresh();

            Debug.Log("[ScriptReferenceRepair] Registry rebuilt without patching project assets.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScriptReferenceRepair] Registry rebuild failed: {ex}");
        }
    }

    [MenuItem("Tools/Dawn/Script Reference Repair/Open Registry JSON")]
    private static void OpenRegistryJson()
    {
        string path = ScriptReferenceRegistry.RegistryPath;
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("[ScriptReferenceRepair] Registry JSON does not exist yet.");
            return;
        }

        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Tools/Dawn/Script Reference Repair/Restore Broken Script References")]
    private static void RestoreBrokenScriptReferences()
    {
        if (_isRunning)
        {
            Debug.LogWarning("[ScriptReferenceRepair] A repair pass is already running.");
            return;
        }

        try
        {
            _isRunning = true;
            ScriptReferenceBrokenRestore.Run(verboseLogging: true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScriptReferenceRepair] Broken reference restore failed: {ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }
}*/