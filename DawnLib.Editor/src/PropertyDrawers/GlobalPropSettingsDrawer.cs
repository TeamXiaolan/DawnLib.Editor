using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using static DunGen.Graph.DungeonFlow;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(GlobalPropSettings))]
public class GlobalPropSettingsDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Min is higher than Max";

        if (property.GetTargetObjectOfProperty() is GlobalPropSettings data)
        {
            if (data.Count.Min <= data.Count.Max)
            {
                displayName = $"ID: {data.ID} | Min: {data.Count.Min} | Max: {data.Count.Max}";
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