using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using DunGen.Graph;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(GraphLine))]
public class GraphLineDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide atleast one Archetype";

        if (property.GetTargetObjectOfProperty() is GraphLine data && data.DungeonArchetypes != null && data.DungeonArchetypes.Count > 0)
        {
            string archetypes = string.Empty;
            foreach (var archetype in data.DungeonArchetypes)
            {
                archetypes += $"{archetype.name}:";
            }
            archetypes = archetypes[..^1];
            displayName = $"Pos: {data.Position} | Length: {data.Length} | {archetypes}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}