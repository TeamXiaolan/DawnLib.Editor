using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeRebirthLib.CRMod;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace CodeRebirthLib.Editor.PropertyDrawers;

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

        List<string> options = EditorJsonStringList.GetList();
        string[] displayOptions = new string[options.Count + 2];
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
                    string value = state.customValue.Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (!options.Contains(value))
                            EditorJsonStringList.AddToList(value);

                        SetReference(property, value, "Change Namespace");
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
                EditorGUI.FocusTextInControl(ctrlName);
        }
        else if (displayOptions[newIndex] == "<Remove all unused>")
        {
            List<CRMContentDefinition> definitions = ContentContainerEditor.FindAssetsByType<CRMContentDefinition>().ToList();

            var usedNamespaces = new HashSet<string>();
            foreach (CRMContentDefinition def in definitions)
            {
                SerializedObject serializedObject = new(def);
                SerializedProperty key = serializedObject.FindProperty("Key");
                if (key != null)
                {
                    SerializedProperty kNs = key.FindPropertyRelative("_namespace");
                    if (kNs != null && !string.IsNullOrEmpty(kNs.stringValue))
                        usedNamespaces.Add(kNs.stringValue);
                }
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
                    SetReference(nsProp, newNs, "Change Namespace");
                }
            }

            state.addingNew = false;
            state.customValue = "";
        }

        string currentKeyName;
        bool contentDefinitionExists = false;
        if (property.serializedObject.targetObject is CRMContentDefinition contentDefinition && fieldInfo.Name == "_typedKey")
        {
            contentDefinitionExists = true;
            string defaultKey = contentDefinition.GetDefaultKey();
            if (keyProp.stringValue != defaultKey)
            {
                SetReference(keyProp, defaultKey, "Change Key");
            }
            currentKeyName = defaultKey;
        }
        else
        {
            currentKeyName = keyProp.stringValue;
        }

        float line = EditorGUIUtility.singleLineHeight;
        float svs  = EditorGUIUtility.standardVerticalSpacing;

        float y = position.y;
        y += line + svs;
        if (GetState(property).addingNew)
            y += line + svs;

        Rect keyLabelRect = new(position.x, y, EditorGUIUtility.labelWidth, line);
        Rect keyValueRect = new(position.x + EditorGUIUtility.labelWidth, y, position.width - EditorGUIUtility.labelWidth, line);

        EditorGUI.LabelField(keyLabelRect, "Key");
        using (new EditorGUI.DisabledScope(contentDefinitionExists))
        {
            currentKeyName = EditorGUI.TextField(keyValueRect, currentKeyName);
            if (currentKeyName != keyProp.stringValue)
            {
                SetReference(keyProp, currentKeyName, "Change Key");
            }
        }
        EditorGUI.EndProperty();
    }

    private static void SetReference(SerializedProperty property, string value, string changeName)
    {
        Undo.RecordObject(property.serializedObject.targetObject, changeName);
        property.stringValue = value;
        EditorUtility.SetDirty(property.serializedObject.targetObject);
        property.serializedObject.ApplyModifiedProperties();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float svs = EditorGUIUtility.standardVerticalSpacing;

        float h = 0f;
        h += line;
        if (GetState(property).addingNew)
            h += svs + line;
        h += svs + line;

        return h;
    }
}

public static class EditorJsonStringList
{
    public static List<string> GetList()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json")))
        {
            return new();
        }
        List<string>? listOfNamespaces = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json")));
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
			File.WriteAllText(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json"), text);
        }
    }

    public static void RemoveFromList(List<string> list)
    {
        List<string> currentList = GetList();
        currentList.RemoveAll(list.Contains);
        string text = JsonConvert.SerializeObject(currentList, Formatting.Indented);
        File.WriteAllText(Path.Combine(Application.dataPath, "code_rebirth_lib_namespaces.json"), text);
    }
}