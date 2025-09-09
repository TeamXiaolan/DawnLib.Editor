using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dawn.Editor.Extensions;
using Dusk;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(AssetBundleData), true)]
public class AssetBundleDataDrawer : PropertyDrawer
{
    static class ValidationSettings
    {
        const string Key_AllOff = "Dawn.AssetBundleDataDrawer.DisableAllValidation";
        const string Key_HeavyOff = "Dawn.AssetBundleDataDrawer.DisableHeavyValidation";

        static bool? _allOffCache;
        static bool? _heavyOffCache;

        public static bool DisableAll
        {
            get
            {
                _allOffCache ??= EditorPrefs.GetBool(Key_AllOff, false);
                return _allOffCache.Value;
            }
            set
            {
                _allOffCache = value;
                EditorPrefs.SetBool(Key_AllOff, value);
            }
        }

        public static bool DisableHeavy
        {
            get
            {
                _heavyOffCache ??= EditorPrefs.GetBool(Key_HeavyOff, false);
                return _heavyOffCache.Value;
            }
            set
            {
                _heavyOffCache = value;
                EditorPrefs.SetBool(Key_HeavyOff, value);
            }
        }
    }

    static readonly string[] EntityLists =
    [
        "weathers", "enemies", "unlockables", "items", "mapObjects", "dungeons", "vehicles"
    ];

