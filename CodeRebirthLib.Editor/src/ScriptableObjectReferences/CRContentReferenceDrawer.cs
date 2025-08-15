using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CodeRebirthLib.CRMod;
using System.Reflection;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;

[CustomPropertyDrawer(typeof(CRMAchievementReference))]
[CustomPropertyDrawer(typeof(CRMEnemyReference))]
[CustomPropertyDrawer(typeof(CRMItemReference))]
[CustomPropertyDrawer(typeof(CRMMapObjectReference))]
[CustomPropertyDrawer(typeof(CRMUnlockableReference))]
[CustomPropertyDrawer(typeof(CRMWeatherReference))]
public class CRMContentReferenceDrawer : PropertyDrawer
{
    // todo: update this if an asset moves
    private static Dictionary<string, string> mappedGuids = new();
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CRMContentReference? reference = (CRMContentReference)property.managedReferenceValue;
        if (reference == null)
        {
            var fieldInfo = property.serializedObject.targetObject.GetType().GetField(property.propertyPath.Split(".")[0], BindingFlags.NonPublic | BindingFlags.Instance);
            var referenceType = fieldInfo.FieldType.GenericTypeArguments[0];
            var constructor = referenceType.GetConstructor([typeof(string)]);
            reference = constructor.Invoke(new object[] { string.Empty }) as CRMContentReference;
            property.managedReferenceValue = reference;
            EditorUtility.SetDirty(property.serializedObject.targetObject);
            property.serializedObject.ApplyModifiedProperties();
			property.serializedObject.Update();
        }
        EditorGUI.BeginProperty(position, label, property);

        Object? oldAsset = null;
        
        if (reference.Key != null)
        {
            string guid = reference.assetGUID;
            if (!mappedGuids.TryGetValue(guid, out string path))
            {
                path = AssetDatabase.GUIDToAssetPath(guid);
                mappedGuids[guid] = path;
            }

            if (!string.IsNullOrEmpty(path))
            {
                oldAsset = AssetDatabase.LoadAssetAtPath<CRMContentDefinition>(path);
            }
        }
        
        EditorGUI.BeginChangeCheck();
        CRMContentDefinition newAsset = (CRMContentDefinition)EditorGUI.ObjectField(position, label, oldAsset, reference.Type, false);
        if (EditorGUI.EndChangeCheck())
        {
            if (newAsset)
            {
                reference.assetGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(newAsset)).ToString();
                reference.Key = newAsset.Key;
            }
            else
            {
                reference.assetGUID = string.Empty;
                reference.Key = null;
            }

            property.managedReferenceValue = reference;
            EditorUtility.SetDirty(property.serializedObject.targetObject);
            property.serializedObject.ApplyModifiedProperties();
			property.serializedObject.Update();
        }

        EditorGUI.EndProperty();
    }
}