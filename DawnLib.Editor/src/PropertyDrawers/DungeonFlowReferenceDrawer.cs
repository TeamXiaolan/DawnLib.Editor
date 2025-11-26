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

        if (nameProperty == null || bundleNameProp == null)
        {
            EditorGUI.HelpBox(position, "DungeonFlowReference must have _flowAssetName and _bundleName fields", MessageType.Error);
            return;
        }

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

                DungeonFlowCache[flowName] = pickedDungeonFlow;
                propsChanged = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(nameProperty.stringValue))
                    DungeonFlowCache.Remove(nameProperty.stringValue);

                nameProperty.stringValue = string.Empty;
                bundleNameProp.stringValue = string.Empty;
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