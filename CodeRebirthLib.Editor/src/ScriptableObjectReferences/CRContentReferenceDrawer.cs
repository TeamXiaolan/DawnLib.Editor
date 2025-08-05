using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CodeRebirthLib.ContentManagement;
using CodeRebirthLib.ContentManagement.Achievements;
using System.Reflection;
using CodeRebirthLib.ContentManagement.Items;
using CodeRebirthLib.ContentManagement.Enemies;
using CodeRebirthLib.ContentManagement.MapObjects;
using CodeRebirthLib.ContentManagement.Unlockables;
using CodeRebirthLib.ContentManagement.Weathers;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;

[CustomPropertyDrawer(typeof(CRAchievementReference))]
[CustomPropertyDrawer(typeof(CREnemyReference))]
[CustomPropertyDrawer(typeof(CRItemReference))]
[CustomPropertyDrawer(typeof(CRMapObjectReference))]
[CustomPropertyDrawer(typeof(CRUnlockableReference))]
[CustomPropertyDrawer(typeof(CRWeatherReference))]
public class CRContentReferenceDrawer : PropertyDrawer
{
    // todo: update this if an asset moves
    private static Dictionary<string, string> mappedGuids = new();
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CRContentReference? reference = (CRContentReference)property.managedReferenceValue;
        if (reference == null)
        {
            var fieldInfo = property.serializedObject.targetObject.GetType().GetField(property.propertyPath.Split(".")[0], BindingFlags.NonPublic | BindingFlags.Instance);
            var referenceType = fieldInfo.FieldType.GenericTypeArguments[0];
            var constructor = referenceType.GetConstructor([typeof(string)]);
            reference = constructor.Invoke(new object[] { string.Empty }) as CRContentReference;
            property.managedReferenceValue = reference;
        }
        EditorGUI.BeginProperty(position, label, property);

        Object? oldAsset = null;
        if (reference.assetGUID != null)
        {
            string guid = reference.assetGUID;
            if (!mappedGuids.TryGetValue(guid, out string path))
            {
                path = AssetDatabase.GUIDToAssetPath(guid);
                mappedGuids[guid] = path;
            }

            if (!string.IsNullOrEmpty(path))
            {
                oldAsset = AssetDatabase.LoadAssetAtPath<CRContentDefinition>(path);
            }
        }
        
        EditorGUI.BeginChangeCheck();
        CRContentDefinition newAsset = (CRContentDefinition)EditorGUI.ObjectField(position, label, oldAsset, reference.ContentType, false);
        if (EditorGUI.EndChangeCheck())
        {
            if (newAsset) 
            {
                string newName = reference.GetEntityName(newAsset);
                reference.assetGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(newAsset)).ToString();
                reference.entityName = newName;
            } 
            else 
            {
                reference.assetGUID = string.Empty;
                reference.entityName = string.Empty;
            }
            
        }

        EditorGUI.EndProperty();
    }
}