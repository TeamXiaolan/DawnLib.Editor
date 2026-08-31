using DunGen.Graph;
using Dusk.Utils;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(DungeonFlowReference))]
public sealed class DungeonFlowReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        DungeonFlow? currentDungeonFlow = DungeonFlowReferenceEditorUtility.ResolveDungeonFlow(property);
        SerializedProperty guidProperty = property.FindPropertyRelative("_flowAssetGuid");

        if (currentDungeonFlow != null && guidProperty != null && string.IsNullOrEmpty(guidProperty.stringValue))
        {
            DungeonFlowReferenceEditorUtility.UpdateReference(property, currentDungeonFlow);
        }

        EditorGUI.BeginChangeCheck();
        DungeonFlow pickedDungeonFlow = (DungeonFlow)EditorGUI.ObjectField(position, label, currentDungeonFlow, typeof(DungeonFlow), false);

        if (EditorGUI.EndChangeCheck())
        {
            if (pickedDungeonFlow != null)
            {
                DungeonFlowReferenceEditorUtility.UpdateReference(property, pickedDungeonFlow);
            }
            else
            {
                DungeonFlowReferenceEditorUtility.ClearReference(property);
            }
        }

        EditorGUI.EndProperty();
    }
}