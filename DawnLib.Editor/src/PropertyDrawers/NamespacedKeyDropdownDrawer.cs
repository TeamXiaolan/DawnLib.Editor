using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dawn.Editor.Extensions;
using Dusk;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(NamespacedKey<>), true)]
[CustomPropertyDrawer(typeof(NamespacedKey), true)]
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
        if (fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), true).FirstOrDefault() is InspectorNameAttribute inspectorName)
        {
            label = new GUIContent(inspectorName.displayName);
        }

        SerializedProperty nsProp = property.FindPropertyRelative("_namespace");
        SerializedProperty keyProp = property.FindPropertyRelative("_key");

        EditorGUI.BeginProperty(position, label, property);

        List<string> options = ["<None>", "lethal_company"];
        options.AddRange(EditorJsonStringList.GetList());
        string[] displayOptions = new string[options.Count + 4];
        for (int i = 0; i < options.Count; i++) displayOptions[i] = options[i];
        displayOptions[^2] = "<Remove all unused>";
        displayOptions[^1] = "<Add New>";

        string currentNs = nsProp.stringValue;
        int index = Mathf.Max(options.IndexOf(currentNs), 0);

        Rect dropdownRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        State state = GetState(property);
        int selectedIndex = state.addingNew ? displayOptions.Length - 1 : index;
        int newIndex = EditorGUI.Popup(dropdownRect, label.text, selectedIndex, displayOptions);

        if (displayOptions[newIndex] == "<Add New>")
        {
            state.addingNew = true;

            Rect addNewRect = new(position.x, dropdownRect.yMax + 2, position.width, EditorGUIUtility.singleLineHeight);
            string ctrlName = $"NSAddField_{property.propertyPath}";
            GUI.SetNextControlName(ctrlName);
            state.customValue = EditorGUI.TextField(addNewRect, state.customValue);

            if (Event.current.type == EventType.KeyUp)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    string value = ContentContainerEditor.NormalizeNamespacedKey(state.customValue.Trim(), false);
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (!options.Contains(value))
                        {
                            EditorJsonStringList.AddToList(value);
                        }

                        property.SetStringReference(value, "Change Namespace");
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

            if (GUI.GetNameOfFocusedControl() != ctrlName)
            {
                EditorGUI.FocusTextInControl(ctrlName);
            }
        }
        else if (displayOptions[newIndex] == "<Remove all unused>")
        {
            List<DuskContentDefinition> definitions = ContentContainerEditor.FindAssetsByType<DuskContentDefinition>().ToList();

            List<string> usedNamespaces = new List<string>();
            foreach (DuskContentDefinition def in definitions)
            {
                if (def == null || def.Key == null || string.IsNullOrEmpty(def.Key.Namespace))
                {
                    continue;
                }
                usedNamespaces.Add(def.Key.Namespace);
            }

            List<string> unused = options.Except(usedNamespaces).ToList();
            EditorJsonStringList.RemoveFromList(unused);

            state.addingNew = false;
            state.customValue = "";
        }
        else
        {
            if (newIndex >= 0 && newIndex < options.Count)
            {
                var newNs = options[newIndex];
                if (newNs != currentNs)
                {
                    nsProp.SetStringReference(newNs, "Change Namespace");
                }
            }

            state.addingNew = false;
            state.customValue = "";
        }

        string currentKeyName;
        bool contentDefinitionExists = false;

        UnlockedNamespacedKey unlockedNamespacedKey = fieldInfo.GetCustomAttribute<UnlockedNamespacedKey>();
        DefaultKeySourceAttribute defaultKeySource = fieldInfo.GetCustomAttribute<DefaultKeySourceAttribute>();

        if (property.serializedObject.targetObject is DuskContentDefinition contentDefinition && unlockedNamespacedKey == null)
        {
            contentDefinitionExists = true;

            string? defaultKey = null;

            if (defaultKeySource != null)
            {
                object? owner = property.GetOwnerObjectOfProperty();
                int? elementIndex = property.GetElementIndex();

                defaultKey = GetDefaultKeyFromMember(owner, defaultKeySource.MemberName, defaultKeySource.Normalize, elementIndex);
            }

            if (string.IsNullOrEmpty(defaultKey))
            {
                defaultKey = ContentContainerEditor.NormalizeNamespacedKey(contentDefinition.GetDefaultKey(), false);
            }

            if (keyProp.stringValue != defaultKey)
            {
                keyProp.SetStringReference(defaultKey, "Change Key");
            }

            currentKeyName = defaultKey;
        }
        else
        {
            currentKeyName = ContentContainerEditor.NormalizeNamespacedKey(keyProp.stringValue, false);
        }

        float line = EditorGUIUtility.singleLineHeight;
        float svs = EditorGUIUtility.standardVerticalSpacing;

        float y = position.y;
        y += line + svs;
        if (GetState(property).addingNew)
        {
            y += line + svs;
        }

        Rect keyRow = new(position.x, y, position.width, line);
        using (new EditorGUI.DisabledScope(contentDefinitionExists))
        {
            if (currentKeyName != keyProp.stringValue)
            {
                keyProp.SetStringReference(currentKeyName, "Change Key");
            }
            EditorGUI.PropertyField(keyRow, keyProp, new GUIContent("Key"));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float svs = EditorGUIUtility.standardVerticalSpacing;

        float h = 0f;
        h += line;
        if (GetState(property).addingNew)
        {
            h += svs + line;
        }
        h += svs + line;

        return h;
    }

    private static string? GetDefaultKeyFromMember(object? target, string memberName, bool normalize, int? index)
    {
        if (target == null)
        {
            return null;
        }

        const BindingFlags BINDING_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = target.GetType();

        if (type.GetField(memberName, BINDING_FLAGS) is FieldInfo fieldInfo)
        {
            return CoerceKey(fieldInfo.GetValue(target), normalize, index);
        }

        if (type.GetProperty(memberName, BINDING_FLAGS) is PropertyInfo propertyInfo)
        {
            return CoerceKey(propertyInfo.GetValue(target, null), normalize, index);
        }

        if (type.GetMethod(memberName, BINDING_FLAGS, null, [typeof(int)], null) is MethodInfo methodInfoInt && index.HasValue)
        {
            return CoerceKey(methodInfoInt.Invoke(target, [index.Value]), normalize, index);
        }

        if (type.GetMethod(memberName, BINDING_FLAGS, null, Type.EmptyTypes, null) is MethodInfo methodInfo)
        {
            return CoerceKey(methodInfo.Invoke(target, null), normalize, index);
        }

        return null;
    }

    private static string? CoerceKey(object? value, bool normalize, int? index)
    {
        if (value == null)
        {
            return null;
        }

        switch (value)
        {
            case string input:
                return normalize ? ContentContainerEditor.NormalizeNamespacedKey(input, false) : input;

            case NamespacedKey namespacedKey:
                return normalize ? ContentContainerEditor.NormalizeNamespacedKey(namespacedKey.Key, false) : namespacedKey.Key;

            case System.Collections.IList list when index.HasValue && index.Value >= 0 && index.Value < list.Count:
                object element = list[index.Value];
                if (element is NamespacedKey namespacedKeyElement)
                {
                    return normalize ? ContentContainerEditor.NormalizeNamespacedKey(namespacedKeyElement.Key, false) : namespacedKeyElement.Key;
                }

                if (element is string inputElement)
                {
                    return normalize ? ContentContainerEditor.NormalizeNamespacedKey(inputElement, false) : inputElement;
                }

                return null;
        }
        return null;
    }
}

