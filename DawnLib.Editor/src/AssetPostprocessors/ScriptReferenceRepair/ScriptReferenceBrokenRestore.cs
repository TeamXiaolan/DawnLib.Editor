using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/*namespace Dawn.Editor.AssetPostprocessors.ScriptReferenceRepair;
internal static class ScriptReferenceBrokenRestore
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

    private static readonly Regex MonoBehaviourBlockRegex = new(
        @"---\s*!u!114\s*&\d+.*?(?=^---\s*!u!|\z)",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ScriptReferenceRegex = new(
        @"m_Script:\s*\{fileID:\s*(?<fileId>-?\d+),\s*guid:\s*(?<guid>[0-9a-fA-F]+),\s*type:\s*3\s*\}",
        RegexOptions.Compiled);

    internal readonly struct RestoreResult
    {
        public readonly int scannedFiles;
        public readonly int modifiedFiles;
        public readonly int restoredReferences;
        public readonly int ambiguousReferences;
        public readonly int unresolvedReferences;

        public RestoreResult(
            int scannedFiles,
            int modifiedFiles,
            int restoredReferences,
            int ambiguousReferences,
            int unresolvedReferences)
        {
            this.scannedFiles = scannedFiles;
            this.modifiedFiles = modifiedFiles;
            this.restoredReferences = restoredReferences;
            this.ambiguousReferences = ambiguousReferences;
            this.unresolvedReferences = unresolvedReferences;
        }
    }

    private readonly struct ScriptRef
    {
        public readonly string guid;
        public readonly long fileId;

        public ScriptRef(string guid, long fileId)
        {
            this.guid = guid;
            this.fileId = fileId;
        }
    }

    public static RestoreResult Run(bool verboseLogging = true)
    {
        ScriptRegistryData registry = ScriptReferenceRegistry.Load();

        string assetsRoot = Application.dataPath;
        if (!Directory.Exists(assetsRoot))
        {
            return new RestoreResult(0, 0, 0, 0, 0);
        }

        string[] allFiles = Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories);

        int scannedFiles = 0;
        int modifiedFiles = 0;
        int restoredReferences = 0;
        int ambiguousReferences = 0;
        int unresolvedReferences = 0;

        foreach (string absolutePath in allFiles)
        {
            string relativePath = AbsoluteToProjectRelativePath(absolutePath);
            if (string.IsNullOrWhiteSpace(relativePath))
                continue;

            if (!ShouldScan(relativePath))
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

            scannedFiles++;

            string updatedText = originalText;
            bool fileChanged = false;

            MatchCollection blocks = MonoBehaviourBlockRegex.Matches(originalText);
            foreach (Match blockMatch in blocks)
            {
                string block = blockMatch.Value;
                Match scriptMatch = ScriptReferenceRegex.Match(block);
                if (!scriptMatch.Success)
                    continue;

                string guid = scriptMatch.Groups["guid"].Value;
                string fileIdText = scriptMatch.Groups["fileId"].Value;
                if (!long.TryParse(fileIdText, out long fileId))
                    continue;

                ScriptRef brokenRef = new(guid, fileId);

                if (IsValidMonoScriptReference(brokenRef))
                    continue;

                List<ScriptRecord> candidates = FindCandidatesByHistoricalIdentity(registry, brokenRef.guid, brokenRef.fileId);

                if (candidates.Count == 1)
                {
                    ScriptRecord record = candidates[0];

                    string replacement = $"m_Script: {{fileID: {record.current.fileId}, guid: {record.current.guid}, type: 3}}";
                    string replacedBlock = ScriptReferenceRegex.Replace(block, replacement, 1);

                    if (!string.Equals(replacedBlock, block, StringComparison.Ordinal))
                    {
                        updatedText = ReplaceFirst(updatedText, block, replacedBlock);
                        fileChanged = true;
                        restoredReferences++;

                        if (verboseLogging)
                        {
                            Debug.Log(
                                $"[ScriptReferenceRepair] Restored broken script reference in {relativePath} " +
                                $"from ({brokenRef.guid}, {brokenRef.fileId}) to ({record.current.guid}, {record.current.fileId}) " +
                                $"using {record.fullTypeName}");
                        }
                    }
                }
                else if (candidates.Count > 1)
                {
                    ambiguousReferences++;

                    if (verboseLogging)
                    {
                        string names = string.Join(", ", candidates.Select(x => x.fullTypeName));
                        Debug.LogWarning(
                            $"[ScriptReferenceRepair] Ambiguous broken script reference in {relativePath} " +
                            $"for ({brokenRef.guid}, {brokenRef.fileId}). Candidates: {names}");
                    }
                }
                else
                {
                    unresolvedReferences++;

                    if (verboseLogging)
                    {
                        Debug.LogWarning(
                            $"[ScriptReferenceRepair] Could not restore broken script reference in {relativePath} " +
                            $"for ({brokenRef.guid}, {brokenRef.fileId})");
                    }
                }
            }

            if (!fileChanged)
                continue;

            try
            {
                File.WriteAllText(absolutePath, updatedText);
                modifiedFiles++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScriptReferenceRepair] Failed writing restored file {relativePath}: {ex}");
            }
        }

        if (modifiedFiles > 0)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        Debug.Log(
            $"[ScriptReferenceRepair] Broken reference restore finished. " +
            $"Scanned files: {scannedFiles}, Modified files: {modifiedFiles}, " +
            $"Restored refs: {restoredReferences}, Ambiguous: {ambiguousReferences}, Unresolved: {unresolvedReferences}");

        return new RestoreResult(
            scannedFiles,
            modifiedFiles,
            restoredReferences,
            ambiguousReferences,
            unresolvedReferences);
    }

    private static List<ScriptRecord> FindCandidatesByHistoricalIdentity(ScriptRegistryData registry, string guid, long fileId)
    {
        List<ScriptRecord> results = new();

        for (int i = 0; i < registry.scripts.Count; i++)
        {
            ScriptRecord record = registry.scripts[i];

            bool historicalMatch = record.previousIdentities.Any(x =>
                string.Equals(x.guid, guid, StringComparison.OrdinalIgnoreCase) &&
                x.fileId == fileId);

            if (!historicalMatch)
                continue;

            if (string.IsNullOrWhiteSpace(record.current.guid))
                continue;

            results.Add(record);
        }

        return results;
    }

    private static bool IsValidMonoScriptReference(ScriptRef scriptRef)
    {
        string path = AssetDatabase.GUIDToAssetPath(scriptRef.guid);
        if (string.IsNullOrWhiteSpace(path))
            return false;

        MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        if (monoScript == null)
            return false;

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(monoScript, out string actualGuid, out long actualFileId))
            return false;

        return string.Equals(actualGuid, scriptRef.guid, StringComparison.OrdinalIgnoreCase)
                && actualFileId == scriptRef.fileId;
    }

    private static bool ShouldScan(string relativePath)
    {
        if (!relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return false;

        string extension = Path.GetExtension(relativePath);
        return ScannableExtensions.Contains(extension);
    }

    private static bool IsLikelyTextSerializedFile(string absolutePath)
    {
        try
        {
            using FileStream stream = new(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);

            char[] buffer = new char[128];
            int read = reader.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return false;

            string start = new(buffer, 0, read);
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

    private static string ReplaceFirst(string input, string search, string replacement)
    {
        int index = input.IndexOf(search, StringComparison.Ordinal);
        if (index < 0)
            return input;

        return input[..index] + replacement + input[(index + search.Length)..];
    }
}*/