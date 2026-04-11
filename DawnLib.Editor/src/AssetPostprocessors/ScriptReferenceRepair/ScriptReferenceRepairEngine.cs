using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/*namespace Dawn.Editor.AssetPostprocessors.ScriptReferenceRepair;
internal static class ScriptReferenceRepairEngine
{
    private static readonly HashSet<string> ScannableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".prefab",
        ".unity",
        ".asset",
        ".mat",
        ".controller",
        ".overrideController",
        ".anim",
        ".playable",
        ".mask",
        ".guiskin",
        ".flare"
    };

    private static readonly string[] IgnoredPathPrefixes =
    {
        "Packages/",
        "Library/",
        "ProjectSettings/",
        "Logs/",
        "Temp/",
        "UserSettings/"
    };

    private static readonly Regex ScriptReferenceRegex = new(
        @"m_Script:\s*\{fileID:\s*(?<fileId>-?\d+),\s*guid:\s*(?<guid>[0-9a-fA-F]+),\s*type:\s*3\s*\}",
        RegexOptions.Compiled);

    internal readonly struct IdentityChange
    {
        public readonly string scriptName;
        public readonly string oldGuid;
        public readonly long oldFileId;
        public readonly string newGuid;
        public readonly long newFileId;

        public IdentityChange(string scriptName, string oldGuid, long oldFileId, string newGuid, long newFileId)
        {
            this.scriptName = scriptName;
            this.oldGuid = oldGuid;
            this.oldFileId = oldFileId;
            this.newGuid = newGuid;
            this.newFileId = newFileId;
        }
    }

    internal readonly struct RunResult
    {
        public readonly int modifiedFiles;
        public readonly int identityChanges;
        public readonly int newRecords;
        public readonly int missingRecords;

        public RunResult(int modifiedFiles, int identityChanges, int newRecords, int missingRecords)
        {
            this.modifiedFiles = modifiedFiles;
            this.identityChanges = identityChanges;
            this.newRecords = newRecords;
            this.missingRecords = missingRecords;
        }
    }

    public static RunResult Run(bool verboseLogging = true)
    {
        WarnIfTextSerializationIsNotEnabled();

        ScriptRegistryData registry = ScriptReferenceRegistry.Load();
        List<DiscoveredScriptInfo> currentScripts = ScriptReferenceRegistry.GatherCurrentScripts();

        int newRecords = 0;
        int missingRecords = 0;
        List<IdentityChange> changes = new();

        HashSet<string> currentNames = currentScripts
            .Select(x => x.FullTypeName)
            .ToHashSet(StringComparer.Ordinal);

        for (int i = 0; i < registry.scripts.Count; i++)
            registry.scripts[i].existsInProject = false;

        foreach (DiscoveredScriptInfo discovered in currentScripts)
        {
            ScriptRecord? existing = ScriptReferenceRegistry.FindByNameOrAlias(registry, discovered.FullTypeName);

            if (existing == null)
            {
                // Extra attempt: if something already exists with the same current identity, update its name.
                existing = ScriptReferenceRegistry.FindByCurrentIdentity(registry, discovered.Guid, discovered.FileId);
                if (existing != null && !string.Equals(existing.fullTypeName, discovered.FullTypeName, StringComparison.Ordinal))
                {
                    if (!existing.aliases.Contains(existing.fullTypeName))
                        existing.aliases.Add(existing.fullTypeName);

                    existing.fullTypeName = discovered.FullTypeName;
                }
            }

            if (existing == null)
            {
                registry.scripts.Add(ScriptReferenceRegistry.CreateRecord(discovered));
                newRecords++;
                continue;
            }

            existing.existsInProject = true;
            existing.assetPath = discovered.AssetPath;

            bool nameChanged = !string.Equals(existing.fullTypeName, discovered.FullTypeName, StringComparison.Ordinal);
            if (nameChanged)
            {
                if (!existing.aliases.Contains(existing.fullTypeName))
                    existing.aliases.Add(existing.fullTypeName);

                existing.fullTypeName = discovered.FullTypeName;
            }

            bool guidChanged = !string.Equals(existing.current.guid, discovered.Guid, StringComparison.OrdinalIgnoreCase);
            bool fileIdChanged = existing.current.fileId != discovered.FileId;

            if (guidChanged || fileIdChanged)
            {
                existing.PushCurrentToHistoryIfNeeded();

                changes.Add(new IdentityChange(
                    existing.fullTypeName,
                    existing.current.guid,
                    existing.current.fileId,
                    discovered.Guid,
                    discovered.FileId));

                existing.current.guid = discovered.Guid;
                existing.current.fileId = discovered.FileId;
            }
        }

        for (int i = 0; i < registry.scripts.Count; i++)
        {
            ScriptRecord record = registry.scripts[i];
            if (!currentNames.Contains(record.fullTypeName) && !record.aliases.Any(currentNames.Contains))
            {
                record.existsInProject = false;
                missingRecords++;
            }
        }

        int modifiedFiles = 0;
        if (changes.Count > 0)
            modifiedFiles = PatchProjectAssets(changes, verboseLogging);

        ScriptReferenceRegistry.Save(registry);

        if (verboseLogging)
        {
            Debug.Log(
                $"[ScriptReferenceRepair] Done. " +
                $"Changes: {changes.Count}, Modified files: {modifiedFiles}, " +
                $"New records: {newRecords}, Missing records tracked: {missingRecords}.");
        }

        return new RunResult(modifiedFiles, changes.Count, newRecords, missingRecords);
    }

    private static int PatchProjectAssets(List<IdentityChange> changes, bool verboseLogging)
    {
        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
            return 0;

        string[] allFiles = Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories);
        int modifiedCount = 0;

        foreach (string absolutePath in allFiles)
        {
            string projectRelativePath = AbsoluteToProjectRelativePath(absolutePath);
            if (string.IsNullOrWhiteSpace(projectRelativePath))
                continue;

            if (ShouldIgnorePath(projectRelativePath))
                continue;

            string extension = Path.GetExtension(projectRelativePath);
            if (!ScannableExtensions.Contains(extension))
                continue;

            if (!IsLikelyTextSerializedFile(absolutePath))
                continue;

            string originalText;
            try
            {
                originalText = File.ReadAllText(absolutePath);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrEmpty(originalText) || !originalText.Contains("m_Script:"))
                continue;

            string updatedText = originalText;
            bool changed = false;

            foreach (IdentityChange change in changes)
            {
                string next = ReplaceOneIdentity(updatedText, change.oldGuid, change.oldFileId, change.newGuid, change.newFileId);
                if (!ReferenceEquals(next, updatedText) && !string.Equals(next, updatedText, StringComparison.Ordinal))
                {
                    updatedText = next;
                    changed = true;

                    if (verboseLogging)
                    {
                        Debug.Log(
                            $"[ScriptReferenceRepair] Patched {projectRelativePath} " +
                            $"for {change.scriptName}: ({change.oldGuid}, {change.oldFileId}) -> ({change.newGuid}, {change.newFileId})");
                    }
                }
            }

            if (!changed)
                continue;

            try
            {
                File.WriteAllText(absolutePath, updatedText);
                modifiedCount++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScriptReferenceRepair] Failed writing {projectRelativePath}: {ex}");
            }
        }

        if (modifiedCount > 0)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        return modifiedCount;
    }

    private static string ReplaceOneIdentity(string input, string oldGuid, long oldFileId, string newGuid, long newFileId)
    {
        return ScriptReferenceRegex.Replace(input, match =>
        {
            string matchGuid = match.Groups["guid"].Value;
            string matchFileIdText = match.Groups["fileId"].Value;

            if (!long.TryParse(matchFileIdText, out long matchFileId))
                return match.Value;

            if (!string.Equals(matchGuid, oldGuid, StringComparison.OrdinalIgnoreCase))
                return match.Value;

            if (matchFileId != oldFileId)
                return match.Value;

            return $"m_Script: {{fileID: {newFileId}, guid: {newGuid}, type: 3}}";
        });
    }

    private static bool ShouldIgnorePath(string projectRelativePath)
    {
        for (int i = 0; i < IgnoredPathPrefixes.Length; i++)
        {
            if (projectRelativePath.StartsWith(IgnoredPathPrefixes[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsLikelyTextSerializedFile(string absolutePath)
    {
        try
        {
            using FileStream stream = new(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);

            char[] buffer = new char[64];
            int read = reader.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return false;

            string start = new(buffer, 0, read);

            // Most Unity YAML assets start with this.
            return start.Contains("%YAML") || start.Contains("MonoBehaviour:") || start.Contains("GameObject:");
        }
        catch
        {
            return false;
        }
    }

    private static string AbsoluteToProjectRelativePath(string absolutePath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        absolutePath = absolutePath.Replace('\\', '/');

        if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return "Assets" + absolutePath[dataPath.Length..];
    }

    private static void WarnIfTextSerializationIsNotEnabled()
    {
        if (EditorSettings.serializationMode != SerializationMode.ForceText)
        {
            Debug.LogWarning(
                "[ScriptReferenceRepair] Asset Serialization Mode is not Force Text. " +
                "Binary assets cannot be repaired by YAML patching.");
        }
    }
}*/