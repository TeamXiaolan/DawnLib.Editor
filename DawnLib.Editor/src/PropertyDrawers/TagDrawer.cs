using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using DunGen.Tags;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(Tag))]
public class TagDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = string.Empty;

        if (property.GetTargetObjectOfProperty() is Tag data)
        {
            displayName = $"ID: [{data.ID}]";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}