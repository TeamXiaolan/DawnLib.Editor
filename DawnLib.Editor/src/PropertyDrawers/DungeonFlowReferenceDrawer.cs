using System;
using System.Collections.Generic;
using System.Linq;
using DunGen.Graph;
using Dusk.Utils;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(DungeonFlowReference))]
public class DungeonFlowReferenceDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, DungeonFlow?> DungeonFlowCache = new();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty nameProperty = property.FindPropertyRelative("_flowAssetName");
        SerializedProperty bundleNameProp = property.FindPropertyRelative("_bundleName");
        SerializedProperty tileSetNamesProperty = property.FindPropertyRelative("_tileSetNames");
        SerializedProperty dungeonArchetypeNamesProperty = property.FindPropertyRelative("_dungeonArchetypeNames");
        SerializedProperty archetypeTileSetsProperty = property.FindPropertyRelative("_archetypeTileSets");

        property.serializedObject.Update();
        EditorGUI.BeginProperty(position, label, property);

        string currentName = nameProperty.stringValue ?? string.Empty;
        DungeonFlow? currentDungeonFlow = null;

        // Try to load from cache
        if (!string.IsNullOrEmpty(currentName))
        {
            if (!DungeonFlowCache.TryGetValue(currentName, out var cachedFlow) || cachedFlow == null)
            {
                currentDungeonFlow = LoadDungeonFlowByName(currentName);
                if (currentDungeonFlow != null)
                {
                    DungeonFlowCache[currentName] = currentDungeonFlow;
                }
            }
            else
            {
                currentDungeonFlow = cachedFlow;
            }

            if (currentDungeonFlow == null)
            {
                DungeonFlowCache.Remove(currentName);
                nameProperty.stringValue = string.Empty;
                bundleNameProp.stringValue = string.Empty;
                tileSetNamesProperty.ClearArray();
                dungeonArchetypeNamesProperty.ClearArray();
                archetypeTileSetsProperty.ClearArray();
                currentName = string.Empty;
            }
        }

        EditorGUI.BeginChangeCheck();
        var pickedDungeonFlow = (DungeonFlow)EditorGUI.ObjectField(position, label, currentDungeonFlow, typeof(DungeonFlow), false);

        bool propsChanged = false;

        if (EditorGUI.EndChangeCheck())
        {
            if (pickedDungeonFlow != null)
            {
                string flowName = pickedDungeonFlow.name;
                string assetPath = AssetDatabase.GetAssetPath(pickedDungeonFlow);
                var importer = AssetImporter.GetAtPath(assetPath);
                string bundle = importer?.assetBundleName ?? string.Empty;

                nameProperty.stringValue = flowName;
                bundleNameProp.stringValue = bundle;
                tileSetNamesProperty.ClearArray();
                dungeonArchetypeNamesProperty.ClearArray();
                archetypeTileSetsProperty.ClearArray();

                var archetypeToTileSets = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
                var allTileSetNames = new HashSet<string>(StringComparer.Ordinal);

                foreach (var archetype in pickedDungeonFlow.GetUsedArchetypes())
                {
                    if (archetype == null)
                        continue;

                    string archetypeName = archetype.name;

                    if (!archetypeToTileSets.TryGetValue(archetypeName, out var tileSetSet))
                    {
                        tileSetSet = new HashSet<string>(StringComparer.Ordinal);
                        archetypeToTileSets.Add(archetypeName, tileSetSet);
                    }

                    foreach (var tileSet in archetype.TileSets)
                    {
                        if (tileSet == null)
                            continue;

                        tileSetSet.Add(tileSet.name);
                        allTileSetNames.Add(tileSet.name);
                    }
                }

                foreach (var kvp in archetypeToTileSets)
                {
                    int newIndex = dungeonArchetypeNamesProperty.arraySize;
                    dungeonArchetypeNamesProperty.InsertArrayElementAtIndex(newIndex);
                    dungeonArchetypeNamesProperty.GetArrayElementAtIndex(newIndex).stringValue = kvp.Key;
                }

                var sortedTileSets = allTileSetNames.ToList();
                sortedTileSets.Sort(StringComparer.Ordinal);

                foreach (var tileSetName in sortedTileSets)
                {
                    int newIndex = tileSetNamesProperty.arraySize;
                    tileSetNamesProperty.InsertArrayElementAtIndex(newIndex);
                    tileSetNamesProperty.GetArrayElementAtIndex(newIndex).stringValue = tileSetName;
                }

                int mappingIndex = 0;
                foreach (var kvp in archetypeToTileSets)
                {
                    archetypeTileSetsProperty.InsertArrayElementAtIndex(mappingIndex);
                    var mappingProp = archetypeTileSetsProperty.GetArrayElementAtIndex(mappingIndex);

                    var archetypeNameProp = mappingProp.FindPropertyRelative("_archetypeName");
                    var mappingTileSetNamesProp = mappingProp.FindPropertyRelative("_tileSetNames");

                    archetypeNameProp.stringValue = kvp.Key;

                    mappingTileSetNamesProp.ClearArray();
                    var perArchetypeTileSets = kvp.Value.ToList();
                    perArchetypeTileSets.Sort(StringComparer.Ordinal);

                    for (int i = 0; i < perArchetypeTileSets.Count; i++)
                    {
                        mappingTileSetNamesProp.InsertArrayElementAtIndex(i);
                        mappingTileSetNamesProp.GetArrayElementAtIndex(i).stringValue = perArchetypeTileSets[i];
                    }

                    mappingIndex++;
                }

                DungeonFlowCache[flowName] = pickedDungeonFlow;
                propsChanged = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(nameProperty.stringValue))
                {
                    DungeonFlowCache.Remove(nameProperty.stringValue);
                }

                nameProperty.stringValue = string.Empty;
                bundleNameProp.stringValue = string.Empty;
                tileSetNamesProperty.ClearArray();
                dungeonArchetypeNamesProperty.ClearArray();
                archetypeTileSetsProperty.ClearArray();
                propsChanged = true;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(nameProperty.stringValue) && currentDungeonFlow != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(currentDungeonFlow);
                var importer = AssetImporter.GetAtPath(assetPath);
                string importerBundle = importer?.assetBundleName ?? string.Empty;

                if (bundleNameProp.stringValue != importerBundle)
                {
                    bundleNameProp.stringValue = importerBundle;
                    propsChanged = true;
                }
            }
        }

        if (propsChanged)
        {
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorGUI.EndProperty();
    }

    private static DungeonFlow? LoadDungeonFlowByName(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:DungeonFlow");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<DungeonFlow>(path);
        }
        return null;
    }
}