using System;
using Dawn.Dusk;
using Dawn.Editor.Extensions;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(AssetBundleReference), true)]
public class AssetBundleReferenceDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        string[] options = AssetDatabase.GetAllAssetBundleNames();
        string[] displayOptions = new string[options.Length + 1];
        displayOptions[0] = "<None>";
        for (int i = 0; i < options.Length; i++)
        {
            displayOptions[i + 1] = options[i];
        }

        string currentAB = property.stringValue;
        int index = Mathf.Max(Array.IndexOf(displayOptions, currentAB), 0);

        Rect dropdownRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        int selectedIndex = index;
        int newIndex = EditorGUI.Popup(dropdownRect, label.text, selectedIndex, displayOptions);

        if (newIndex >= 0 && newIndex < displayOptions.Length)
        {
            string newAB = displayOptions[newIndex];
            if (newAB != currentAB)
            {
                property.SetStringReference(newAB, "Change AssetBundleReference");
            }
        }
        EditorGUI.EndProperty();
    }
}