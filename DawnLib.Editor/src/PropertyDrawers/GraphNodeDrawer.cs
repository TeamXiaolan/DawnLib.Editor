using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using DunGen.Graph;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(GraphNode))]
public class GraphNodeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide atleast one TileSet";

        if (property.GetTargetObjectOfProperty() is GraphNode data && data.TileSets != null && data.TileSets.Count > 0)
        {
            string tileSets = string.Empty;
            foreach (var tileSet in data.TileSets)
            {
                tileSets += $"{tileSet.name}:";
            }
            tileSets = tileSets[..^1];
            displayName = $"Type: {data.NodeType} | Pos: {data.Position} | Label: {data.Label} | {tileSets}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}