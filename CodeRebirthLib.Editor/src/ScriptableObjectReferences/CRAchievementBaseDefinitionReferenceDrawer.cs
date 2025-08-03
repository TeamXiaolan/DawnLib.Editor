using UnityEditor;
using UnityEngine;
using CodeRebirthLib.ContentManagement.Achievements;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;
[CustomPropertyDrawer(typeof(CRAchievementBaseDefinitionReference))]
public class CRAchievementBaseDefinitionReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
#if UNITY_EDITOR
        SerializedProperty achievementAssetProp = property.FindPropertyRelative("achievementAsset");
        SerializedProperty achievementNameProp = property.FindPropertyRelative("achievementName");

        EditorGUI.BeginProperty(position, label, property);

        EditorGUI.BeginChangeCheck();
        CRAchievementBaseDefinitionAsset newAchievement = (CRAchievementBaseDefinitionAsset)EditorGUI.ObjectField(position, label, achievementAssetProp.objectReferenceValue, typeof(CRAchievementBaseDefinitionAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            achievementAssetProp.objectReferenceValue = newAchievement;
            achievementNameProp.stringValue = newAchievement != null ? newAchievement.name : "";
        }

        EditorGUI.EndProperty();
#endif
    }
}