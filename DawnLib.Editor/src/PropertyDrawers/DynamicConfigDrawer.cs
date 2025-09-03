using Dawn.Dusk;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(DuskDynamicConfig))]
public class DynamicConfigDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect settingNameRect = new Rect(position.x, position.y, position.width, lineHeight);
        Rect typeRect = new Rect(position.x, position.y + (lineHeight + spacing), position.width, lineHeight);
        Rect defaultRect = new Rect(position.x, position.y + (lineHeight + spacing) * 2, position.width, lineHeight);
        Rect descRect = new Rect(position.x, position.y + (lineHeight + spacing) * 3, position.width, lineHeight);

        EditorGUI.PropertyField(settingNameRect, property.FindPropertyRelative("settingName"), new GUIContent("settingName"));
        EditorGUI.PropertyField(typeRect, property.FindPropertyRelative("DynamicConfigType"), new GUIContent("Type"));

        SerializedProperty dynamicTypeProp = property.FindPropertyRelative("DynamicConfigType");
        DuskDynamicConfigType configType = (DuskDynamicConfigType)dynamicTypeProp.enumValueIndex;
        switch (configType)
        {
            case DuskDynamicConfigType.String:
                EditorGUI.PropertyField(defaultRect, property.FindPropertyRelative("defaultString"), new GUIContent("Default Value"));
                break;
            case DuskDynamicConfigType.Int:
                EditorGUI.PropertyField(defaultRect, property.FindPropertyRelative("defaultInt"), new GUIContent("Default Value"));
                break;
            case DuskDynamicConfigType.Float:
                EditorGUI.PropertyField(defaultRect, property.FindPropertyRelative("defaultFloat"), new GUIContent("Default Value"));
                break;
            case DuskDynamicConfigType.Bool:
                EditorGUI.PropertyField(defaultRect, property.FindPropertyRelative("defaultBool"), new GUIContent("Default Value"));
                break;
            case DuskDynamicConfigType.BoundedRange:
                var boundedRangeProperty = property.FindPropertyRelative("defaultBoundedRange");
                if (boundedRangeProperty.isExpanded)
                    descRect = new Rect(position.x, position.y + (lineHeight + spacing) * 5, position.width, lineHeight);
                EditorGUI.PropertyField(defaultRect, boundedRangeProperty, new GUIContent("Default Value"), true);
                break;
            case DuskDynamicConfigType.AnimationCurve:
                EditorGUI.PropertyField(defaultRect, property.FindPropertyRelative("defaultAnimationCurve"), new GUIContent("Default Value"));
                break;
        }

        EditorGUI.PropertyField(descRect, property.FindPropertyRelative("Description"), new GUIContent("Description"));
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        // Four lines (Key, Type, Default Value, Description) plus spacing.
        SerializedProperty dynamicTypeProp = property.FindPropertyRelative("DynamicConfigType");
        DuskDynamicConfigType configType = (DuskDynamicConfigType)dynamicTypeProp.enumValueIndex;
        var boundedRangeProperty = property.FindPropertyRelative("defaultBoundedRange");
        return (lineHeight * (configType == DuskDynamicConfigType.BoundedRange && boundedRangeProperty.isExpanded ? 6.25f : 4)) + (spacing * 3);
    }
}