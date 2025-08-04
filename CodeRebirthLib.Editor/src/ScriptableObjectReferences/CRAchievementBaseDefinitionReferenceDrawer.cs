using UnityEditor;
using UnityEngine;
using CodeRebirthLib.ContentManagement.Achievements;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;
[CustomPropertyDrawer(typeof(CRAchievementBaseDefinitionReference))]
public class CRAchievementBaseDefinitionReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty achievementAssetProp = property.FindPropertyRelative("achievementAsset");
        SerializedProperty achievementNameProp = property.FindPropertyRelative("achievementName");

        EditorGUI.BeginProperty(position, label, property);

        EditorGUI.BeginChangeCheck();
        CRAchievementBaseDefinition newAchievement = (CRAchievementBaseDefinition)EditorGUI.ObjectField(position, label, achievementAssetProp.objectReferenceValue, typeof(CRAchievementBaseDefinition), false);
        if (EditorGUI.EndChangeCheck())
        {
            achievementAssetProp.objectReferenceValue = newAchievement;
            achievementNameProp.stringValue = newAchievement != null ? newAchievement.name : "";
        }

        EditorGUI.EndProperty();
    }
}