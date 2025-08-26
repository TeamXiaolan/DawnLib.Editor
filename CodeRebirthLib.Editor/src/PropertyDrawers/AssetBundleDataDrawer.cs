using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CodeRebirthLib.CRMod;
using CodeRebirthLib.Editor.Extensions;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AssetBundleData), true)]
public class AssetBundleDataDrawer : PropertyDrawer
{
    static readonly string[] EntityLists =
    [
        "weathers", "enemies", "unlockables", "items", "mapObjects", "dungeons"
    ];

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        List<(string path, string message)> invalidPathsWithMessage = new();
        bool makeHeaderRed = false;

        if (property.GetTargetObjectOfProperty() is AssetBundleData assetBundleData)
        {
            makeHeaderRed |= ValidateObjectFieldsAndCollectPaths(assetBundleData, assetBundleData, property, invalidPathsWithMessage);

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string listName in EntityLists)
            {
                SerializedProperty listProp = property.FindPropertyRelative(listName);
                FieldInfo listField = typeof(AssetBundleData).GetField(listName, bindingFlags);
                if (listProp == null || listField == null)
                {
                    continue;
                }

                if (listField.GetValue(assetBundleData) is not IList listObj)
                {
                    continue;
                }

                int count = Mathf.Min(listProp.arraySize, listObj.Count);
                for (int i = 0; i < count; i++)
                {
                    object elem = listObj[i];
                    if (elem == null)
                    {
                        continue;
                    }

                    SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                    makeHeaderRed |= ValidateObjectFieldsAndCollectPaths(elem, assetBundleData, elemProp, invalidPathsWithMessage);
                }
            }
        }

        GUIStyle headerStyle = EditorStyles.foldout;
        string tooltip = string.Empty;
        foreach (var (_, message) in invalidPathsWithMessage)
        {
            makeHeaderRed = true;
            if (string.IsNullOrEmpty(message))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                tooltip += "\n";
            }
            tooltip += $"{message}";
        }
        if (makeHeaderRed)
        {
            headerStyle = new GUIStyle(EditorStyles.foldout);
            TintAllFoldoutStates(headerStyle, Color.red);
        }

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
            label = new GUIContent(label.text, tooltip);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true, headerStyle);
        }

        if (!property.isExpanded)
        {
            return;
        }

        // Children
        Rect childRect = new(position.x, position.y + headerHeight, position.width, 0f);
        using (new EditorGUI.IndentLevelScope(1))
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                childRect.height = height;

                bool isInvalidHere = IsPathOrAncestorInvalid(iterator.propertyPath, invalidPathsWithMessage);

                if (isInvalidHere)
                {
                    using (new LabelAndGuiTintScope(Color.red))
                    {
                        EditorGUI.PropertyField(childRect, iterator, true);
                    }
                }
                else
                {
                    EditorGUI.PropertyField(childRect, iterator, true);
                }

                childRect.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    static bool IsPathOrAncestorInvalid(string path, List<(string path, string message)> invalidPathsWithMessage)
    {
        foreach (var invalidPathWithMessage in invalidPathsWithMessage)
        {
            if (path == invalidPathWithMessage.path)
            {
                return true;
            }
        }

        // Check ancestors: "a.b.color" => "a.b", "a"
        int dot = path.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = path[..dot];
            foreach (var invalidPathWithMessage in invalidPathsWithMessage)
            {
                if (parent == invalidPathWithMessage.path)
                {
                    return true;
                }
            }

            dot = parent.LastIndexOf('.');
        }
        return false;
    }

    static bool ValidateObjectFieldsAndCollectPaths(object obj, AssetBundleData assetBundleData, SerializedProperty objProp, List<(string path, string message)> invalidPathsWithMessage)
    {
        bool anyInvalid = false;
        foreach (FieldInfo fieldInfo in GetAllInstanceFields(obj.GetType()))
        {
            if (!IsFieldSerialized(fieldInfo))
            {
                continue;
            }

            object value = fieldInfo.GetValue(obj);

            // [AssertNotEmpty]
            if (fieldInfo.GetCustomAttribute<AssertNotEmpty>() != null)
            {
                bool empty = string.IsNullOrWhiteSpace(value?.ToString());
                if (empty)
                {
                    anyInvalid = true;
                    TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"{fieldInfo.Name} is empty");
                }
            }

            // [AssertFieldNotNull]
            if (fieldInfo.GetCustomAttribute<AssertFieldNotNull>() != null)
            {
                bool invalid = value == null || (value is CRMContentReference cr && string.IsNullOrEmpty(cr.assetGUID));

                if (invalid)
                {
                    anyInvalid = true;

                    SerializedProperty parentProp = objProp.FindPropertyRelative(fieldInfo.Name);
                    if (parentProp != null)
                    {
                        invalidPathsWithMessage.Add((parentProp.propertyPath, $"{fieldInfo.Name} is null"));

                        SerializedProperty guidProp = parentProp.FindPropertyRelative("assetGUID");
                        if (guidProp != null)
                        {
                            if (value is CRMEnemyReference enemyReference)
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "EnemyReference Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                            else if (value is CRMItemReference itemReference)
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "ItemReference Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                            else if (value is CRMMapObjectReference mapObjectReference)
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "MapObject Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                            else if (value is CRMUnlockableReference unlockableReference)
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "Unlockable Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                            else if (value is CRMWeatherReference weatherReference)
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "Weather Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                            else if (value is CRMAdditionalTilesReference additionalTilesReference)
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "AdditionalTiles Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                            else
                            {
                                invalidPathsWithMessage.Add((guidProp.propertyPath, "Unknown Reference is empty"));
                                invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                            }
                        }
                    }
                    else
                    {
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"{fieldInfo.Name} is null");
                    }
                }
                else if (value is CRMContentReference contentReference)
                {
                    string guid = contentReference.assetGUID;
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"Path to {fieldInfo.Name} is invalid??");
                        continue;
                    }

                    bool WRExists = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "WeatherRegistry");
                    CRMContentDefinition? loadedAsset = null;
                    if (value is CRMEnemyReference)
                    {
                        loadedAsset = AssetDatabase.LoadAssetAtPath<CRMEnemyDefinition>(path);
                        CRMEnemyDefinition def = (CRMEnemyDefinition)loadedAsset;
                        if (def.EnemyType == null)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"EnemyType on {def.name} does not exist.");
                            continue;
                        }

                        if (def.EnemyType.enemyPrefab == null)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"EnemyPrefab on {def.EnemyType.name} does not exist.");
                            continue;
                        }
                        // other checks
                    }
                    else if (value is CRMItemReference)
                    {
                        loadedAsset = AssetDatabase.LoadAssetAtPath<CRMItemDefinition>(path);
                        CRMItemDefinition def = (CRMItemDefinition)loadedAsset;
                        if (def.Item == null)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"Item on {def.name} does not exist.");
                            continue;
                        }

                        if (def.Item.spawnPrefab == null)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"SpawnPrefab on {def.Item.name} does not exist.");
                            continue;
                        }
                        // other checks
                    }
                    else if (value is CRMMapObjectReference)
                    {
                        loadedAsset = AssetDatabase.LoadAssetAtPath<CRMMapObjectDefinition>(path);
                        CRMMapObjectDefinition def = (CRMMapObjectDefinition)loadedAsset;
                        if (def.GameObject == null)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"GameObject on {def.name} does not exist.");
                            continue;
                        }
                        // other checks
                    }
                    else if (value is CRMUnlockableReference)
                    {
                        loadedAsset = AssetDatabase.LoadAssetAtPath<CRMUnlockableDefinition>(path);
                        CRMUnlockableDefinition def = (CRMUnlockableDefinition)loadedAsset;
                        if (string.IsNullOrEmpty(def.UnlockableItem.unlockableName))
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"Unlockable's name on {def.name} is empty or does not exist.");
                            continue;
                        }
                        // other checks
                    }
                    else if (value is CRMAdditionalTilesReference)
                    {
                        loadedAsset = AssetDatabase.LoadAssetAtPath<CRMAdditionalTilesDefinition>(path);
                        CRMAdditionalTilesDefinition def = (CRMAdditionalTilesDefinition)loadedAsset;
                        if (def.TilesToAdd == null)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"TilesToAdd on {def.name} does not exist.");
                            continue;
                        }
                        // other checks
                    }
                    else if (WRExists)
                    {
                        loadedAsset = AssetDatabase.LoadAssetAtPath<CRMContentDefinition>(path);
                        CRMContentDefinition def = (CRMContentDefinition)loadedAsset;
                        anyInvalid = WeatherRegistryChecks(def, out string message);
                        if (anyInvalid)
                        {
                            anyInvalid = true;
                            TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, message);
                            continue;
                        }
                        // other checks
                    }

                    if (loadedAsset == null)
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"CRMContentDefinition on the path: {path} does not exist somehow???");
                        continue;
                    }

                    string assetBundleName = AssetDatabase.GetImplicitAssetBundleName(path);
                    if (string.IsNullOrEmpty(assetBundleName))
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"{loadedAsset.name} on the path: {path} is not assigned any AssetBundle.");
                        continue;
                    }

                    if (assetBundleName != assetBundleData.assetBundleName)
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"{loadedAsset.name} on the path: {path} is assigned to the incorrect AssetBundle, it should be assigned to: {assetBundleData.assetBundleName}.");
                        continue;
                    }

                    string assetBundleExtension = AssetDatabase.GetImplicitAssetBundleVariantName(path);
                    if (!string.IsNullOrEmpty(assetBundleExtension))
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, $"{loadedAsset.name} on the path: {path} is assigned to an assetbundle with an extension, it should not have an extension.");
                        continue;
                    }
                }
            }
        }
        return anyInvalid;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    static bool WeatherRegistryChecks(CRMContentDefinition contentDefinition, out string message)
    {
        message = string.Empty;
        if (contentDefinition is CRMWeatherDefinition weatherDefinition)
        {
            if (weatherDefinition.Weather == null)
            {
                message = "Weather on " + weatherDefinition.name + " does not exist.";
                return true;
            }
        }
        return false;
    }

    static void TryAddRelativePath(SerializedProperty parent, string relativeName, List<(string path, string message)> pathsWithMessage, string message)
    {
        if (parent == null)
        {
            return;
        }

        SerializedProperty child = parent.FindPropertyRelative(relativeName);
        if (child != null)
        {
            pathsWithMessage.Add((child.propertyPath, message));
        }
    }

    static bool IsFieldSerialized(FieldInfo fieldInfo)
    {
        if (fieldInfo.IsDefined(typeof(NonSerializedAttribute), inherit: true))
            return false;

        if (fieldInfo.IsPublic)
            return true;

        if (fieldInfo.IsDefined(typeof(SerializeField), inherit: true))
            return true;

        if (fieldInfo.IsDefined(typeof(SerializeReference), inherit: true))
            return true;

        return false;
    }


    static IEnumerable<FieldInfo> GetAllInstanceFields(Type t)
    {
        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type cur = t; cur != null; cur = cur.BaseType)
        {
            foreach (FieldInfo fieldInfo in cur.GetFields(bindingFlags))
            {
                yield return fieldInfo;
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    static void TintAllFoldoutStates(GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        style.onNormal.textColor = color;
        style.focused.textColor = color;
        style.onFocused.textColor = color;
        style.active.textColor = color;
        style.onActive.textColor = color;
        style.hover.textColor = color;
        style.onHover.textColor = color;
    }

    sealed class LabelAndGuiTintScope : IDisposable
    {
        readonly Color _lN,_lON,_lF,_lOF,_lA,_lOA,_lH,_lOH;
        readonly Color _guiColor, _contentColor;

        public LabelAndGuiTintScope(Color tint)
        {
            GUIStyle style = EditorStyles.label;
            _lN  = style.normal.textColor;
            _lON = style.onNormal.textColor;
            _lF  = style.focused.textColor;
            _lOF = style.onFocused.textColor;
            _lA  = style.active.textColor;
            _lOA = style.onActive.textColor;
            _lH  = style.hover.textColor;
            _lOH = style.onHover.textColor;

            style.normal.textColor    = tint;
            style.onNormal.textColor  = tint;
            style.focused.textColor   = tint;
            style.onFocused.textColor = tint;
            style.active.textColor    = tint;
            style.onActive.textColor  = tint;
            style.hover.textColor     = tint;
            style.onHover.textColor   = tint;

            _guiColor = GUI.color;
            _contentColor = GUI.contentColor;
            GUI.color = tint;
            GUI.contentColor = tint;
        }

        public void Dispose()
        {
            GUIStyle style = EditorStyles.label;
            style.normal.textColor    = _lN;
            style.onNormal.textColor  = _lON;
            style.focused.textColor   = _lF;
            style.onFocused.textColor = _lOF;
            style.active.textColor    = _lA;
            style.onActive.textColor  = _lOA;
            style.hover.textColor     = _lH;
            style.onHover.textColor   = _lOH;

            GUI.color = _guiColor;
            GUI.contentColor = _contentColor;
        }
    }
}
