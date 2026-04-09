using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Dusk;

namespace Dawn.Editor;

[CustomEditor(typeof(ConfigReader))]
public class ConfigReaderEditor : UnityEditor.Editor
{
    private readonly Dictionary<string, SerializedProperty> _propertyCache = new();

    private bool _showGeneralEvents = true;
    private bool _showTypedEvent = true;

    private SerializedProperty FindCached(string name)
    {
        if (_propertyCache.TryGetValue(name, out SerializedProperty property) && property != null)
        {
            return property;
        }

        property = serializedObject.FindProperty(name);
        _propertyCache[name] = property;
        return property;
    }

    private void ClearCache()
    {
        _propertyCache.Clear();
    }

    private void OnDisable()
    {
        ClearCache();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty pluginGuid = FindCached("pluginGuid");
        SerializedProperty section = FindCached("section");
        SerializedProperty key = FindCached("key");
        SerializedProperty expectedType = FindCached("expectedType");
        SerializedProperty invokeOnStart = FindCached("invokeOnStart");

        SerializedProperty onUnsupportedType = FindCached("onUnsupportedType");
        SerializedProperty onEntryNotFound = FindCached("onEntryNotFound");
        SerializedProperty onTypeMismatch = FindCached("onTypeMismatch");

        DrawTargetSection(pluginGuid, section, key, expectedType, invokeOnStart);
        EditorGUILayout.Space();
        DrawGeneralEventsSection(onEntryNotFound, onUnsupportedType, onTypeMismatch);
        EditorGUILayout.Space();
        DrawTypedEventSection(expectedType);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTargetSection(SerializedProperty pluginGuid, SerializedProperty section, SerializedProperty key, SerializedProperty expectedType, SerializedProperty invokeOnStart)
    {
        EditorGUILayout.PropertyField(pluginGuid);
        EditorGUILayout.PropertyField(section);
        EditorGUILayout.PropertyField(key);
        EditorGUILayout.PropertyField(expectedType);

        EditorGUILayout.Space(2f);

        EditorGUILayout.PropertyField(invokeOnStart);
    }

    private void DrawGeneralEventsSection(SerializedProperty onEntryNotFound, SerializedProperty onUnsupportedType, SerializedProperty onTypeMismatch)
    {
        _showGeneralEvents = EditorGUILayout.Foldout(_showGeneralEvents, "General Events", true);
        if (!_showGeneralEvents)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(onEntryNotFound);
            EditorGUILayout.PropertyField(onUnsupportedType);
            EditorGUILayout.PropertyField(onTypeMismatch);
        }
    }

    private void DrawTypedEventSection(SerializedProperty expectedType)
    {
        DuskDynamicConfigType selectedType = (DuskDynamicConfigType)expectedType.enumValueIndex;
        _showTypedEvent = EditorGUILayout.Foldout(_showTypedEvent, $"Typed Event ({selectedType})", true);
        if (!_showTypedEvent)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            string eventFieldName = ConfigReaderTypeUtility.GetEventFieldName(selectedType);
            SerializedProperty typedEvent = FindCached(eventFieldName);

            if (typedEvent == null)
            {
                EditorGUILayout.HelpBox($"Could not find serialized event field '{eventFieldName}' for type '{selectedType}'.", MessageType.Error);
                return;
            }

            EditorGUILayout.PropertyField(typedEvent);
        }
    }
}