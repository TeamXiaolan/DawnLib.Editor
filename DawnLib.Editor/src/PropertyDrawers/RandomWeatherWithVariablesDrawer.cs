using System;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(RandomWeatherWithVariables))]
public class RandomWeatherWithVariablesDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a valid Weather";

        SerializedProperty weatherProp = property.FindPropertyRelative(nameof(RandomWeatherWithVariables.weatherType));

        if (weatherProp != null)
        {
            Enum? weatherEnum = (LevelWeatherType)weatherProp.enumValueFlag;
            if (weatherEnum != null && !string.IsNullOrEmpty(weatherEnum.ToString()))
            {
                int weatherVariable1 = property.FindPropertyRelative(nameof(RandomWeatherWithVariables.weatherVariable)).intValue;
                int weatherVariable2 = property.FindPropertyRelative(nameof(RandomWeatherWithVariables.weatherVariable2)).intValue;
                displayName = $"{weatherEnum.ToString()} - {weatherVariable1} | {weatherVariable2}";
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