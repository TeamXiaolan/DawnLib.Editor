
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;

namespace CodeRebirthLib.Editor.Extensions;

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

    public static void SetReference(this SerializedProperty property, string value, string changeName)
    {
        Undo.RecordObject(property.serializedObject.targetObject, changeName);
        property.stringValue = value;
        EditorUtility.SetDirty(property.serializedObject.targetObject);
        property.serializedObject.ApplyModifiedProperties();
    }
}