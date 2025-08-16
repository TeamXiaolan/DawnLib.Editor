using System.Collections.Generic;
using CodeRebirthLib.CRMod;
using UnityEditor;
using UnityEngine;

namespace CodeRebirthLib.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(NamespacedKeyNameAttribute))]
public class NamespacedKeyDropdownDrawer : PropertyDrawer
{
    private bool isNew = false;
    private string customValue = "";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        NamespacedKeyNameAttribute attr = (NamespacedKeyNameAttribute)attribute;
        List<string> options = EditorPrefsStringList.GetList(attr.Key);

        string current = property.stringValue;
        int index = options.IndexOf(current);
        isNew = index == -1;

        EditorGUI.BeginProperty(position, label, property);

        string[] displayOptions = new string[options.Count + 1];
        for (int i = 0; i < options.Count; i++)
        {
            displayOptions[i] = options[i];
        }
        displayOptions[options.Count] = "<Add New>";

        int selectedIndex = isNew ? options.Count : index;
        Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        int newIndex = EditorGUI.Popup(dropdownRect, label.text, selectedIndex, displayOptions);

        if (newIndex == options.Count) // Add new selected
        {
            Rect textFieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            customValue = EditorGUI.TextField(textFieldRect, "New Value", current);
            property.stringValue = customValue;

            if (!string.IsNullOrWhiteSpace(customValue))
                EditorPrefsStringList.AddToList(attr.Key, customValue);
        }
        else
        {
            if (newIndex >= 0 && newIndex < options.Count)
                property.stringValue = options[newIndex];
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return isNew ? EditorGUIUtility.singleLineHeight * 2 + 2 : EditorGUIUtility.singleLineHeight;
    }
}

public static class EditorPrefsStringList
{
    public static List<string> GetList(string key)
    {
        string raw = EditorPrefs.GetString(key, "");
        return new List<string>(raw.Split('|')).FindAll(s => !string.IsNullOrWhiteSpace(s));
    }

    public static void AddToList(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var list = GetList(key);
        if (!list.Contains(value))
        {
            list.Add(value);
            EditorPrefs.SetString(key, string.Join("|", list));
        }
    }
}