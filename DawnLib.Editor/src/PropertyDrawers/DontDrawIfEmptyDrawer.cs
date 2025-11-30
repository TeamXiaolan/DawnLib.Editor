using UnityEditor;
using UnityEngine;
using Dusk;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(DontDrawIfEmpty))]
public class DontDrawIfEmptyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!ShouldDraw(property))
            return;

        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldDraw(property))
        {
            return 0f;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    private bool ShouldDraw(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            return !string.IsNullOrEmpty(property.stringValue);
        }

        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            return property.objectReferenceValue != null;
        }

        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            return property.arraySize > 0;
        }
        return true;
    }
}