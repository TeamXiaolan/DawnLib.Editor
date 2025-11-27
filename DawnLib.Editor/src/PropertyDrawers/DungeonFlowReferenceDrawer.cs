using System.Collections.Generic;
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
                foreach (var tileSet in pickedDungeonFlow.GetUsedTileSets())
                {
                    int newIndex = tileSetNamesProperty.arraySize;
                    tileSetNamesProperty.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty elementProp = tileSetNamesProperty.GetArrayElementAtIndex(newIndex);
                    elementProp.stringValue = tileSet.name;
                }

                dungeonArchetypeNamesProperty.ClearArray();
                foreach (var archetype in pickedDungeonFlow.GetUsedArchetypes())
                {
                    int newIndex = dungeonArchetypeNamesProperty.arraySize;
                    dungeonArchetypeNamesProperty.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty elementProp = dungeonArchetypeNamesProperty.GetArrayElementAtIndex(newIndex);
                    elementProp.stringValue = archetype.name;
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
                propsChanged = true;
            }
        }
        else
        {
            // Keep bundle name in sync with asset importer
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