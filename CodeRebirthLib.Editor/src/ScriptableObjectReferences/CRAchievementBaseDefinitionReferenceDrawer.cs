using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CodeRebirthLib.ContentManagement.Achievements;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;
[CustomPropertyDrawer(typeof(CRAchievementBaseDefinitionReference))]
public class CRAchievementBaseDefinitionReferenceDrawer : PropertyDrawer {
    static Dictionary<string, string> mappedGuids = new Dictionary<string, string>();
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty achievementAssetProp = property.FindPropertyRelative("achievementAsset");
        SerializedProperty achievementNameProp = property.FindPropertyRelative("achievementName");
        
        EditorGUI.BeginProperty(position, label, property);

        CRAchievementBaseDefinition oldAchivement = null;
        if(achievementAssetProp.stringValue != null) {
            string guid = achievementAssetProp.stringValue;
            if(!mappedGuids.TryGetValue(guid, out string path)) {
                path = AssetDatabase.GUIDToAssetPath(guid);
                mappedGuids[guid] = path;
            }

            if(!string.IsNullOrEmpty(path)) {
                oldAchivement = AssetDatabase.LoadAssetAtPath<CRAchievementBaseDefinition>(path);
            }
        }
        
        EditorGUI.BeginChangeCheck();
        CRAchievementBaseDefinition newAchievement = (CRAchievementBaseDefinition)EditorGUI.ObjectField(position, label, oldAchivement, typeof(CRAchievementBaseDefinition), false);
        if (EditorGUI.EndChangeCheck())
        {
            achievementAssetProp.stringValue = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(newAchievement)).ToString();
            achievementNameProp.stringValue = newAchievement != null ? newAchievement.name : "";
        }

        EditorGUI.EndProperty();
    }
}