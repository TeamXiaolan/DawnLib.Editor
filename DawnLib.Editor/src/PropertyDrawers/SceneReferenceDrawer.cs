using System.Collections.Generic;
using Dusk.Utils;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(SceneReference))]
public class SceneReferenceDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, SceneAsset?> SceneReferencesDict = new();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty scenePathProp  = property.FindPropertyRelative("_scenePath");
        SerializedProperty assetGuidProp  = property.FindPropertyRelative("_assetGUID");
        SerializedProperty bundleNameProp = property.FindPropertyRelative("_bundleName");

        property.serializedObject.Update();
        EditorGUI.BeginProperty(position, label, property);

        if (!string.IsNullOrEmpty(assetGuidProp.stringValue))
        {
            string guidPath = AssetDatabase.GUIDToAssetPath(assetGuidProp.stringValue);
            if (!string.IsNullOrEmpty(guidPath) && guidPath != scenePathProp.stringValue)
            {
                scenePathProp.stringValue = guidPath;
            }
        }

        SceneAsset? currentSceneAsset = null;
        string currentPath = scenePathProp.stringValue;

        if (!string.IsNullOrEmpty(currentPath))
        {
            if (!SceneReferencesDict.TryGetValue(currentPath, out currentSceneAsset) || currentSceneAsset == null)
            {
                currentSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(currentPath);
                if (currentSceneAsset != null)
                {
                    SceneReferencesDict[currentPath] = currentSceneAsset;
                }
            }

            if (currentSceneAsset == null)
            {
                SceneReferencesDict.Remove(currentPath);

                scenePathProp.stringValue = string.Empty;
                assetGuidProp.stringValue = string.Empty;
                bundleNameProp.stringValue = string.Empty;
                currentPath = string.Empty;
            }
        }

        EditorGUI.BeginChangeCheck();
        var pickedSceneAsset = (SceneAsset)EditorGUI.ObjectField(position, label, currentSceneAsset, typeof(SceneAsset), false);

        bool propsChanged = false;

        if (EditorGUI.EndChangeCheck())
        {
            if (pickedSceneAsset != null)
            {
                string path = AssetDatabase.GetAssetPath(pickedSceneAsset);
                string guid = AssetDatabase.GUIDFromAssetPath(path).ToString();

                var importer = AssetImporter.GetAtPath(path);
                string bundle = importer != null ? importer.assetBundleName ?? string.Empty : string.Empty;

                scenePathProp.stringValue  = path;
                assetGuidProp.stringValue  = guid;
                bundleNameProp.stringValue = bundle;

                SceneReferencesDict[path] = pickedSceneAsset;
                propsChanged = true;
            }
            else
            {
                if (!string.IsNullOrEmpty(scenePathProp.stringValue))
                    SceneReferencesDict.Remove(scenePathProp.stringValue);

                scenePathProp.stringValue  = string.Empty;
                assetGuidProp.stringValue  = string.Empty;
                bundleNameProp.stringValue = string.Empty;
                propsChanged = true;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(scenePathProp.stringValue) && currentSceneAsset != null)
            {
                var importer = AssetImporter.GetAtPath(scenePathProp.stringValue);
                string importerBundle = importer != null ? importer.assetBundleName ?? string.Empty : string.Empty;

                if (bundleNameProp.stringValue != importerBundle)
                {
                    bundleNameProp.stringValue = importerBundle;
                    propsChanged = true;
                }

                if (string.IsNullOrEmpty(assetGuidProp.stringValue))
                {
                    string guid = AssetDatabase.GUIDFromAssetPath(scenePathProp.stringValue).ToString();
                    if (!string.IsNullOrEmpty(guid))
                    {
                        assetGuidProp.stringValue = guid;
                        propsChanged = true;
                    }
                }
            }
        }

        if (propsChanged)
        {
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorGUI.EndProperty();
    }
}