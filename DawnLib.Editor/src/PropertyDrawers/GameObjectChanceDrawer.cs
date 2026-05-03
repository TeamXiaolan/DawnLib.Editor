using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using DunGen;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(GameObjectChance))]
public class GameObjectChanceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a Valid Prefab";

        if (property.GetTargetObjectOfProperty() is GameObjectChance data && data.Value != null)
        {
            displayName = $"{data.Value.name} | MPW: {data.MainPathWeight} | BPW: {data.BranchPathWeight}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}