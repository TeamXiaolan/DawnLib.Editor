using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using Dusk;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(NamespacedConfigWeight))]
public class NamespacedConfigWeightDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a valid Key and Namespace";

        if (property.GetTargetObjectOfProperty() is NamespacedConfigWeight data && !string.IsNullOrEmpty(data.NamespacedKey.Namespace) && !string.IsNullOrEmpty(data.NamespacedKey.Key))
        {
            string operation = data.MathOperation switch
            {
                MathOperation.Additive => "+",
                MathOperation.Subtractive => "-",
                MathOperation.Multiplicative => "*",
                MathOperation.Divisive => "/",
                _ => "+",
            };
            displayName = $"{data.NamespacedKey}={operation}{data.Weight}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}