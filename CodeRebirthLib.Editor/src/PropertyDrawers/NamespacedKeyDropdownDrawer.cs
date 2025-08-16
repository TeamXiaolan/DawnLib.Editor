using System.Collections.Generic;
using System.Linq;
using CodeRebirthLib.CRMod;
using UnityEditor;
using UnityEngine;

namespace CodeRebirthLib.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(NamespacedKey))]
public class NamespacedKeyDropdownDrawer : PropertyDrawer
{
    private class State
    {
        public bool addingNew;
        public string customValue = "";
    }

    private static readonly Dictionary<string, State> _states = new();

    private static State GetState(SerializedProperty property)
    {
        if (!_states.TryGetValue(property.propertyPath, out var s))
        {
            s = new State();
            _states[property.propertyPath] = s;
        }
        return s;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var state = GetState(property);

        var options = EditorPrefsStringList.GetList("code_rebirth_lib_namespaces");

        // Read current value WITHOUT mutating during typing
        var target = property.serializedObject.targetObject;
        var current = (NamespacedKey)fieldInfo.GetValue(target);
        int index = options.IndexOf(current._namespace);

        EditorGUI.BeginProperty(position, label, property);

        string[] displayOptions = new string[options.Count + 2];
        for (int i = 0; i < options.Count; i++)
        {
            displayOptions[i] = options[i];
        }
        displayOptions[^2] = "<Remove all unused>";
        displayOptions[^1] = "<Add New>";

        int selectedIndex = state.addingNew ? displayOptions.Length - 1 : Mathf.Max(index, 0);

        var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        int newIndex = EditorGUI.Popup(dropdownRect, label.text, selectedIndex, displayOptions);

        if (displayOptions[newIndex] == "<Add New>")
        {
            state.addingNew = true;
            var textFieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("NSAddField");
            state.customValue = EditorGUI.TextField(textFieldRect, state.customValue);
            if (EditorGUI.EndChangeCheck())
            {

            }

            if (Event.current.type == EventType.KeyUp)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    string value = state.customValue.Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (!options.Contains(value))
                            EditorPrefsStringList.AddToList("code_rebirth_lib_namespaces", value);

                        current._namespace = value;
                        fieldInfo.SetValue(target, current);
                        property.serializedObject.ApplyModifiedProperties();
                        state.addingNew = false;
                        state.customValue = "";
                        GUI.FocusControl(null);
                        Event.current.Use();
                    }
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    state.addingNew = false;
                    state.customValue = "";
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }

            if (GUI.GetNameOfFocusedControl() != "NSAddField")
                EditorGUI.FocusTextInControl("NSAddField");
        }
        else if (displayOptions[newIndex] == "<Remove all unused>")
        {
            List<CRMContentDefinition> definitions = ContentContainerEditor.FindAssetsByType<CRMContentDefinition>().ToList();
            List<NamespacedKey> usedNamespaces = new();
            foreach (var definition in definitions)
            {
                if (string.IsNullOrEmpty(definition.Key._namespace))
                    continue;

                Debug.Log($"Definition: {definition.name} contains namespace: {definition.Key._namespace}");
                if (!usedNamespaces.Contains(definition.Key))
                {
                    usedNamespaces.Add(definition.Key);
                }
            }
            List<string> unusedNamespaces = options.Except(usedNamespaces.Select(it => it._namespace)).ToList();
            EditorPrefsStringList.RemoveFromList("code_rebirth_lib_namespaces", unusedNamespaces);

            state.addingNew = false;
            state.customValue = "";
        }
        else
        {
            if (newIndex >= 0 && newIndex < options.Count)
            {
                var newNs = options[newIndex];
                if (newNs != current._namespace)
                {
                    current._namespace = newNs;
                    fieldInfo.SetValue(target, current);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            state.addingNew = false;
            state.customValue = "";
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var state = GetState(property);
        return state.addingNew ? EditorGUIUtility.singleLineHeight * 2 + 2 : EditorGUIUtility.singleLineHeight;
    }
}

public static class EditorPrefsStringList
{
    public static List<string> GetList(string key)
    {
        string raw = EditorPrefs.GetString(key, "");
        var list = new List<string>();
        if (!string.IsNullOrEmpty(raw))
            list.AddRange(raw.Split('|'));
        list.RemoveAll(string.IsNullOrWhiteSpace);
        return list;
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

    public static void RemoveFromList(string key, List<string> list)
    {
        var currentList = GetList(key);
        currentList.RemoveAll(list.Contains);
        EditorPrefs.SetString(key, string.Join("|", currentList));
    }
}