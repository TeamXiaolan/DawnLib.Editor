using Dusk.Utils;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(SceneReference))]
public class SceneReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty scenePathProp = property.FindPropertyRelative("_scenePath");
        SerializedProperty assetGuidProp = property.FindPropertyRelative("_assetGUID");
        SerializedProperty bundleNameProp = property.FindPropertyRelative("_bundleName");

        property.serializedObject.Update();

        EditorGUI.BeginProperty(position, label, property);

        SceneAsset? currentSceneAsset = string.IsNullOrEmpty(scenePathProp.stringValue) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePathProp.stringValue);

        EditorGUI.BeginChangeCheck();

        SceneAsset pickedSceneAsset = (SceneAsset)EditorGUI.ObjectField(position, label, currentSceneAsset, typeof(SceneAsset), false);

        if (EditorGUI.EndChangeCheck())
        {
            if (pickedSceneAsset != null)
            {
                string path = AssetDatabase.GetAssetPath(pickedSceneAsset);
                string guid = AssetDatabase.GUIDFromAssetPath(path).ToString();

                scenePathProp.stringValue = path;
                assetGuidProp.stringValue = guid;
                bundleNameProp.stringValue = AssetImporter.GetAtPath(path)?.assetBundleName ?? string.Empty;
            }
            else
            {
                scenePathProp.stringValue = string.Empty;
                assetGuidProp.stringValue = string.Empty;
                bundleNameProp.stringValue = string.Empty;
            }

            property.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.EndProperty();
    }
}