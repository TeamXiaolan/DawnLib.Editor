using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        HashSet<string> invalidPaths = new();
        bool makeHeaderRed = false;

        if (property.GetTargetObjectOfProperty() is AssetBundleData assetBundleData)
        {
            makeHeaderRed |= ValidateObjectFieldsAndCollectPaths(assetBundleData, property, invalidPaths);

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
                    makeHeaderRed |= ValidateObjectFieldsAndCollectPaths(elem, elemProp, invalidPaths);
                }
            }
        }

        GUIStyle headerStyle = EditorStyles.foldout;
        if (makeHeaderRed)
        {
            headerStyle = new GUIStyle(EditorStyles.foldout);
            TintAllFoldoutStates(headerStyle, Color.red);
        }

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
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

                bool isInvalidHere = IsPathOrAncestorInvalid(iterator.propertyPath, invalidPaths);

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

    static bool IsPathOrAncestorInvalid(string path, HashSet<string> invalidPaths)
    {
        if (invalidPaths.Contains(path))
        {
            return true;
        }

        // Check ancestors: "a.b.color" => "a.b", "a"
        int dot = path.LastIndexOf('.');
        while (dot > 0)
        {
            string parent = path.Substring(0, dot);
            if (invalidPaths.Contains(parent))
            {
                return true;
            }

            dot = parent.LastIndexOf('.');
        }
        return false;
    }

    static bool ValidateObjectFieldsAndCollectPaths(object obj, SerializedProperty objProp, HashSet<string> invalidPaths)
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
                    TryAddRelativePath(objProp, fieldInfo.Name, invalidPaths);
                }
            }

            // [AssertFieldNotNull]
            if (fieldInfo.GetCustomAttribute<AssertFieldNotNull>() != null)
            {
                bool invalid =
                    value == null ||
                    (value is CRMContentReference cr && string.IsNullOrEmpty(cr.assetGUID));

                if (invalid)
                {
                    anyInvalid = true;

                    SerializedProperty parentProp = objProp.FindPropertyRelative(fieldInfo.Name);
                    if (parentProp != null)
                    {
                        invalidPaths.Add(parentProp.propertyPath);

                        SerializedProperty guidProp = parentProp.FindPropertyRelative("assetGUID");
                        if (guidProp != null)
                            invalidPaths.Add(guidProp.propertyPath);
                    }
                    else
                    {
                        TryAddRelativePath(objProp, fieldInfo.Name, invalidPaths);
                    }
                }
            }
        }
        return anyInvalid;
    }


    static void TryAddRelativePath(SerializedProperty parent, string relativeName, HashSet<string> paths)
    {
        if (parent == null)
        {
            return;
        }

        SerializedProperty child = parent.FindPropertyRelative(relativeName);
        if (child != null) paths.Add(child.propertyPath);
    }

    static bool IsFieldSerialized(FieldInfo f)
    {
        if (f.IsDefined(typeof(NonSerializedAttribute), inherit: true))
            return false;

        if (f.IsPublic)
            return true;

        if (f.IsDefined(typeof(SerializeField), inherit: true))
            return true;

        if (f.IsDefined(typeof(SerializeReference), inherit: true))
            return true;

        return false;
    }


    static IEnumerable<FieldInfo> GetAllInstanceFields(Type t)
    {
        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type cur = t; cur != null; cur = cur.BaseType)
        {
            foreach (var f in cur.GetFields(bindingFlags))
            {
                yield return f;
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
