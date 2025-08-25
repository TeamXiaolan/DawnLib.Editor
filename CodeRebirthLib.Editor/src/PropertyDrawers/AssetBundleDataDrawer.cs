using System;
using System.Collections.Generic;
using System.Reflection;
using CodeRebirthLib.CRMod;
using CodeRebirthLib.Editor.Extensions;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AssetBundleData), true)]
public class AssetBundleDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool makeRed = false;
        if (property.GetTargetObjectOfProperty() is AssetBundleData abd)
        {
            FieldInfo[] fieldInfos = typeof(AssetBundleData).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (FieldInfo fieldInfo in fieldInfos)
            {
                object obj = fieldInfo.GetValue(abd);

                if (fieldInfo.GetCustomAttribute<AssertNotEmpty>() != null)
                {
                    bool empty = string.IsNullOrWhiteSpace(obj?.ToString());
                    if (empty)
                    {
                        makeRed = true;
                    }
                    // TODO colour field red too.
                }

                if (fieldInfo.GetCustomAttribute<AssertFieldNotNull>() != null)
                {
                    bool invalid = obj == null || (obj is CRMContentReference cr && string.IsNullOrEmpty(cr.assetGUID));
                    if (invalid)
                    {
                        makeRed = true;
                    }
                    // TODO colour field red too.
                }
            }

            List<EntityData> entities = [.. abd.weathers, .. abd.enemies, .. abd.unlockables, .. abd.items, .. abd.mapObjects, .. abd.dungeons];
            foreach (EntityData entity in entities)
            {
                foreach (FieldInfo fieldInfo in GetAllInstanceFields(entity.GetType()))
                {
                    object obj = fieldInfo.GetValue(entity);

                    if (fieldInfo.GetCustomAttribute<AssertNotEmpty>() != null)
                    {
                        bool empty = string.IsNullOrWhiteSpace(obj?.ToString());
                        if (empty)
                        {
                            makeRed = true;
                        }
                    }

                    if (fieldInfo.GetCustomAttribute<AssertFieldNotNull>() != null)
                    {
                        bool invalid = obj == null || (obj is CRMContentReference cr && string.IsNullOrEmpty(cr.assetGUID));
                        if (invalid)
                        {
                            makeRed = true;
                        }
                    }
                }
            }
        }

        GUIStyle headerStyle = EditorStyles.foldout;
        if (makeRed)
        {
            headerStyle = new GUIStyle(EditorStyles.foldout);
            headerStyle.normal.textColor  = Color.red;
            headerStyle.onNormal.textColor = Color.red;
            headerStyle.focused.textColor = Color.red;
            headerStyle.onFocused.textColor = Color.red;
            headerStyle.active.textColor  = Color.red;
            headerStyle.onActive.textColor = Color.red;
            headerStyle.hover.textColor   = Color.red;
            headerStyle.onHover.textColor = Color.red;
        }

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true, headerStyle);
        }

        if (property.isExpanded)
        {
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
                    EditorGUI.PropertyField(childRect, iterator, true);
                    childRect.y += height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }
    }

    static IEnumerable<FieldInfo> GetAllInstanceFields(Type t)
    {
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type? cur = t; cur != null; cur = cur.BaseType)
            foreach (var f in cur.GetFields(bf))
                yield return f;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}