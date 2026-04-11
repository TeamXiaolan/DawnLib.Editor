using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/*namespace Dawn.Editor.AssetPostprocessors.ScriptReferenceRepair;
[Serializable]
public class ScriptIdentity
{
    public string guid = "";
    public long fileId;
}

[Serializable]
public class ScriptRecord
{
    public string fullTypeName = "";
    public string assetPath = "";
    public bool existsInProject = true;

    public ScriptIdentity current = new();
    public List<ScriptIdentity> previousIdentities = new();

    /// <summary>
    /// Old names that should be treated as the same script identity.
    /// Useful if you renamed namespace/class and still want restoration.
    /// </summary>
    public List<string> aliases = new();

    public bool MatchesName(string fullName)
    {
        if (string.Equals(fullTypeName, fullName, StringComparison.Ordinal))
        {
            return true;
        }

        for (int i = 0; i < aliases.Count; i++)
        {
            if (string.Equals(aliases[i], fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasIdentity(string guid, long fileId)
    {
        if (string.Equals(current.guid, guid, StringComparison.OrdinalIgnoreCase) && current.fileId == fileId)
            return true;

        for (int i = 0; i < previousIdentities.Count; i++)
        {
            ScriptIdentity identity = previousIdentities[i];
            if (string.Equals(identity.guid, guid, StringComparison.OrdinalIgnoreCase) && identity.fileId == fileId)
                return true;
        }

        return false;
    }

    public void PushCurrentToHistoryIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(current.guid))
            return;

        if (previousIdentities.Any(x =>
                string.Equals(x.guid, current.guid, StringComparison.OrdinalIgnoreCase) &&
                x.fileId == current.fileId))
        {
            return;
        }

        previousIdentities.Add(new ScriptIdentity
        {
            guid = current.guid,
            fileId = current.fileId
        });
    }
}

[Serializable]
public class ScriptRegistryData
{
    public List<ScriptRecord> scripts = new();
}

internal static class ScriptReferenceRegistry
{
    private const string RegistryFolder = "ProjectSettings";
    private const string RegistryFileName = "DawnScriptReferenceRegistry.json";

    public static string RegistryPath => Path.Combine(RegistryFolder, RegistryFileName);

    public static ScriptRegistryData Load()
    {
        try
        {
            if (!File.Exists(RegistryPath))
                return new ScriptRegistryData();

            string json = File.ReadAllText(RegistryPath);
            if (string.IsNullOrWhiteSpace(json))
                return new ScriptRegistryData();

            ScriptRegistryData data = JsonUtility.FromJson<ScriptRegistryData>(json);
            return data ?? new ScriptRegistryData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScriptReferenceRepair] Failed to load registry: {ex}");
            return new ScriptRegistryData();
        }
    }

    public static void Save(ScriptRegistryData data)
    {
        try
        {
            if (!Directory.Exists(RegistryFolder))
                Directory.CreateDirectory(RegistryFolder);

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(RegistryPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScriptReferenceRepair] Failed to save registry: {ex}");
        }
    }

    public static List<DiscoveredScriptInfo> GatherCurrentScripts()
    {
        List<DiscoveredScriptInfo> results = new();

        MonoScript[] allRuntimeScripts = MonoImporter.GetAllRuntimeMonoScripts();
        foreach (MonoScript monoScript in allRuntimeScripts)
        {
            if (monoScript == null)
                continue;

            Type scriptType = monoScript.GetClass();
            if (scriptType == null)
                continue;

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(monoScript, out string guid, out long fileId))
                continue;

            string assetPath = AssetDatabase.GetAssetPath(monoScript);
            string fullTypeName = scriptType.FullName ?? scriptType.Name;
            if (string.IsNullOrWhiteSpace(fullTypeName))
                continue;

            results.Add(new DiscoveredScriptInfo
            {
                FullTypeName = fullTypeName,
                AssetPath = assetPath,
                Guid = guid,
                FileId = fileId
            });
        }

        return results
            .GroupBy(x => x.FullTypeName, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
    }

    public static ScriptRecord? FindByNameOrAlias(ScriptRegistryData data, string fullTypeName)
    {
        for (int i = 0; i < data.scripts.Count; i++)
        {
            if (data.scripts[i].MatchesName(fullTypeName))
            {
                return data.scripts[i];
            }
        }

        return null;
    }

    public static ScriptRecord? FindByCurrentIdentity(ScriptRegistryData data, string guid, long fileId)
    {
        for (int i = 0; i < data.scripts.Count; i++)
        {
            ScriptRecord record = data.scripts[i];
            if (string.Equals(record.current.guid, guid, StringComparison.OrdinalIgnoreCase) && record.current.fileId == fileId)
            {
                return record;
            }
        }

        return null;
    }

    public static ScriptRecord CreateRecord(DiscoveredScriptInfo info)
    {
        return new ScriptRecord
        {
            fullTypeName = info.FullTypeName,
            assetPath = info.AssetPath,
            existsInProject = true,
            current = new ScriptIdentity
            {
                guid = info.Guid,
                fileId = info.FileId
            }
        };
    }
}

internal struct DiscoveredScriptInfo
{
    public string FullTypeName { get; set; }
    public string AssetPath { get; set; }
    public string Guid { get; set; }
    public long FileId { get; set; }
}*/