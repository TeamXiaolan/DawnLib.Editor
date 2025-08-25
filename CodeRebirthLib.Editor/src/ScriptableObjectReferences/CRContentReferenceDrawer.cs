using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CodeRebirthLib.CRMod;
using CodeRebirthLib.Editor.Extensions;

namespace CodeRebirthLib.Editor.ScriptableObjectReferences;

[CustomPropertyDrawer(typeof(CRMEnemyReference))]
[CustomPropertyDrawer(typeof(CRMItemReference))]
[CustomPropertyDrawer(typeof(CRMMapObjectReference))]
[CustomPropertyDrawer(typeof(CRMUnlockableReference))]
[CustomPropertyDrawer(typeof(CRMWeatherReference))]
[CustomPropertyDrawer(typeof(CRMAchievementReference))]
[CustomPropertyDrawer(typeof(CRMAdditionalTilesReference), true)]
public class CRMContentReferenceDrawer : PropertyDrawer
{
    // todo: update this if an asset moves
    private static Dictionary<string, string> mappedGuids = new();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.managedReferenceValue is not CRMContentReference reference)
        {
            var referenceType = fieldInfo.FieldType;
            if (referenceType.IsGenericType && referenceType.GetGenericTypeDefinition() == typeof(List<>))
            {
                referenceType = referenceType.GenericTypeArguments[0];
            }

            var constructor = referenceType.GetConstructor([]);
            reference = (CRMContentReference)constructor.Invoke([]);
            property.SetManagedReference(reference, "Create Empty Reference");
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
        CRMContentDefinition newAsset = (CRMContentDefinition)EditorGUI.ObjectField(position, label, oldAsset, reference.DefinitionType, false);
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

            property.SetManagedReference(reference, "Set New Reference");
        }

        EditorGUI.EndProperty();
    }
}