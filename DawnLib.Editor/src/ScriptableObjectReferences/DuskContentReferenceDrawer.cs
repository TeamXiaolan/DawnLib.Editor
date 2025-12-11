using System;
using System.Collections.Generic;
using Dawn.Editor.Extensions;
using Dusk;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.ScriptableObjectReferences;

[CustomPropertyDrawer(typeof(DuskEnemyReference))]
[CustomPropertyDrawer(typeof(DuskItemReference))]
[CustomPropertyDrawer(typeof(DuskMapObjectReference))]
[CustomPropertyDrawer(typeof(DuskUnlockableReference))]
[CustomPropertyDrawer(typeof(DuskWeatherReference))]
[CustomPropertyDrawer(typeof(DuskAchievementReference))]
[CustomPropertyDrawer(typeof(DuskAdditionalTilesReference), true)]
[CustomPropertyDrawer(typeof(DuskVehicleReference))]
[CustomPropertyDrawer(typeof(DuskDungeonReference))]
[CustomPropertyDrawer(typeof(DuskMoonReference))]
[CustomPropertyDrawer(typeof(DuskEntityReplacementDefinition), true)]
[CustomPropertyDrawer(typeof(DuskStoryLogReference))]
public class DuskContentReferenceDrawer : PropertyDrawer
{
    // todo: update this if an asset moves
    private static Dictionary<string, string> mappedGuids = new();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.managedReferenceValue is not DuskContentReference reference)
        {
            Type referenceType = fieldInfo.FieldType;
            if (referenceType.IsGenericType && referenceType.GetGenericTypeDefinition() == typeof(List<>))
            {
                referenceType = referenceType.GenericTypeArguments[0];
            }

            var constructor = referenceType.GetConstructor([]);
            reference = (DuskContentReference)constructor.Invoke([]);
            property.SetManagedReference(reference, "Create Empty Reference");
        }
        EditorGUI.BeginProperty(position, label, property);

        UnityEngine.Object? oldAsset = null;
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
                oldAsset = AssetDatabase.LoadAssetAtPath<DuskContentDefinition>(path);
            }
        }

        EditorGUI.BeginChangeCheck();
        DuskContentDefinition newAsset = (DuskContentDefinition)EditorGUI.ObjectField(position, label, oldAsset, reference.DefinitionType, false);
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