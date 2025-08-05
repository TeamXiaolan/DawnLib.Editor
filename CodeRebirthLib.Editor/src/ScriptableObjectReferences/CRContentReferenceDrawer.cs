using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CodeRebirthLib.ContentManagement;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;
[CustomPropertyDrawer(typeof(CRContentReference))]
public class CRContentReferenceDrawer : PropertyDrawer {
    static Dictionary<string, string> mappedGuids = new Dictionary<string, string>();
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        CRContentReference reference = (CRContentReference)property.managedReferenceValue;
        SerializedProperty guidProp = property.FindPropertyRelative("_assetGUID");
        SerializedProperty nameProp = property.FindPropertyRelative("entityName");
        
        EditorGUI.BeginProperty(position, label, property);

        Object oldAsset = null;
        if(guidProp.stringValue != null) {
            string guid = guidProp.stringValue;
            if(!mappedGuids.TryGetValue(guid, out string path)) {
                path = AssetDatabase.GUIDToAssetPath(guid);
                mappedGuids[guid] = path;
            }

            if(!string.IsNullOrEmpty(path)) {
                oldAsset = AssetDatabase.LoadAssetAtPath<CRContentDefinition>(path);
            }
        }
        
        EditorGUI.BeginChangeCheck();
        CRContentDefinition newAsset = (CRContentDefinition)EditorGUI.ObjectField(position, label, oldAsset, reference.ContentType, false);
        if (EditorGUI.EndChangeCheck()) {
            string newName = reference.GetEntityName(newAsset);
            guidProp.stringValue = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(newAsset)).ToString();
            nameProp.stringValue = newName;
        }

        EditorGUI.EndProperty();
    }
}