public static class EditorJsonStringList
{
    public static List<string> GetList()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "dawn_lib_namespaces.json")))
        {
            if (File.Exists(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json")))
            {
                List<string>? listNewOfNamespaces = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json")));
                listNewOfNamespaces ??= new();
                listNewOfNamespaces.RemoveAll(string.IsNullOrWhiteSpace);
                string text = JsonConvert.SerializeObject(listNewOfNamespaces, Formatting.Indented);
                File.WriteAllText(Path.Combine(Application.dataPath, "dawn_lib_namespaces.json"), text);
                File.Delete(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json"));
                return listNewOfNamespaces;
            }
            return new();
        }
        List<string>? listOfNamespaces = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Path.Combine(Application.dataPath, "dawn_lib_namespaces.json")));
        listOfNamespaces ??= new();

        listOfNamespaces.RemoveAll(string.IsNullOrWhiteSpace);
        return listOfNamespaces;
    }

    public static void AddToList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        List<string> list = GetList();
        if (!list.Contains(value))
        {
            list.Add(value);
			string text = JsonConvert.SerializeObject(list, Formatting.Indented);
			File.WriteAllText(Path.Combine(Application.dataPath, "dawn_lib_namespaces.json"), text);
        }
    }

    public static void RemoveFromList(List<string> list)
    {
        List<string> currentList = GetList();
        currentList.RemoveAll(list.Contains);
        string text = JsonConvert.SerializeObject(currentList, Formatting.Indented);
        File.WriteAllText(Path.Combine(Application.dataPath, "dawn_lib_namespaces.json"), text);
    }
}