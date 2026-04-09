using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(SpawnableMapObject))]
public class SpawnableMapObjectDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a valid MapObject";

        SerializedProperty prefabToSpawnProp = property.FindPropertyRelative(nameof(SpawnableMapObject.prefabToSpawn));

        if (prefabToSpawnProp != null && prefabToSpawnProp.objectReferenceValue != null)
        {
            GameObject? mapObjectGameObject = prefabToSpawnProp.objectReferenceValue as GameObject;
            if (mapObjectGameObject != null && !string.IsNullOrEmpty(mapObjectGameObject.name))
            {
                displayName = $"{mapObjectGameObject.name}";
            }
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}