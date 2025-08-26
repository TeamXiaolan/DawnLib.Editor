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

namespace CodeRebirthLib.Editor;
[CustomPropertyDrawer(typeof(AssetBundleData), true)]
public class AssetBundleDataDrawer : PropertyDrawer
{
    private sealed class ValidationEntry
    {
        public int fingerprint;
        public bool anyInvalid;
        public string tooltip = string.Empty;
        public List<(string path, string message)> invalids = new List<(string path, string message)>();
    }

    private static class ValidationCache
    {
        private static readonly Dictionary<int, ValidationEntry> _byInstance = new Dictionary<int, ValidationEntry>();

        public static bool TryGet(int id, out ValidationEntry e) => _byInstance.TryGetValue(id, out e);
        public static void Set(int id, ValidationEntry e) => _byInstance[id] = e;
        public static void Invalidate(int id) { _byInstance.Remove(id); }
        public static void Clear() => _byInstance.Clear();

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.projectChanged += Clear;
        }
    }

    private static class ValidationMode
    {
        private static readonly HashSet<int> _enabled = new HashSet<int>();

        public static bool IsEnabled(int instanceId) => _enabled.Contains(instanceId);
        public static void Enable(int instanceId) => _enabled.Add(instanceId);
        public static void Disable(int instanceId) => _enabled.Remove(instanceId);
        public static void Clear() => _enabled.Clear();

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            // When the project changes, forget all toggles
            EditorApplication.projectChanged += Clear;
        }
    }

    private static class AssetDbCache
    {
        private static readonly Dictionary<string, string> _guid2path = new Dictionary<string, string>();
        private static readonly Dictionary<string, (string bundle, string variant)> _bundleInfo = new Dictionary<string, (string bundle, string variant)>();

        public static string? GuidToPath(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            if (_guid2path.TryGetValue(guid, out string cached))
            {
                return cached;
            }

            string p = AssetDatabase.GUIDToAssetPath(guid);
            _guid2path[guid] = p;
            return p;
        }

        public static (string bundle, string variant) GetBundleInfo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return default;
            }

            if (_bundleInfo.TryGetValue(path, out (string bundle, string variant) bi))
            {
                return bi;
            }

            (string bundle, string variant) result = (AssetDatabase.GetImplicitAssetBundleName(path), AssetDatabase.GetImplicitAssetBundleVariantName(path));
            _bundleInfo[path] = result;
            return result;
        }

        public static void Clear()
        {
            _guid2path.Clear();
            _bundleInfo.Clear();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.projectChanged += Clear;
        }
    }

    private static GUIStyle? _redFoldout;
    private static GUIStyle RedFoldout
    {
        get
        {
            if (_redFoldout == null)
            {
                _redFoldout = new GUIStyle(EditorStyles.foldout);
                TintAllFoldoutStates(_redFoldout, Color.red);
            }
            return _redFoldout;
        }
    }

    private static bool DrawHeaderControls(Rect totalRect, bool isEnabled, out bool pressedValidate, out bool toggleValidate)
    {
        pressedValidate = toggleValidate = false;

        const float btnW = 90f;
        const float pad = 4f;

        Rect buttonRow = new Rect(totalRect.xMax - (isEnabled ? (btnW*2 + pad) : btnW) - pad, totalRect.y + 1f, isEnabled ? (btnW*2 + pad) : btnW, EditorGUIUtility.singleLineHeight);
        totalRect.width -= buttonRow.width + pad;
        using (new EditorGUI.DisabledScope(false))
        {
            if (!isEnabled)
            {
                if (GUI.Button(new Rect(buttonRow.x, buttonRow.y, btnW, buttonRow.height), "Toggle Validation"))
                {
                    pressedValidate = true;
                }
            }
            else
            {
                if (GUI.Button(new Rect(buttonRow.x + btnW + pad, buttonRow.y, btnW, buttonRow.height), "Toggle Validation"))
                {
                    toggleValidate = true;
                }
            }
        }

        return isEnabled;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return;

        UnityEngine.Object? rootTarget = property.serializedObject?.targetObject;
        if (rootTarget == null)
            return;

        int instanceId = rootTarget.GetInstanceID();

        float headerH = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new Rect(position.x, position.y, position.width, headerH);

        bool enabledForInstance = ValidationMode.IsEnabled(instanceId);
        DrawHeaderControls(headerRect, enabledForInstance, out bool pressedValidate, out bool toggleValidate);

        if (toggleValidate)
        {
            ValidationMode.Disable(instanceId);
            ValidationCache.Invalidate(instanceId);
            AssetDbCache.Clear();
            enabledForInstance = false;
        }
        else if (pressedValidate)
        {
            ValidationMode.Enable(instanceId);
            ValidationCache.Invalidate(instanceId);
            enabledForInstance = true;
        }

        if (!enabledForInstance)
        {
            using (new EditorGUI.PropertyScope(headerRect, label, property))
            {
                property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true, EditorStyles.foldout);
            }

            if (!property.isExpanded)
                return;

            Rect childRect = new Rect(position.x, position.y + headerH, position.width, 0f);
            using (new EditorGUI.IndentLevelScope(1))
            {
                var it = property.Copy();
                var end = it.GetEndProperty();
                bool enterChildren = true;

                while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
                {
                    enterChildren = false;
                    float height = EditorGUI.GetPropertyHeight(it, true);
                    childRect.height = height;
                    EditorGUI.PropertyField(childRect, it, true);
                    childRect.y += height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
            return;
        }

        int fingerprint = ComputeFingerprint(property);
        bool needDeepValidate = false;

        if (!ValidationCache.TryGet(instanceId, out ValidationEntry entry))
        {
            entry = new ValidationEntry();
            needDeepValidate = true;
        }
        else
        {
            if (entry.fingerprint != fingerprint)
            {
                needDeepValidate = true;
            }
        }

        if (needDeepValidate)
        {
            List<(string path, string message)> deepInvalids = ValidateDeep(property);
            entry.invalids = deepInvalids;
            entry.fingerprint = fingerprint;

            if (deepInvalids.Count > 0)
            {
                HashSet<string> existing = new HashSet<string>();
                for (int i = 0; i < entry.invalids.Count; i++)
                {
                    (string path, string message) = entry.invalids[i];
                    existing.Add(path + "||" + message);
                }
            }
        }

        entry.anyInvalid = entry.invalids.Count > 0;
        entry.tooltip = BuildTooltip(entry.invalids);
        ValidationCache.Set(instanceId, entry);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
            GUIContent lbl = new GUIContent(label.text, entry.tooltip);
            GUIStyle style = entry.anyInvalid ? RedFoldout : EditorStyles.foldout;
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, lbl, true, style);
        }

        if (!property.isExpanded) return;

        Rect childRect2 = new Rect(position.x, position.y + headerH, position.width, 0f);
        using (new EditorGUI.IndentLevelScope(1))
        {
            var it = property.Copy();
            var end = it.GetEndProperty();
            bool enterChildren = true;

            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                enterChildren = false;

                float height = EditorGUI.GetPropertyHeight(it, true);
                childRect2.height = height;

                bool invalidateHere = IsPathOrAncestorInvalid(it.propertyPath, entry.invalids);
                if (invalidateHere)
                {
                    using (new LabelAndGuiTintScope(Color.red))
                    {
                        EditorGUI.PropertyField(childRect2, it, true);
                    }
                }
                else
                {
                    EditorGUI.PropertyField(childRect2, it, true);
                }

                childRect2.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    private static List<(string path, string message)> ValidateFast(SerializedProperty root)
    {
        List<(string path, string message)> invalids = new List<(string path, string message)>();

        SerializedProperty configNameProp = root.FindPropertyRelative("configName");
        if (configNameProp != null && string.IsNullOrWhiteSpace(configNameProp.stringValue))
        {
            invalids.Add((configNameProp.propertyPath, "configName is empty"));
        }

        return invalids;
    }

    private static List<(string path, string message)> ValidateDeep(SerializedProperty root)
    {
        List<(string path, string message)> invalids = ValidateFast(root);

        object? targetObj = root.GetTargetObjectOfProperty();
        if (targetObj is not AssetBundleData assetBundleData)
        {
            TryAttributeNullChecks(root, invalids);
            return invalids;
        }

        ValidateObjectRecursive(assetBundleData, root, assetBundleData, invalids);
        TryAttributeNullChecks(root, invalids);
        return invalids.Distinct().ToList();
    }

    private static void ValidateObjectRecursive(object obj, SerializedProperty objProp, AssetBundleData assetBundleData, List<(string path, string message)> invalids)
    {
        foreach (FieldInfo fieldInfo in GetAllInstanceFields(obj.GetType()))
        {
            if (!IsFieldSerialized(fieldInfo))
                continue;

            object value = fieldInfo.GetValue(obj);
            SerializedProperty fieldProp = objProp.FindPropertyRelative(fieldInfo.Name);

            if (value is IList list && fieldProp != null && fieldProp.isArray)
            {
                int count = Mathf.Min(list.Count, fieldProp.arraySize);
                for (int i = 0; i < count; i++)
                {
                    object elem = list[i];
                    SerializedProperty elemProp = fieldProp.GetArrayElementAtIndex(i);

                    if (elem != null)
                    {
                        ValidateObjectRecursive(elem, elemProp, assetBundleData, invalids);
                    }
                    else
                    {
                        invalids.Add((elemProp.propertyPath, $"{fieldInfo.Name}[{i}] is null"));
                    }
                }
                continue;
            }

            if (fieldInfo.GetCustomAttribute<AssertNotEmpty>() != null)
            {
                bool empty = string.IsNullOrWhiteSpace(value?.ToString());
                if (empty)
                {
                    TryAddRelativePath(objProp, fieldInfo.Name, invalids, $"{fieldInfo.Name} is empty");
                }
            }

            if (IsCRMContentReference(fieldInfo.FieldType))
            {
                ValidateCRMContentReference(value, fieldProp, assetBundleData.assetBundleName, invalids);
                continue;
            }

            if (value != null && ShouldRecurseInto(value.GetType()))
            {
                if (fieldProp != null)
                {
                    ValidateObjectRecursive(value, fieldProp, assetBundleData, invalids);
                }
            }
        }
    }

    private static bool ShouldRecurseInto(Type type)
    {
        if (type.IsPrimitive)
        {
            return false;
        }

        if (type == typeof(string))
        {
            return false;
        }

        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            return false;
        }

        if (typeof(CRMContentReference).IsAssignableFrom(type))
        {
            return false;
        }

        return true;
    }


    private static IEnumerable<SerializedProperty> EnumerateChildren(SerializedProperty parent)
    {
        SerializedProperty prop = parent.Copy();
        SerializedProperty end = prop.GetEndProperty();
        bool enter = true;
        while (prop.NextVisible(enter) && !SerializedProperty.EqualContents(prop, end))
        {
            enter = false;
            yield return prop.Copy();
        }
    }

    private static string BuildTooltip(List<(string path, string message)> invalids)
    {
        if (invalids == null || invalids.Count == 0)
        {
            return string.Empty;
        }

        HashSet<string> seen = new HashSet<string>();
        List<string> lines = new List<string>();
        for (int i = 0; i < invalids.Count; i++)
        {
            string msg = invalids[i].message;
            if (string.IsNullOrEmpty(msg))
                continue;

            if (seen.Add(msg))
            {
                lines.Add(msg);
            }
        }
        return string.Join("\n", lines);
    }

    private static void ValidateCRMContentReference(object? contentRefObj, SerializedProperty fieldProp, string owningBundleName, List<(string path, string message)> invalids)
    {
        if (contentRefObj == null)
        {
            if (fieldProp != null)
            {
                invalids.Add((fieldProp.propertyPath, $"{fieldProp.displayName} is null"));
                SerializedProperty guidProp = fieldProp.FindPropertyRelative("assetGUID");
                if (guidProp != null)
                {
                    invalids.Add((guidProp.propertyPath, "Reference GUID is empty"));
                }
            }
            return;
        }

        if (contentRefObj is not CRMContentReference cr)
            return;

        SerializedProperty guidPropSp = fieldProp.FindPropertyRelative("assetGUID");

        if (string.IsNullOrEmpty(cr.assetGUID))
        {
            if (fieldProp != null)
            {
                invalids.Add((fieldProp.propertyPath, ""));
            }

            if (guidPropSp != null)
            {
                invalids.Add((guidPropSp.propertyPath, $"{PrettyRefName(cr)} Reference is empty"));
            }
            return;
        }

        string? path = AssetDbCache.GuidToPath(cr.assetGUID);
        if (string.IsNullOrEmpty(path))
        {
            TryAddRelativePath(fieldProp?.serializedObject?.FindProperty(fieldProp.propertyPath), "assetGUID", invalids, $"Path to {PrettyRefName(cr)} is invalid for GUID {cr.assetGUID}.");
            return;
        }

        if (cr is CRMEnemyReference)
        {
            CRMEnemyDefinition def = AssetDatabase.LoadAssetAtPath<CRMEnemyDefinition>(path);
            if (def == null)
            {
                invalids.Add((fieldProp.propertyPath, $"Asset at path '{path}' failed to load as CRMEnemyDefinition."));
            }
            else
            {
                if (def.EnemyType == null)
                {
                    invalids.Add((fieldProp.propertyPath, $"EnemyType on {def.name} does not exist."));
                }
                else if (def.EnemyType.enemyPrefab == null)
                {
                    invalids.Add((fieldProp.propertyPath, $"EnemyPrefab on {def.EnemyType.name} does not exist."));
                }
            }
        }
        else if (cr is CRMItemReference)
        {
            CRMItemDefinition def = AssetDatabase.LoadAssetAtPath<CRMItemDefinition>(path);
            if (def == null)
            {
                invalids.Add((fieldProp.propertyPath, $"Asset at path '{path}' failed to load as CRMItemDefinition."));
            }
            else
            {
                if (def.Item == null)
                {
                    invalids.Add((fieldProp.propertyPath, $"Item on {def.name} does not exist."));
                }
                else if (def.Item.spawnPrefab == null)
                {
                    invalids.Add((fieldProp.propertyPath, $"SpawnPrefab on {def.Item.name} does not exist."));
                }
            }
        }
        else if (cr is CRMMapObjectReference)
        {
            CRMMapObjectDefinition def = AssetDatabase.LoadAssetAtPath<CRMMapObjectDefinition>(path);
            if (def == null)
            {
                invalids.Add((fieldProp.propertyPath, $"Asset at path '{path}' failed to load as CRMMapObjectDefinition."));
            }
            else if (def.GameObject == null)
            {
                invalids.Add((fieldProp.propertyPath, $"GameObject on {def.name} does not exist."));
            }
        }
        else if (cr is CRMUnlockableReference)
        {
            CRMUnlockableDefinition def = AssetDatabase.LoadAssetAtPath<CRMUnlockableDefinition>(path);
            if (def == null)
            {
                invalids.Add((fieldProp.propertyPath, $"Asset at path '{path}' failed to load as CRMUnlockableDefinition."));
            }
            else if (def.UnlockableItem == null || string.IsNullOrEmpty(def.UnlockableItem.unlockableName))
            {
                invalids.Add((fieldProp.propertyPath, $"Unlockable's name on {def.name} is empty or does not exist."));
            }
        }
        else if (cr is CRMAdditionalTilesReference)
        {
            CRMAdditionalTilesDefinition def = AssetDatabase.LoadAssetAtPath<CRMAdditionalTilesDefinition>(path);
            if (def == null)
            {
                invalids.Add((fieldProp.propertyPath, $"Asset at path '{path}' failed to load as CRMAdditionalTilesDefinition."));
            }
            else if (def.TilesToAdd == null)
            {
                invalids.Add((fieldProp.propertyPath, $"TilesToAdd on {def.name} does not exist."));
            }
        }
        else
        {
            bool wrExists = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "WeatherRegistry");

            if (wrExists)
            {
                var def = AssetDatabase.LoadAssetAtPath<CRMContentDefinition>(path);
                if (def != null && WeatherRegistryChecks(def, out string msg))
                {
                    invalids.Add((fieldProp.propertyPath, msg));
                }
            }
        }

        (string bundleName, string variant) = AssetDbCache.GetBundleInfo(path);
        if (string.IsNullOrEmpty(bundleName))
        {
            invalids.Add((fieldProp.propertyPath, $"Asset at '{path}' is not assigned any AssetBundle."));
        }
        else if (!string.Equals(bundleName, owningBundleName, StringComparison.Ordinal))
        {
            invalids.Add((fieldProp.propertyPath, $"Asset at '{path}' is in the wrong AssetBundle. Expected '{owningBundleName}', found '{bundleName}'."));
        }

        if (!string.IsNullOrEmpty(variant))
        {
            invalids.Add((fieldProp.propertyPath, $"Asset at '{path}' uses an AssetBundle variant ('{variant}'), which is not allowed."));
        }
    }

    private static bool IsCRMContentReference(Type t) => typeof(CRMContentReference).IsAssignableFrom(t);

    private static string PrettyRefName(CRMContentReference cr)
    {
        return cr switch
        {
            CRMEnemyReference => "Enemy",
            CRMItemReference => "Item",
            CRMMapObjectReference => "MapObject",
            CRMUnlockableReference => "Unlockable",
            CRMWeatherReference => "Weather",
            CRMAdditionalTilesReference => "AdditionalTiles",
            _ => "Unknown"
        };
    }

    private static bool IsPathOrAncestorInvalid(string path, List<(string path, string message)> invalids)
    {
        for (int i = 0; i < invalids.Count; i++)
        {
            if (path == invalids[i].path)
            {
                return true;
            }
        }

        int dot = path.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = path[..dot];
            for (int i = 0; i < invalids.Count; i++)
            {
                if (parent == invalids[i].path)
                {
                    return true;
                }
            }
            dot = parent.LastIndexOf('.');
        }
        return false;
    }

    private static int ComputeFingerprint(SerializedProperty root)
    {
        unchecked
        {
            int hashCode = 17;

            SerializedProperty bundle = root.FindPropertyRelative("assetBundleName");
            if (bundle != null)
            {
                hashCode = hashCode * 31 + (bundle.stringValue ?? string.Empty).GetHashCode();
            }

            SerializedProperty config = root.FindPropertyRelative("configName");
            if (config != null)
            {
                hashCode = hashCode * 31 + (config.stringValue ?? string.Empty).GetHashCode();
            }

            foreach (SerializedProperty serializedProperty in EnumerateChildren(root))
            {
                if (serializedProperty.propertyType == SerializedPropertyType.String && serializedProperty.name == "assetGUID")
                {
                    hashCode = hashCode * 31 + (serializedProperty.stringValue ?? string.Empty).GetHashCode();
                }

                if (serializedProperty.propertyType == SerializedPropertyType.ManagedReference)
                {
                    hashCode = hashCode * 31 + serializedProperty.managedReferenceId.GetHashCode();
                }
            }

            return hashCode;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float baseHeight = EditorGUI.GetPropertyHeight(property, label, false);
        if (!property.isExpanded)
        {
            return baseHeight;
        }

        float childrenHeight = 0f;
        SerializedProperty iterator = property.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            childrenHeight += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return baseHeight + childrenHeight;
    }

    private static void TintAllFoldoutStates(GUIStyle style, Color color)
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

    private sealed class LabelAndGuiTintScope : IDisposable
    {
        private readonly Color _lN, _lON, _lF, _lOF, _lA, _lOA, _lH, _lOH;
        private readonly Color _guiColor, _contentColor;

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

    private static void TryAttributeNullChecks(SerializedProperty root, List<(string path, string message)> invalids)
    {
        object? targetObj = root.GetTargetObjectOfProperty();
        if (targetObj is not AssetBundleData assetBundleData)
        {
            return;
        }

        foreach (FieldInfo fieldInfo in GetAllInstanceFields(assetBundleData.GetType()))
        {
            if (!IsFieldSerialized(fieldInfo))
                continue;

            bool hasAssertNotNull = fieldInfo.GetCustomAttribute<AssertFieldNotNull>() != null;
            bool hasAssertNotEmpty = fieldInfo.GetCustomAttribute<AssertNotEmpty>() != null;
            if (!hasAssertNotNull && !hasAssertNotEmpty)
                continue;

            object val = fieldInfo.GetValue(assetBundleData);

            // [AssertNotEmpty] generic string field check
            if (hasAssertNotEmpty && fieldInfo.FieldType == typeof(string))
            {
                if (string.IsNullOrWhiteSpace(val as string))
                {
                    TryAddRelativePath(root, fieldInfo.Name, invalids, $"{fieldInfo.Name} is empty");
                }
            }

            if (hasAssertNotNull)
            {
                bool invalid = val == null;

                // CRMContentReference-like: treat empty assetGUID as null
                if (!invalid && val is CRMContentReference contentReference && string.IsNullOrEmpty(contentReference.assetGUID))
                {
                    invalid = true;
                }

                if (invalid)
                {
                    SerializedProperty parentProp = root.FindPropertyRelative(fieldInfo.Name);
                    if (parentProp != null)
                    {
                        invalids.Add((parentProp.propertyPath, $"{fieldInfo.Name} is null"));

                        SerializedProperty guidProp = parentProp.FindPropertyRelative("assetGUID");
                        if (guidProp != null)
                        {
                            invalids.Add((guidProp.propertyPath, "Reference GUID is empty"));
                        }
                    }
                    else
                    {
                        TryAddRelativePath(root, fieldInfo.Name, invalids, $"{fieldInfo.Name} is null");
                    }
                }
            }
        }
    }

    private static bool IsFieldSerialized(FieldInfo fieldInfo)
    {
        if (fieldInfo.IsDefined(typeof(NonSerializedAttribute), true))
        {
            return false;
        }

        if (fieldInfo.IsPublic)
        {
            return true;
        }

        if (fieldInfo.IsDefined(typeof(SerializeField), true))
        {
            return true;
        }

        if (fieldInfo.IsDefined(typeof(SerializeReference), true))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<FieldInfo> GetAllInstanceFields(Type type)
    {
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type cur = type; cur != null; cur = cur.BaseType)
        {
            FieldInfo[] fields = cur.GetFields(bf);
            for (int i = 0; i < fields.Length; i++)
            {
                yield return fields[i];
            }
        }
    }

    private static void TryAddRelativePath(SerializedProperty? parent, string relativeName, List<(string path, string message)> paths, string message)
    {
        if (parent == null)
            return;

        SerializedProperty child = parent.FindPropertyRelative(relativeName);
        if (child != null)
        {
            paths.Add((child.propertyPath, message));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool WeatherRegistryChecks(CRMContentDefinition contentDefinition, out string message)
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
}