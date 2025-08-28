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

    static readonly bool WRExists = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "WeatherRegistry");

    static class AssetValidationCache
    {
        static readonly ConditionalWeakTable<UnityEngine.Object, PerObjectCache> _perObject = new();

        static readonly Dictionary<string, string> _guidToPath = new(256);
        static readonly Dictionary<string, (string bundle, string variant)> _pathToBundle = new(256);

        static readonly Dictionary<string, UnityEngine.Object> _loadedByPathAndType = new(512);
        static readonly Dictionary<Type, FieldInfo[]> _serializedFieldsByType = new(64);
        static readonly Dictionary<FieldInfo, bool> _isFieldSerializedMemo = new(256);

        static GUIStyle? _redFoldoutStyle = null;

        public static PerObjectCache Get(UnityEngine.Object target)
        {
            if (!_perObject.TryGetValue(target, out PerObjectCache cache))
            {
                cache = new PerObjectCache();
                _perObject.Add(target, cache);
            }
            return cache;
        }

        public static bool TryGetPathForGuid(string guid, out string? path)
        {
            if (string.IsNullOrEmpty(guid))
            {
                path = null;
                return false;
            }

            if (_guidToPath.TryGetValue(guid, out path))
            {
                return !string.IsNullOrEmpty(path);
            }

            path = AssetDatabase.GUIDToAssetPath(guid);
            _guidToPath[guid] = path ?? string.Empty;
            return !string.IsNullOrEmpty(path);
        }

        public static (string? bundle, string? variant) GetBundleNames(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return (null, null);
            }

            if (_pathToBundle.TryGetValue(path, out (string bundle, string variant) bundleNameWithVariant))
            {
                return bundleNameWithVariant;
            }
            string bundleName = AssetDatabase.GetImplicitAssetBundleName(path);
            string variant = AssetDatabase.GetImplicitAssetBundleVariantName(path);
            bundleNameWithVariant = (bundleName, variant);
            _pathToBundle[path] = bundleNameWithVariant;
            return bundleNameWithVariant;
        }

        public static T? LoadAtPathCached<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string key = path + "\n" + typeof(T).AssemblyQualifiedName;
            if (_loadedByPathAndType.TryGetValue(key, out var obj))
            {
                return obj as T;
            }

            T loaded = AssetDatabase.LoadAssetAtPath<T>(path);
            _loadedByPathAndType[key] = loaded;
            return loaded;
        }

        public static FieldInfo[] GetSerializedFieldsCached(Type type)
        {
            if (_serializedFieldsByType.TryGetValue(type, out FieldInfo[] arr))
            {
                return arr;
            }

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            List<FieldInfo> list = new();

            for (Type cur = type; cur != null; cur = cur.BaseType)
            {
                foreach (var fieldInfo in cur.GetFields(bindingFlags))
                {
                    if (IsFieldSerializedCached(fieldInfo))
                    {
                        list.Add(fieldInfo);
                    }
                }
            }
            arr = list.ToArray();
            _serializedFieldsByType[type] = arr;
            return arr;
        }

        public static bool IsFieldSerializedCached(FieldInfo fieldInfo)
        {
            if (_isFieldSerializedMemo.TryGetValue(fieldInfo, out var b))
            {
                return b;
            }

            bool res = !fieldInfo.IsDefined(typeof(NonSerializedAttribute), true) && (fieldInfo.IsPublic || fieldInfo.IsDefined(typeof(SerializeField), true) || fieldInfo.IsDefined(typeof(SerializeReference), true));

            _isFieldSerializedMemo[fieldInfo] = res;
            return res;
        }

        public static GUIStyle GetRedFoldoutStyle()
        {
            if (_redFoldoutStyle != null)
            {
                return _redFoldoutStyle;
            }

            _redFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            TintAllFoldoutStates(_redFoldoutStyle, Color.red);
            return _redFoldoutStyle;
        }

        [InitializeOnLoadMethod]
        static void Init()
        {
            AssemblyReloadEvents.afterAssemblyReload += ClearAll;
            EditorApplication.projectChanged += ClearAll;
        }

        static void ClearAll()
        {
            _perObject.Clear();
            _guidToPath.Clear();
            _pathToBundle.Clear();
            _loadedByPathAndType.Clear();
            _serializedFieldsByType.Clear();
            _isFieldSerializedMemo.Clear();
            _redFoldoutStyle = null;
        }

        public sealed class PerObjectCache
        {
            public readonly Dictionary<string, Entry> Entries = new(256);
        }

        public sealed class Entry
        {
            public string LastGuid = string.Empty;
            public string? LastBundle = string.Empty;
            public string? LastVariant = string.Empty;
            public bool LastInvalid = false;
            public string LastMessage = string.Empty;

            public double NextAllowedDeepCheckTime;
        }

        public static void TintAllFoldoutStates(GUIStyle style, Color color)
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
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        List<(string path, string message)> invalidPathsWithMessage = new();
        bool makeHeaderRed = false;

        if (property.GetTargetObjectOfProperty() is AssetBundleData assetBundleData)
        {
            makeHeaderRed |= ValidateObjectFieldsAndCollectPaths_Cached(assetBundleData, assetBundleData, property, invalidPathsWithMessage);

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string listName in EntityLists)
            {
                SerializedProperty listProp = property.FindPropertyRelative(listName);
                FieldInfo listField = typeof(AssetBundleData).GetField(listName, bindingFlags);
                if (listProp == null || listField == null)
                    continue;

                if (listField.GetValue(assetBundleData) is not IList listObj)
                    continue;

                int count = Mathf.Min(listProp.arraySize, listObj.Count);
                for (int i = 0; i < count; i++)
                {
                    object elem = listObj[i];
                    if (elem == null)
                        continue;

                    SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                    makeHeaderRed |= ValidateObjectFieldsAndCollectPaths_Cached(elem, assetBundleData, elemProp, invalidPathsWithMessage);
                }
            }
        }

        // Tooltip
        string tooltip = string.Empty;
        foreach ((string _, string message) in invalidPathsWithMessage)
        {
            makeHeaderRed = true;
            if (string.IsNullOrEmpty(message))
                continue;

            if (!string.IsNullOrEmpty(tooltip))
                tooltip += "\n";

            tooltip += message;
        }

        GUIStyle headerStyle = makeHeaderRed ? AssetValidationCache.GetRedFoldoutStyle() : EditorStyles.foldout;

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
            label = new GUIContent(label.text, tooltip);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true, headerStyle);
        }

        if (!property.isExpanded)
            return;

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
                    Rect border = new Rect(childRect.x, childRect.y, 2f, height);
                    EditorGUI.DrawRect(border, Color.red);
                }

                EditorGUI.PropertyField(childRect, iterator, true);
                childRect.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    static bool IsPathOrAncestorInvalid(string path, List<(string path, string message)> invalidPathsWithMessage)
    {
        foreach (var tuple in invalidPathsWithMessage)
        {
            if (path == tuple.path)
            {
                return true;
            }
        }

        int dot = path.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = path[..dot];
            foreach (var tuple in invalidPathsWithMessage)
            {
                if (parent == tuple.path)
                {
                    return true;
                }
            }

            dot = parent.LastIndexOf('.');
        }
        return false;
    }

    static bool ValidateObjectFieldsAndCollectPaths_Cached(object obj, AssetBundleData assetBundleData, SerializedProperty? objProp, List<(string path, string message)> invalidPathsWithMessage)
    {
        bool anyInvalid = false;
        if (objProp == null)
        {
            return false;
        }

        UnityEngine.Object target = objProp.serializedObject.targetObject;
        AssetValidationCache.PerObjectCache cache = AssetValidationCache.Get(target);

        foreach (FieldInfo fieldInfo in AssetValidationCache.GetSerializedFieldsCached(obj.GetType()))
        {
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
                    MarkNullReference(objProp, fieldInfo, value, invalidPathsWithMessage);
                    continue;
                }

                if (value is CRMContentReference contentReference)
                {
                    string guid = contentReference.assetGUID;

                    SerializedProperty parentProp = objProp.FindPropertyRelative(fieldInfo.Name);
                    string propPath = parentProp != null ? parentProp.propertyPath : $"{objProp.propertyPath}.{fieldInfo.Name}";

                    if (!cache.Entries.TryGetValue(propPath, out var entry))
                    {
                        entry = new AssetValidationCache.Entry();
                        cache.Entries[propPath] = entry;
                    }

                    if (!AssetValidationCache.TryGetPathForGuid(guid, out var path) || string.IsNullOrEmpty(path))
                    {
                        if (entry.LastGuid != guid)
                        {
                            entry.LastGuid = guid;
                            entry.LastInvalid = true;
                            entry.LastMessage = $"Path to {fieldInfo.Name} is invalid (empty GUID or unresolved).";
                        }
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, entry.LastMessage);
                        continue;
                    }

                    (string? bundle, string? variant) = AssetValidationCache.GetBundleNames(path);

                    bool keySame =
                        entry.LastGuid == guid &&
                        entry.LastBundle == bundle &&
                        entry.LastVariant == variant;

                    double now = EditorApplication.timeSinceStartup;
                    bool canReuse = keySame && now < entry.NextAllowedDeepCheckTime;

                    if (!canReuse)
                    {
                        bool invalidHere = false;
                        string message = string.Empty;

                        if (value is CRMEnemyReference)
                        {
                            CRMEnemyDefinition? def = AssetValidationCache.LoadAtPathCached<CRMEnemyDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "CRMEnemyDefinition");
                            }
                            else
                            {
                                if (def.EnemyType == null)
                                {
                                    invalidHere = true;
                                    message = $"EnemyType on {def.name} does not exist.";
                                }
                                else if (def.EnemyType.enemyPrefab == null)
                                {
                                    invalidHere = true;
                                    message = $"EnemyPrefab on {def.EnemyType.name} does not exist.";
                                }
                            }
                        }
                        else if (value is CRMItemReference)
                        {
                            CRMItemDefinition? def = AssetValidationCache.LoadAtPathCached<CRMItemDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "CRMItemDefinition");
                            }
                            else
                            {
                                if (def.Item == null)
                                {
                                    invalidHere = true;
                                    message = $"Item on {def.name} does not exist.";
                                }
                                else if (def.Item.spawnPrefab == null)
                                {
                                    invalidHere = true;
                                    message = $"SpawnPrefab on {def.Item.name} does not exist.";
                                }
                            }
                        }
                        else if (value is CRMMapObjectReference)
                        {
                            CRMMapObjectDefinition? def = AssetValidationCache.LoadAtPathCached<CRMMapObjectDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "CRMMapObjectDefinition");
                            }
                            else if (def.GameObject == null)
                            {
                                invalidHere = true;
                                message = $"GameObject on {def.name} does not exist.";
                            }
                        }
                        else if (value is CRMUnlockableReference)
                        {
                            CRMUnlockableDefinition? def = AssetValidationCache.LoadAtPathCached<CRMUnlockableDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "CRMUnlockableDefinition");
                            }
                            else if (string.IsNullOrEmpty(def.UnlockableItem.unlockableName))
                            {
                                invalidHere = true;
                                message = $"Unlockable's name on {def.name} is empty or does not exist.";
                            }
                        }
                        else if (value is CRMAdditionalTilesReference)
                        {
                            CRMAdditionalTilesDefinition? def = AssetValidationCache.LoadAtPathCached<CRMAdditionalTilesDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "CRMAdditionalTilesDefinition");
                            }
                            else if (def.TilesToAdd == null)
                            {
                                invalidHere = true;
                                message = $"TilesToAdd on {def.name} does not exist.";
                            }
                        }
                        else if (WRExists)
                        {
                            CRMContentDefinition? def = AssetValidationCache.LoadAtPathCached<CRMContentDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "CRMContentDefinition");
                            }
                            else if (WeatherRegistryChecks(def, out string wrMsg))
                            {
                                invalidHere = true;
                                message = wrMsg;
                            }
                        }

                        if (!invalidHere)
                        {
                            if (string.IsNullOrEmpty(bundle))
                            {
                                invalidHere = true;
                                message = $"{System.IO.Path.GetFileNameWithoutExtension(path)} on the path: {path} is not assigned any AssetBundle.";
                            }
                            else if (bundle != assetBundleData.assetBundleName)
                            {
                                invalidHere = true;
                                message = $"{System.IO.Path.GetFileNameWithoutExtension(path)} on the path: {path} is assigned to the incorrect AssetBundle, it should be assigned to: {assetBundleData.assetBundleName}.";
                            }
                            else if (!string.IsNullOrEmpty(variant))
                            {
                                invalidHere = true;
                                message = $"{System.IO.Path.GetFileNameWithoutExtension(path)} on the path: {path} is assigned to an assetbundle with an extension, it should not have an extension.";
                            }
                        }

                        entry.LastGuid = guid;
                        entry.LastBundle = bundle;
                        entry.LastVariant = variant;
                        entry.LastInvalid = invalidHere;
                        entry.LastMessage = invalidHere ? message : string.Empty;
                        entry.NextAllowedDeepCheckTime = EditorApplication.timeSinceStartup + 1;
                    }

                    if (entry.LastInvalid)
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPathsWithMessage, entry.LastMessage);
                    }
                }
            }
        }
        return anyInvalid;
    }

    static string MissingDef(string path, string typeName) => $"{typeName} on the path: {path} does not exist.";

    static void MarkNullReference(SerializedProperty objProp, FieldInfo fieldInfo, object? value, List<(string path, string message)> invalidPathsWithMessage)
    {
        SerializedProperty parentProp = objProp.FindPropertyRelative(fieldInfo.Name);
        if (parentProp != null)
        {
            invalidPathsWithMessage.Add((parentProp.propertyPath, $"{fieldInfo.Name} is null"));

            SerializedProperty guidProp = parentProp.FindPropertyRelative("assetGUID");
            if (guidProp != null)
            {
                if (value is CRMEnemyReference)
                {
                    invalidPathsWithMessage.Add((guidProp.propertyPath, "EnemyReference Reference is empty"));
                    invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                }
                else if (value is CRMItemReference)
                {
                    invalidPathsWithMessage.Add((guidProp.propertyPath, "ItemReference Reference is empty"));
                    invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                }
                else if (value is CRMMapObjectReference)
                {
                    invalidPathsWithMessage.Add((guidProp.propertyPath, "MapObject Reference is empty"));
                    invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                }
                else if (value is CRMUnlockableReference)
                {
                    invalidPathsWithMessage.Add((guidProp.propertyPath, "Unlockable Reference is empty"));
                    invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                }
                else if (value is CRMWeatherReference)
                {
                    invalidPathsWithMessage.Add((guidProp.propertyPath, "Weather Reference is empty"));
                    invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                }
                else if (value is CRMAdditionalTilesReference)
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

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);
}