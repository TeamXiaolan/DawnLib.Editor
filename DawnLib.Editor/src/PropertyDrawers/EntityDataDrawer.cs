using Dawn.Dusk;
using Dawn.Editor.Extensions;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(EntityData), true)]
public class EntityDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        object? obj = property.GetTargetObjectOfProperty();
        if (obj is EntityData entity)
        {
            string? keyLabel = entity.Key?.ToString();
            if (!string.IsNullOrEmpty(keyLabel) && keyLabel != ":")
            {
                label = new GUIContent(keyLabel);
            }
            else
            {
                label = new GUIContent("Empty Entity");
            }
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}