    static readonly bool WRExists = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "WeatherRegistry");

    enum ReferenceKind
    {
        None,
        Enemy,
        Item,
        MapObject,
        Unlockable,
        AdditionalTile,
        Vehicle,
        OtherContent
    }

    sealed class FieldEntry
    {
        public string FieldName = string.Empty;
        public bool HasAssertNotEmpty;
        public bool HasAssertFieldNotNull;
        public ReferenceKind ReferenceKind;
        public bool IsStringField;
    }

    sealed class TypeChecksDescriptor
    {
        public readonly List<FieldEntry> Entries = new List<FieldEntry>(32);
    }

    static class AssetValidationCache
    {
        static readonly ConditionalWeakTable<UnityEngine.Object, PerObjectCache> _perObject = new();

        static readonly Dictionary<string, string> _guidToPath = new(256);
        static readonly Dictionary<string, (string bundle, string variant)> _pathToBundle = new(256);
        static readonly Dictionary<string, UnityEngine.Object> _loadedByPathAndType = new(512);
        static readonly Dictionary<Type, TypeChecksDescriptor> _typeDescriptors = new(32);
        static readonly Dictionary<string, Type> _listElementTypesByName = new(8);

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

        public static TypeChecksDescriptor GetDescriptorForType(Type type)
        {
            if (_typeDescriptors.TryGetValue(type, out TypeChecksDescriptor typeChecksDescriptor))
            {
                return typeChecksDescriptor;
            }

            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            List<FieldEntry> list = new(32);

            for (Type cur = type; cur != null; cur = cur.BaseType)
            {
                foreach (FieldInfo fieldInfo in cur.GetFields(bindingFlags))
                {
                    if (fieldInfo.IsDefined(typeof(NonSerializedAttribute), true))
                        continue;

                    bool isSerialized = fieldInfo.IsPublic || fieldInfo.IsDefined(typeof(SerializeField), true) || fieldInfo.IsDefined(typeof(SerializeReference), true);
                    if (!isSerialized)
                        continue;

                    FieldEntry entry = new();
                    entry.FieldName = fieldInfo.Name;
                    entry.HasAssertNotEmpty = fieldInfo.GetCustomAttribute<AssertNotEmpty>() != null;
                    entry.HasAssertFieldNotNull = fieldInfo.GetCustomAttribute<AssertFieldNotNull>() != null;
                    entry.IsStringField = fieldInfo.FieldType == typeof(string);

                    if (typeof(DuskEnemyReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.Enemy;
                    }
                    else if (typeof(DuskItemReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.Item;
                    }
                    else if (typeof(DuskMapObjectReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.MapObject;
                    }
                    else if (typeof(DuskUnlockableReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.Unlockable;
                    }
                    else if (typeof(DuskAdditionalTilesReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.AdditionalTile;
                    }
                    else if (typeof(DuskVehicleReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.Vehicle;
                    }
                    else if (typeof(DuskContentReference).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        entry.ReferenceKind = ReferenceKind.OtherContent;
                    }
                    else
                    {
                        entry.ReferenceKind = ReferenceKind.None;
                    }
                    list.Add(entry);
                }
            }

            typeChecksDescriptor = new TypeChecksDescriptor();
            typeChecksDescriptor.Entries.AddRange(list);
            _typeDescriptors[type] = typeChecksDescriptor;
            return typeChecksDescriptor;
        }

        public static Type GetListElementTypeByName(Type ownerType, string listFieldName)
        {
            if (_listElementTypesByName.TryGetValue(listFieldName, out Type type))
            {
                return type;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo fieldInfo = ownerType.GetField(listFieldName, flags);
            Type elementType = typeof(object);

            if (fieldInfo != null)
            {
                Type fieldType = fieldInfo.FieldType;
                if (fieldType.IsArray)
                {
                    elementType = fieldType.GetElementType();
                }
                else if (fieldType.IsGenericType)
                {
                    elementType = fieldType.GetGenericArguments()[0];
                }
            }

            _listElementTypesByName[listFieldName] = elementType ?? typeof(object);
            return _listElementTypesByName[listFieldName];
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
            _typeDescriptors.Clear();
            _listElementTypesByName.Clear();
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

        bool stopAll = ValidationSettings.DisableAll;
        bool stopHeavy = ValidationSettings.DisableHeavy;

        if (!stopAll && property.GetTargetObjectOfProperty() is AssetBundleData assetBundleData)
        {
            makeHeaderRed |= ValidateSerializedObject(property, assetBundleData, invalidPathsWithMessage);

            foreach (string listName in EntityLists)
            {
                SerializedProperty listProp = property.FindPropertyRelative(listName);
                if (listProp == null || !listProp.isArray)
                    continue;

                Type elemType = AssetValidationCache.GetListElementTypeByName(typeof(AssetBundleData), listName);
                TypeChecksDescriptor elemDesc = AssetValidationCache.GetDescriptorForType(elemType);

                int count = listProp.arraySize;
                for (int i = 0; i < count; i++)
                {
                    SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                    if (elemProp == null)
                        continue;

                    makeHeaderRed |= ValidateSerializedWithDescriptor(elemProp, assetBundleData, elemDesc, invalidPathsWithMessage);
                }
            }
        }

        string tooltip = string.Empty;
        if (!stopAll)
        {
            foreach ((string _, string message) in invalidPathsWithMessage)
            {
                makeHeaderRed = true;
                if (string.IsNullOrEmpty(message))
                    continue;
                if (!string.IsNullOrEmpty(tooltip))
                    tooltip += "\n";
                tooltip += message;
            }
        }

        GUIStyle headerStyle = (!stopAll && makeHeaderRed) ? AssetValidationCache.GetRedFoldoutStyle() : EditorStyles.foldout;

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
            const float pad = 2f;
            const float btnW = 90f;
            const float btnH = 16f;

            Rect stopAllRect = new Rect(headerRect.xMax - btnW, headerRect.y + (headerHeight - btnH) * 0.5f, btnW, btnH);
            Rect lightModeRect = new Rect(stopAllRect.xMin - pad - btnW, stopAllRect.y, btnW, btnH);
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, lightModeRect.xMin - headerRect.x - pad, headerHeight);

            label = new GUIContent(label.text, tooltip);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true, headerStyle);

            using (new GUIBackgroundScope(stopHeavy ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.35f, 0.6f, 0.35f)))
            {
                bool newStopHeavy = GUI.Toggle(lightModeRect, stopHeavy, new GUIContent("LIGHT MODE",
                    "ON: Only lightweight checks (null/empty) run.\nOFF: Run full validations."), EditorStyles.miniButton);
                if (newStopHeavy != stopHeavy)
                {
                    ValidationSettings.DisableHeavy = newStopHeavy;
                    GUIUtility.keyboardControl = 0;
                    GUI.changed = true;
                }
            }

            using (new GUIBackgroundScope(stopAll ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.35f, 0.6f, 0.35f)))
            {
                bool newStopAll = GUI.Toggle(stopAllRect, stopAll, new GUIContent("STOP ALL",
                    "ON: Disable ALL validation and highlights.\nOFF: Enable validation."), EditorStyles.miniButton);
                if (newStopAll != stopAll)
                {
                    ValidationSettings.DisableAll = newStopAll;
                    GUIUtility.keyboardControl = 0;
                    GUI.changed = true;
                }
            }
        }

        if (!property.isExpanded)
            return;

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

                if (!ValidationSettings.DisableAll)
                {
                    bool isInvalidHere = IsPathOrAncestorInvalid(iterator.propertyPath, invalidPathsWithMessage);
                    if (isInvalidHere)
                    {
                        Rect border = new Rect(childRect.x, childRect.y, 2f, height);
                        EditorGUI.DrawRect(border, Color.red);
                    }
                }

                EditorGUI.PropertyField(childRect, iterator, true);
                childRect.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    static bool ValidateSerializedObject(SerializedProperty objProp, AssetBundleData assetBundleData, List<(string path, string message)> invalidPathsWithMessage)
    {
        TypeChecksDescriptor descriptor = AssetValidationCache.GetDescriptorForType(objProp.GetTargetObjectOfProperty().GetType());
        return ValidateSerializedWithDescriptor(objProp, assetBundleData, descriptor, invalidPathsWithMessage);
    }

    static bool ValidateSerializedWithDescriptor(SerializedProperty objProp, AssetBundleData assetBundleData, TypeChecksDescriptor descriptor, List<(string path, string message)> invalidPathsWithMessage)
    {
        bool anyInvalid = false;

        if (ValidationSettings.DisableAll)
            return false;

        UnityEngine.Object target = objProp.serializedObject.targetObject;
        AssetValidationCache.PerObjectCache cache = AssetValidationCache.Get(target);

        for (int i = 0; i < descriptor.Entries.Count; i++)
        {
            FieldEntry entryDesc = descriptor.Entries[i];
            SerializedProperty fieldProp = objProp.FindPropertyRelative(entryDesc.FieldName);
            if (fieldProp == null)
                continue;

            if (entryDesc.HasAssertNotEmpty)
            {
                if (entryDesc.IsStringField)
                {
                    if (string.IsNullOrWhiteSpace(fieldProp.stringValue))
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, entryDesc.FieldName, invalidPathsWithMessage, entryDesc.FieldName + " is empty");
                    }
                }
                else
                {
                    string asString = fieldProp.propertyType == SerializedPropertyType.String ? fieldProp.stringValue : fieldProp.ToString();
                    if (string.IsNullOrWhiteSpace(asString))
                    {
                        anyInvalid = true;
                        TryAddRelativePath(objProp, entryDesc.FieldName, invalidPathsWithMessage, entryDesc.FieldName + " is empty");
                    }
                }
            }

            if (entryDesc.HasAssertFieldNotNull)
            {
                SerializedProperty guidProp = fieldProp.FindPropertyRelative("assetGUID");
                bool invalidNull = guidProp == null || string.IsNullOrEmpty(guidProp.stringValue);
                if (invalidNull)
                {
                    anyInvalid = true;
                    MarkNullReference(objProp, entryDesc.FieldName, entryDesc.ReferenceKind, invalidPathsWithMessage);
                    continue;
                }

                if (ValidationSettings.DisableHeavy)
                    continue;

                string guid = guidProp.stringValue;
                SerializedProperty parentProp = objProp.FindPropertyRelative(entryDesc.FieldName);
                string propPath = parentProp != null ? parentProp.propertyPath : objProp.propertyPath + "." + entryDesc.FieldName;

                if (!cache.Entries.TryGetValue(propPath, out var entry))
                {
                    entry = new AssetValidationCache.Entry();
                    cache.Entries[propPath] = entry;
                }

                if (!AssetValidationCache.TryGetPathForGuid(guid, out string? path) || string.IsNullOrEmpty(path))
                {
                    if (entry.LastGuid != guid)
                    {
                        entry.LastGuid = guid;
                        entry.LastInvalid = true;
                        entry.LastMessage = "Path to " + entryDesc.FieldName + " is invalid (empty GUID or unresolved).";
                    }
                    anyInvalid = true;
                    TryAddRelativePath(objProp, entryDesc.FieldName, invalidPathsWithMessage, entry.LastMessage);
                    continue;
                }

                (string? bundle, string? variant) = AssetValidationCache.GetBundleNames(path);

                bool keySame = entry.LastGuid == guid && entry.LastBundle == bundle && entry.LastVariant == variant;
                double now = EditorApplication.timeSinceStartup;
                bool canReuse = keySame && now < entry.NextAllowedDeepCheckTime;

                if (!canReuse)
                {
                    bool invalidHere = false;
                    string message = string.Empty;

                    switch (entryDesc.ReferenceKind)
                    {
                        case ReferenceKind.Enemy:
                        {
                            DuskEnemyDefinition? def = AssetValidationCache.LoadAtPathCached<DuskEnemyDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "DuskEnemyDefinition");
                            }
                            else
                            {
                                if (def.EnemyType == null)
                                {
                                    invalidHere = true;
                                    message = "EnemyType on " + def.name + " does not exist.";
                                }
                                else if (def.EnemyType.enemyPrefab == null)
                                {
                                    invalidHere = true;
                                    message = "EnemyPrefab on " + def.EnemyType.name + " does not exist.";
                                }
                            }
                            break;
                        }
                        case ReferenceKind.Item:
                        {
                            DuskItemDefinition? def = AssetValidationCache.LoadAtPathCached<DuskItemDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "DuskItemDefinition");
                            }
                            else
                            {
                                if (def.Item == null)
                                {
                                    invalidHere = true;
                                    message = "Item on " + def.name + " does not exist.";
                                }
                                else if (def.Item.spawnPrefab == null)
                                {
                                    invalidHere = true;
                                    message = "SpawnPrefab on " + def.Item.name + " does not exist.";
                                }
                            }
                            break;
                        }
                        case ReferenceKind.MapObject:
                        {
                            DuskMapObjectDefinition? def = AssetValidationCache.LoadAtPathCached<DuskMapObjectDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "DuskMapObjectDefinition");
                            }
                            else if (def.GameObject == null)
                            {
                                invalidHere = true;
                                message = "GameObject on " + def.name + " does not exist.";
                            }
                            break;
                        }
                        case ReferenceKind.Unlockable:
                        {
                            DuskUnlockableDefinition? def = AssetValidationCache.LoadAtPathCached<DuskUnlockableDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "DuskUnlockableDefinition");
                            }
                            else if (string.IsNullOrEmpty(def.UnlockableItem.unlockableName))
                            {
                                invalidHere = true;
                                message = "Unlockable's name on " + def.name + " is empty or does not exist.";
                            }
                            break;
                        }
                        case ReferenceKind.AdditionalTile:
                        {
                            DuskAdditionalTilesDefinition? def = AssetValidationCache.LoadAtPathCached<DuskAdditionalTilesDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "DuskAdditionalTilesDefinition");
                            }
                            else if (def.TilesToAdd == null)
                            {
                                invalidHere = true;
                                message = "TilesToAdd on " + def.name + " does not exist.";
                            }
                            break;
                        }
                        case ReferenceKind.Vehicle:
                        {
                            DuskVehicleDefinition? def = AssetValidationCache.LoadAtPathCached<DuskVehicleDefinition>(path);
                            if (def == null)
                            {
                                invalidHere = true;
                                message = MissingDef(path, "DuskVehicleDefinition");
                            }
                            else if (def.BuyableVehiclePreset == null)
                            {
                                invalidHere = true;
                                message = "Vehicle preset on " + def.name + " does not exist.";
                            }
                            else if (def.BuyableVehiclePreset.VehiclePrefab == null)
                            {
                                invalidHere = true;
                                message = "Vehicle prefab on vehicle preset on " + def.name + " does not exist.";
                            }
                            else if (def.BuyableVehiclePreset.StationPrefab == null)
                            {
                                invalidHere = true;
                                message = "Station prefab on vehicle preset on " + def.name + " does not exist.";
                            }
                            break;
                        }
                        case ReferenceKind.OtherContent:
                        {
                            if (WRExists)
                            {
                                DuskContentDefinition? def = AssetValidationCache.LoadAtPathCached<DuskContentDefinition>(path);
                                if (def == null)
                                {
                                    invalidHere = true;
                                    message = MissingDef(path, "DuskContentDefinition");
                                }
                                else if (WeatherRegistryChecks(def, out string wrMsg))
                                {
                                    invalidHere = true;
                                    message = wrMsg;
                                }
                            }
                            break;
                        }
                        default:
                            break;
                    }

                    if (!invalidHere)
                    {
                        if (string.IsNullOrEmpty(bundle))
                        {
                            invalidHere = true;
                            message = System.IO.Path.GetFileNameWithoutExtension(path) + " on the path: " + path + " is not assigned any AssetBundle.";
                        }
                        else if (bundle != assetBundleData.assetBundleName)
                        {
                            invalidHere = true;
                            message = System.IO.Path.GetFileNameWithoutExtension(path) + " on the path: " + path + " is assigned to the incorrect AssetBundle, it should be assigned to: " + assetBundleData.assetBundleName + ".";
                        }
                        else if (!string.IsNullOrEmpty(variant))
                        {
                            invalidHere = true;
                            message = System.IO.Path.GetFileNameWithoutExtension(path) + " on the path: " + path + " is assigned to an assetbundle with an extension, it should not have an extension.";
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
                    TryAddRelativePath(objProp, entryDesc.FieldName, invalidPathsWithMessage, entry.LastMessage);
                }
            }
        }

        return anyInvalid;
    }

    static bool IsPathOrAncestorInvalid(string path, List<(string path, string message)> invalidPathsWithMessage)
    {
        for (int i = 0; i < invalidPathsWithMessage.Count; i++)
        {
            if (path == invalidPathsWithMessage[i].path)
            {
                return true;
            }
        }

        int dot = path.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = path[..dot];
            for (int i = 0; i < invalidPathsWithMessage.Count; i++)
            {
                if (parent == invalidPathsWithMessage[i].path)
                {
                    return true;
                }
            }

            dot = parent.LastIndexOf('.');
        }
        return false;
    }

    static string MissingDef(string path, string typeName) => typeName + " on the path: " + path + " does not exist.";

    static void MarkNullReference(SerializedProperty objProp, string fieldName, ReferenceKind kind, List<(string path, string message)> invalidPathsWithMessage)
    {
        SerializedProperty parentProp = objProp.FindPropertyRelative(fieldName);
        if (parentProp != null)
        {
            invalidPathsWithMessage.Add((parentProp.propertyPath, fieldName + " is null"));

            SerializedProperty guidProp = parentProp.FindPropertyRelative("assetGUID");
            if (guidProp != null)
            {
                switch (kind)
                {
                    case ReferenceKind.Enemy:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "EnemyReference Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    case ReferenceKind.Item:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "ItemReference Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    case ReferenceKind.MapObject:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "MapObject Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    case ReferenceKind.Unlockable:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "Unlockable Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    case ReferenceKind.OtherContent:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "Weather Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    case ReferenceKind.AdditionalTile:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "AdditionalTiles Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    case ReferenceKind.Vehicle:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "Vehicle Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                    default:
                        invalidPathsWithMessage.Add((guidProp.propertyPath, "Unknown Reference is empty"));
                        invalidPathsWithMessage[^2] = (parentProp.propertyPath, "");
                        break;
                }
            }
        }
        else
        {
            TryAddRelativePath(objProp, fieldName, invalidPathsWithMessage, fieldName + " is null");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    static bool WeatherRegistryChecks(DuskContentDefinition contentDefinition, out string message)
    {
        message = string.Empty;
        if (contentDefinition is DuskWeatherDefinition weatherDefinition)
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
            return;

        SerializedProperty child = parent.FindPropertyRelative(relativeName);
        if (child != null)
        {
            pathsWithMessage.Add((child.propertyPath, message));
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

    readonly struct GUIBackgroundScope : IDisposable
    {
        readonly Color _prev;
        public GUIBackgroundScope(Color color)
        {
            _prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
        }
        public void Dispose() => GUI.backgroundColor = _prev;
    }
}