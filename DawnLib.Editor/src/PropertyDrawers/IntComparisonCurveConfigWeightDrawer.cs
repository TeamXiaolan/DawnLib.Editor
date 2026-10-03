using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using Dusk.Weights;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(IntComparisonCurveConfigWeight))]
public class IntComparisonCurveConfigWeightDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "You should never see this.";
        if (property.GetTargetObjectOfProperty() is IntComparisonCurveConfigWeight data)
        {
            // End Result: {Comparison}{Value}
            // End Result: <=100;

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

            displayName = $"{Comparison}{data.IntComparison.Value}";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}