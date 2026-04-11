using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using Dusk;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(IntComparisonConfigWeight))]
public class IntComparisonConfigWeightDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "You should never see this.";
        if (property.GetTargetObjectOfProperty() is IntComparisonConfigWeight data)
        {
            // End Result: {Comparison}{Value}={Operation}{Weight}
            // End Result: <=100=+9999;

            string Comparison = data.IntComparison.ComparisonOperation switch
            {
                ComparisonOperation.Equal => "==",
                ComparisonOperation.NotEqual => "!=",
                ComparisonOperation.Greater => ">",
                ComparisonOperation.Less => "<",
                ComparisonOperation.GreaterOrEqual => ">=",
                ComparisonOperation.LessOrEqual => "<=",
                _ => "==",
            };

            string Operation;
            Operation = data.MathOperation switch
            {
                MathOperation.Additive => "+",
                MathOperation.Subtractive => "-",
                MathOperation.Multiplicative => "*",
                MathOperation.Divisive => "/",
                _ => "+",
            };

            displayName = $"{Comparison}{data.IntComparison.Value}={Operation}{data.Weight}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}