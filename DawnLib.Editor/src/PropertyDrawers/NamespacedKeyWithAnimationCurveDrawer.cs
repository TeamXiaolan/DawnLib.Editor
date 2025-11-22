using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using Dusk;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(NamespacedKeyWithAnimationCurve))]
public class NamespacedKeyWithAnimationCurveDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a valid Key and Namespace and Animation Curve";

        if (property.GetTargetObjectOfProperty() is NamespacedKeyWithAnimationCurve data && !string.IsNullOrEmpty(data.Key.Namespace) && !string.IsNullOrEmpty(data.Key.Key) && data.Curve.keys.Length > 0)
        {
            displayName = $"{data.Key}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}