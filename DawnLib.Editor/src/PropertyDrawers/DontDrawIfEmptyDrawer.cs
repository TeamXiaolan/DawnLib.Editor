using System.Reflection;
using UnityEditor;
using UnityEngine;
using Dusk;
using JetBrains.Annotations;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(DontDrawIfEmpty))]
public class DontDrawIfEmptyDrawer : PropertyDrawer
{
    private const float HeaderGap = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!ShouldDraw(property))
            return;

        DontDrawIfEmpty attr = (DontDrawIfEmpty)attribute;

        if (ShouldDrawHeader(property, attr, out string? header))
        {
            Rect headerRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(headerRect, header, EditorStyles.boldLabel);

            position.y += EditorGUIUtility.singleLineHeight + HeaderGap;
        }

        float height = EditorGUI.GetPropertyHeight(property, label, true);
        Rect fieldRect = new Rect(position.x, position.y, position.width, height);

        GUIContent fieldLabel = new GUIContent(property.displayName, label.tooltip);
        EditorGUI.PropertyField(fieldRect, property, fieldLabel, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldDraw(property))
        {
            return 0f;
        }

        float height = EditorGUI.GetPropertyHeight(property, label, true);

        DontDrawIfEmpty attr = (DontDrawIfEmpty)attribute;
        if (ShouldDrawHeader(property, attr, out _))
        {
            height += EditorGUIUtility.singleLineHeight + HeaderGap;
        }

        return height;
    }

    private bool ShouldDraw(SerializedProperty property)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            return !string.IsNullOrEmpty(property.stringValue);
        }

        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            return property.objectReferenceValue != null;
        }

        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            return property.arraySize > 0;
        }

        return true;
    }

    private bool ShouldDrawHeader(SerializedProperty property, DontDrawIfEmpty attr, [CanBeNull] out string? header)
    {
        header = attr.Header;

        if (string.IsNullOrEmpty(attr.GroupId) || string.IsNullOrEmpty(attr.Header))
        {
            return false;
        }

        SerializedProperty iterator = property.serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == property.propertyPath)
            {
                return true;
            }

            FieldInfo? info = GetFieldInfoFromProperty(iterator);
            if (info == null)
            {
                continue;
            }

            DontDrawIfEmpty otherAttr = info.GetCustomAttribute<DontDrawIfEmpty>();
            if (otherAttr == null)
            {
                continue;
            }

            if (otherAttr.GroupId != attr.GroupId)
            {
                continue;
            }

            if (ShouldDraw(iterator))
            {
                return false;
            }
        }

        return true;
    }

    private FieldInfo? GetFieldInfoFromProperty(SerializedProperty property)
    {
        if (property == null || property.serializedObject?.targetObject == null)
        {
            return null;
        }

        System.Type type = property.serializedObject.targetObject.GetType();
        string[] path = property.propertyPath.Replace(".Array.data[", "[").Split('.');

        FieldInfo? field = null;

        foreach (string element in path)
        {
            if (element.Contains("["))
            {
                string fieldName = element.Substring(0, element.IndexOf("["));
                field = GetField(type, fieldName);
                if (field == null)
                {
                    return null;
                }

                type = GetElementType(field.FieldType);
            }
            else
            {
                field = GetField(type, element);
                if (field == null)
                {
                    return null;
                }

                type = field.FieldType;
            }
        }

        return field;
    }

    private FieldInfo? GetField(System.Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        while (type != null)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private System.Type GetElementType(System.Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType)
        {
            return type.GetGenericArguments()[0];
        }

        return type;
    }
}