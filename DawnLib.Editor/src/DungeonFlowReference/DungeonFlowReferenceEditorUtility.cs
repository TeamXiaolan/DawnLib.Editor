using System;
using System.Collections.Generic;
using System.Linq;
using DunGen;
using DunGen.Graph;
using UnityEditor;
using static Dusk.Utils.DungeonFlowReference;

namespace Dawn.Editor.PropertyDrawers;

internal static class DungeonFlowReferenceEditorUtility
{
    private const string FlowAssetGuidField = "_flowAssetGuid";
    private const string FlowAssetNameField = "_flowAssetName";
    private const string BundleNameField = "_bundleName";
    private const string DungeonArchetypeNamesField = "_dungeonArchetypeNames";
    private const string ArchetypeTileSetsField = "_archetypeTileSets";
    private const string GraphNodeReferencesField = "_graphNodeReferences";

    private const string ArchetypeNameField = "_archetypeName";
    private const string MappingTileSetNamesField = "_tileSetNames";
    private const string MappingBranchCapTileSetNamesField = "_branchCapTileSetNames";

    private const string GraphNodeReferenceTileSetNamesField = "_tileSetNames";

    public static DungeonFlow? ResolveDungeonFlow(SerializedProperty property)
    {
        SerializedProperty guidProperty = property.FindPropertyRelative(FlowAssetGuidField);
        SerializedProperty nameProperty = property.FindPropertyRelative(FlowAssetNameField);

        if (guidProperty == null || nameProperty == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(guidProperty.stringValue))
        {
            string path = AssetDatabase.GUIDToAssetPath(guidProperty.stringValue);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<DungeonFlow>(path);
        }

        if (string.IsNullOrEmpty(nameProperty.stringValue))
        {
            return null;
        }

        return FindDungeonFlowByExactName(nameProperty.stringValue);
    }

