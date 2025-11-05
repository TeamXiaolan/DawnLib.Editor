
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;

namespace Dawn.Editor.Extensions;

static class SerializedPropertyExtensions
{
    public static object? GetTargetObjectOfProperty(this SerializedProperty prop)
    {
        if (prop == null)
        {
            return null;
        }

        string path = prop.propertyPath.Replace(".Array.data[", "[");
        object? obj = prop.serializedObject.targetObject;
        string[] elements = path.Split('.');

        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                string elementName = element[..element.IndexOf("[")];
                int index = Convert.ToInt32(element[element.IndexOf("[")..].Trim('[', ']'));
                obj = GetValue_Imp(obj!, elementName, index);
            }
            else
            {
                obj = GetValue_Imp(obj!, element);
            }
        }
        return obj;
    }

    static object? GetValue_Imp(object source, string name)
    {
        if (source == null)
        {
            return null;
        }

        Type? type = source.GetType();
        while (type != null)
        {
            FieldInfo? fieldInfo = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                return fieldInfo.GetValue(source);
            }

            PropertyInfo? propertyInfo = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (propertyInfo != null)
            {
                return propertyInfo.GetValue(source, null);
            }
            type = type.BaseType;
        }
        return null;
    }

    static object? GetValue_Imp(object source, string name, int index)
    {
        if (GetValue_Imp(source, name) is not IEnumerable enumerable)
            return null;

        IEnumerator enm = enumerable.GetEnumerator();
        for (int i = 0; i <= index; i++)
        {
            if (!enm.MoveNext())
            {
                return null;
            }
        }
        return enm.Current;
    }

    public static void SetStringReference(this SerializedProperty property, string value, string changeName)
    {
        Undo.RecordObject(property.serializedObject.targetObject, changeName);
        property.stringValue = value;
        EditorUtility.SetDirty(property.serializedObject.targetObject);
        property.serializedObject.ApplyModifiedProperties();
    }

    public static void SetManagedReference(this SerializedProperty property, object obj, string changeName)
    {
        Undo.RecordObject(property.serializedObject.targetObject, changeName);
        property.managedReferenceValue = obj;
        EditorUtility.SetDirty(property.serializedObject.targetObject);
        property.serializedObject.ApplyModifiedProperties();
    }

    public static object? GetOwnerObjectOfProperty(this SerializedProperty serializedProperty)
    {
        if (serializedProperty == null)
        {
            return null;
        }

        string path = serializedProperty.propertyPath.Replace(".Array.data[", "[");
        object? targetObject = serializedProperty.serializedObject.targetObject;
        string[] pathParts = path.Split('.');

        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            string element = pathParts[i];
            if (element.Contains("["))
            {
                string elementName = element[..element.IndexOf("[")];
                int index = Convert.ToInt32(element[element.IndexOf("[")..].Trim('[', ']'));
                targetObject = GetValue_Imp(targetObject!, elementName, index);
            }
            else
            {
                targetObject = GetValue_Imp(targetObject!, element);
            }
            if (targetObject == null)
            {
                break;
            }
        }
        return targetObject;
    }

    public static int? GetElementIndex(this SerializedProperty serializedProperty)
    {
        if (serializedProperty == null)
        {
            return null;
        }
        string path = serializedProperty.propertyPath;
        int lastOpenBracket = path.LastIndexOf('[');
        int lastClosingBracket = path.LastIndexOf(']');
        if (lastOpenBracket >= 0 && lastClosingBracket > lastOpenBracket && int.TryParse(path.AsSpan(lastOpenBracket + 1, lastClosingBracket - lastOpenBracket - 1), out int index))
        {
            return index;
        }
        return null;
    }
}