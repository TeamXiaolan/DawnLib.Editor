using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(SpawnableItemWithRarity))]
public class SpawnableItemWithRarityDrawer : PropertyDrawer
{
    static readonly Regex ArrayElementRegex = new Regex(@"\.Array\.data\[(\d+)\]");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool hasPercent = TryGetPercentShare(property, out float percent, out int index, out int totalRarity);
        string displayName = "Provide a valid Item with Rarity";

        SerializedProperty itemProp = property.FindPropertyRelative("spawnableItem");
        SerializedProperty rarityProp = property.FindPropertyRelative("rarity");

        if (itemProp != null && itemProp.objectReferenceValue != null)
        {
            ScriptableObject? itemSO = itemProp.objectReferenceValue as ScriptableObject;
            SerializedProperty itemNameField = new SerializedObject(itemSO).FindProperty("itemName");
            if (itemNameField != null && !string.IsNullOrEmpty(itemNameField.stringValue))
            {
                displayName = $"{itemNameField.stringValue} - {rarityProp.intValue}";
            }
        }

        if (hasPercent)
        {
            displayName += $"  ({percent:0.#}% of list, Σ={totalRarity})";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    private static bool TryGetPercentShare(SerializedProperty elementProperty, out float percent, out int elementIndex, out int totalRarity)
    {
        percent = 0f;
        elementIndex = -1;
        totalRarity = 0;

        Match match = ArrayElementRegex.Match(elementProperty.propertyPath);
        if (!match.Success)
        {
            return false;
        }

        elementIndex = int.Parse(match.Groups[1].Value);

        int arrayTokenIndex = match.Index;
        string parentArrayPath = elementProperty.propertyPath[..arrayTokenIndex];

        SerializedProperty arrayProp = elementProperty.serializedObject.FindProperty(parentArrayPath);
        if (arrayProp == null || !arrayProp.isArray)
        {
            return false;
        }

        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty serializedProperty = arrayProp.GetArrayElementAtIndex(i);
            if (serializedProperty == null)
                continue;

            SerializedProperty rarity = serializedProperty.FindPropertyRelative("rarity");
            if (rarity != null)
            {
                totalRarity += rarity.intValue;
            }
        }

        if (totalRarity <= 0)
        {
            return true;
        }

        SerializedProperty myRarityProp = elementProperty.FindPropertyRelative("rarity");
        int myRarity = myRarityProp != null ? myRarityProp.intValue : 0;

        percent = (myRarity / (float)totalRarity) * 100f;
        return true;
    }
}