    public static void UpdateReference(SerializedProperty property, DungeonFlow dungeonFlow)
    {
        if (property == null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (dungeonFlow == null)
        {
            ClearReference(property);
            return;
        }

        SerializedProperty guidProperty = RequireRelative(property, FlowAssetGuidField);
        SerializedProperty nameProperty = RequireRelative(property, FlowAssetNameField);
        SerializedProperty bundleNameProperty = RequireRelative(property, BundleNameField);
        SerializedProperty dungeonArchetypeNamesProperty = RequireRelative(property, DungeonArchetypeNamesField);
        SerializedProperty archetypeTileSetsProperty = RequireRelative(property, ArchetypeTileSetsField);
        SerializedProperty graphNodeReferencesProperty = RequireRelative(property, GraphNodeReferencesField);

        string assetPath = AssetDatabase.GetAssetPath(dungeonFlow);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        AssetImporter? importer = AssetImporter.GetAtPath(assetPath);

        guidProperty.stringValue = guid;
        nameProperty.stringValue = dungeonFlow.name;
        bundleNameProperty.stringValue = importer?.assetBundleName ?? string.Empty;

        SortedDictionary<string, (HashSet<string> branchCapTileSetNames, HashSet<string> tileSetNames)> archetypeToTileSets = new(StringComparer.Ordinal);
        List<GraphNodeReference> graphNodeReferences = new();

        foreach (GraphNode graphNode in dungeonFlow.Nodes)
        {
            GraphNodeReference newReference = new GraphNodeReference
            {
                _tileSetNames = graphNode.TileSets.Select(tileSet => tileSet.name).ToArray()
            };
            graphNodeReferences.Add(newReference);
        }

        IEnumerable<DungeonArchetype> usedArchetypes = dungeonFlow.GetUsedArchetypes();
        foreach (DungeonArchetype archetype in usedArchetypes)
        {
            if (archetype == null)
                continue;

            string archetypeName = archetype.name;
            if (!archetypeToTileSets.TryGetValue(archetypeName, out (HashSet<string> branchCapTileSetNames, HashSet<string> tileSetNames) pair))
            {
                pair.branchCapTileSetNames = new HashSet<string>(StringComparer.Ordinal);
                pair.tileSetNames = new HashSet<string>(StringComparer.Ordinal);
                archetypeToTileSets.Add(archetypeName, pair);
            }

            foreach (TileSet tileSet in archetype.TileSets)
            {
                if (tileSet == null)
                    continue;

                pair.tileSetNames.Add(tileSet.name);
            }

            foreach (TileSet tileSet in archetype.BranchCapTileSets)
            {
                if (tileSet == null)
                    continue;

                pair.branchCapTileSetNames.Add(tileSet.name);
            }
        }

        SetStringArray(dungeonArchetypeNamesProperty, archetypeToTileSets.Keys);

        archetypeTileSetsProperty.ClearArray();
        foreach (KeyValuePair<string, (HashSet<string> branchCapTileSetNames, HashSet<string> tileSetNames)> pair in archetypeToTileSets)
        {
            int mappingIndex = archetypeTileSetsProperty.arraySize;
            archetypeTileSetsProperty.InsertArrayElementAtIndex(mappingIndex);

            SerializedProperty mappingProperty = archetypeTileSetsProperty.GetArrayElementAtIndex(mappingIndex);
            SerializedProperty archetypeNameProperty = RequireRelative(mappingProperty, ArchetypeNameField);
            SerializedProperty mappingTileSetNamesProperty = RequireRelative(mappingProperty, MappingTileSetNamesField);
            SerializedProperty mappingBranchCapTileSetNamesProperty = RequireRelative(mappingProperty, MappingBranchCapTileSetNamesField);

            archetypeNameProperty.stringValue = pair.Key;
            SetStringArray(mappingTileSetNamesProperty, pair.Value.tileSetNames);
            SetStringArray(mappingBranchCapTileSetNamesProperty, pair.Value.branchCapTileSetNames);
        }

        graphNodeReferencesProperty.ClearArray();
        foreach (GraphNodeReference graphNodeReference in graphNodeReferences)
        {
            int referenceIndex = graphNodeReferencesProperty.arraySize;
            graphNodeReferencesProperty.InsertArrayElementAtIndex(referenceIndex);

            SerializedProperty graphNodeReferenceProperty = graphNodeReferencesProperty.GetArrayElementAtIndex(referenceIndex);
            SerializedProperty graphNodeTileSetNamesProperty = RequireRelative(graphNodeReferenceProperty, GraphNodeReferenceTileSetNamesField);
            SetStringArray(graphNodeTileSetNamesProperty, graphNodeReference._tileSetNames);
        }
    }

    public static void ClearReference(SerializedProperty property)
    {
        if (property == null)
            return;

        SerializedProperty guidProperty = property.FindPropertyRelative(FlowAssetGuidField);
        SerializedProperty nameProperty = property.FindPropertyRelative(FlowAssetNameField);
        SerializedProperty bundleProperty = property.FindPropertyRelative(BundleNameField);
        SerializedProperty archetypeNamesProperty = property.FindPropertyRelative(DungeonArchetypeNamesField);
        SerializedProperty archetypeTileSetsProperty = property.FindPropertyRelative(ArchetypeTileSetsField);
        SerializedProperty graphNodeReferencesProperty = property.FindPropertyRelative(GraphNodeReferencesField);

        guidProperty?.stringValue = string.Empty;
        nameProperty?.stringValue = string.Empty;
        bundleProperty?.stringValue = string.Empty;

        archetypeNamesProperty?.ClearArray();
        archetypeTileSetsProperty?.ClearArray();
        graphNodeReferencesProperty?.ClearArray();
    }

    public static bool IsDungeonFlowReference(SerializedProperty property)
    {
        if (property == null || property.propertyType != SerializedPropertyType.Generic)
        {
            return false;
        }

        return
            property.FindPropertyRelative(FlowAssetGuidField) != null &&
            property.FindPropertyRelative(FlowAssetNameField) != null &&
            property.FindPropertyRelative(BundleNameField) != null &&
            property.FindPropertyRelative(DungeonArchetypeNamesField) != null &&
            property.FindPropertyRelative(ArchetypeTileSetsField) != null &&
            property.FindPropertyRelative(GraphNodeReferencesField) != null;
    }

    public static List<string> FindDungeonFlowReferencePropertyPaths(SerializedObject serializedObject)
    {
        List<string> paths = new();
        SerializedProperty iterator = serializedObject.GetIterator();

        while (iterator.Next(true))
        {
            if (!IsDungeonFlowReference(iterator))
                continue;

            paths.Add(iterator.propertyPath);
        }

        return paths;
    }

    public static void RefreshReferencesForFlows(IReadOnlyCollection<DungeonFlow> flows, IReadOnlyDictionary<string, HashSet<string>>? legacyNamesByGuid = null)
    {
        if (flows == null || flows.Count == 0)
            return;

        Dictionary<string, DungeonFlow> flowsByGuid = new(StringComparer.Ordinal);

        foreach (DungeonFlow flow in flows)
        {
            if (flow == null)
                continue;

            string path = AssetDatabase.GetAssetPath(flow);
            if (string.IsNullOrEmpty(path))
                continue;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                continue;

            flowsByGuid[guid] = flow;
        }

        if (flowsByGuid.Count == 0)
            return;

        foreach (UnityEngine.Object definition in FindAllDuskDungeonDefinitions())
        {
            SerializedObject serializedObject = new(definition);
            serializedObject.Update();
            List<string> referencePaths = FindDungeonFlowReferencePropertyPaths(serializedObject);

            bool touched = false;
            foreach (string propertyPath in referencePaths)
            {
                SerializedProperty referenceProperty = serializedObject.FindProperty(propertyPath);

                if (referenceProperty == null)
                    continue;

                DungeonFlow? matchingFlow = FindMatchingAffectedFlow(referenceProperty, flowsByGuid, legacyNamesByGuid);
                if (matchingFlow == null)
                    continue;

                UpdateReference(referenceProperty, matchingFlow);
                touched = true;
            }

            if (!touched)
                continue;

            if (serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(definition);
            }
        }
    }

    public static void RefreshAllReferences()
    {
        foreach (UnityEngine.Object definition in FindAllDuskDungeonDefinitions())
        {
            SerializedObject serializedObject = new(definition);
            serializedObject.Update();
            List<string> referencePaths = FindDungeonFlowReferencePropertyPaths(serializedObject);

            bool touched = false;
            foreach (string propertyPath in referencePaths)
            {
                SerializedProperty referenceProperty = serializedObject.FindProperty(propertyPath);
                if (referenceProperty == null)
                    continue;

                ResolutionResult resolution = ResolveDungeonFlowDetailed(referenceProperty);
                if (resolution.Flow != null)
                {
                    UpdateReference(referenceProperty, resolution.Flow);
                    touched = true;
                    continue;
                }

                if (resolution.DefinitelyMissing)
                {
                    ClearReference(referenceProperty);
                    touched = true;
                }
            }

            if (!touched)
                continue;

            if (serializedObject.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(definition);
            }
        }
    }

    private static DungeonFlow? FindMatchingAffectedFlow(SerializedProperty referenceProperty, IReadOnlyDictionary<string, DungeonFlow> flowsByGuid, IReadOnlyDictionary<string, HashSet<string>>? legacyNamesByGuid)
    {
        SerializedProperty guidProperty = referenceProperty.FindPropertyRelative(FlowAssetGuidField);
        SerializedProperty nameProperty = referenceProperty.FindPropertyRelative(FlowAssetNameField);
        if (guidProperty == null || nameProperty == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(guidProperty.stringValue))
        {
            return flowsByGuid.TryGetValue(guidProperty.stringValue, out DungeonFlow flow) ? flow : null;
        }

        string storedName = nameProperty.stringValue;
        if (string.IsNullOrEmpty(storedName))
            return null;

        DungeonFlow? result = null;

        foreach (KeyValuePair<string, DungeonFlow> pair in flowsByGuid)
        {
            DungeonFlow flow = pair.Value;
            bool matchesCurrentName = string.Equals(flow.name, storedName, StringComparison.Ordinal);

            bool matchesPreviousName = false;
            if (legacyNamesByGuid != null && legacyNamesByGuid.TryGetValue(pair.Key, out HashSet<string> legacyNames))
            {
                matchesPreviousName = legacyNames.Contains(storedName);
            }

            if (!matchesCurrentName && !matchesPreviousName)
            {
                continue;
            }

            if (result != null && result != flow)
            {
                return null;
            }

            result = flow;
        }

        return result;
    }

    private static ResolutionResult ResolveDungeonFlowDetailed(SerializedProperty referenceProperty)
    {
        SerializedProperty guidProperty = referenceProperty.FindPropertyRelative(FlowAssetGuidField);
        SerializedProperty nameProperty = referenceProperty.FindPropertyRelative(FlowAssetNameField);
        if (guidProperty == null || nameProperty == null)
            return default;

        string guid = guidProperty.stringValue;
        string name = nameProperty.stringValue;

        if (!string.IsNullOrEmpty(guid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
            {
                return new ResolutionResult(null, true);
            }

            DungeonFlow flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(path);

            return new ResolutionResult(flow, flow == null);
        }

        if (string.IsNullOrEmpty(name))
            return default;

        List<DungeonFlow> matches = FindDungeonFlowsByExactName(name);

        if (matches.Count == 1)
        {
            return new ResolutionResult(matches[0], false);
        }

        return new ResolutionResult(null, matches.Count == 0);
    }

    private static DungeonFlow? FindDungeonFlowByExactName(string name)
    {
        List<DungeonFlow> matches = FindDungeonFlowsByExactName(name);
        return matches.FirstOrDefault();
    }

    private static List<DungeonFlow> FindDungeonFlowsByExactName(string name)
    {
        List<DungeonFlow> matches = new();
        if (string.IsNullOrEmpty(name))
        {
            return matches;
        }

        string[] guids = AssetDatabase.FindAssets("t:DungeonFlow");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DungeonFlow? flow = AssetDatabase.LoadAssetAtPath<DungeonFlow>(path);
            if (flow == null)
                continue;

            if (!string.Equals(flow.name, name, StringComparison.Ordinal))
            {
                continue;
            }

            matches.Add(flow);
        }

        return matches;
    }

    private static IEnumerable<UnityEngine.Object> FindAllDuskDungeonDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:DuskDungeonDefinition");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object definition = AssetDatabase.LoadMainAssetAtPath(path);
            if (definition != null)
            {
                yield return definition;
            }
        }
    }

    private static void SetStringArray(SerializedProperty property, IEnumerable<string> values)
    {
        property.ClearArray();
        foreach (string value in values)
        {
            int index = property.arraySize;

            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).stringValue = value;
        }
    }

    private static SerializedProperty RequireRelative(SerializedProperty property, string relativeName)
    {
        SerializedProperty child = property.FindPropertyRelative(relativeName);
        if (child != null)
        {
            return child;
        }

        throw new InvalidOperationException($"DungeonFlowReference property '{property.propertyPath}' does not contain expected serialized field '{relativeName}'.");
    }

    private readonly struct ResolutionResult(DungeonFlow? flow, bool definitelyMissing)
    {
        public readonly DungeonFlow? Flow = flow;
        public readonly bool DefinitelyMissing = definitelyMissing;
    }
}