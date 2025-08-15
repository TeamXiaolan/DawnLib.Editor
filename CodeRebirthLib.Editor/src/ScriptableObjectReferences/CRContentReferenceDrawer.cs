using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CodeRebirthLib.CRMod;

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
        var reference = GetReference(property);

        if (reference == null)
        {
            var type = this.fieldInfo.FieldType;
            reference = (CRMContentReference)System.Activator.CreateInstance(type);
            SetReference(property, reference);
        }
        EditorGUI.BeginProperty(position, label, property);

        Object? oldAsset = null;
        if (!string.IsNullOrEmpty(reference.assetGUID))
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

            SetReference(property, reference);
        }

        EditorGUI.EndProperty();
    }

    static CRMContentReference? GetReference(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.ManagedReference)
            return property.managedReferenceValue as CRMContentReference;

        return property.boxedValue as CRMContentReference;
    }

    static void SetReference(SerializedProperty property, CRMContentReference value)
    {
        if (property.propertyType == SerializedPropertyType.ManagedReference)
        {
            property.managedReferenceValue = value;
        }
        else
        {
            property.boxedValue = value;
        }
        property.serializedObject.ApplyModifiedProperties();
        property.serializedObject.Update();
        EditorUtility.SetDirty(property.serializedObject.targetObject);
    }
}