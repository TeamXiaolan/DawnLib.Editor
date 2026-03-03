using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(IntWithRarity))]
public class IntWithRarityDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool hasPercent = SpawnableItemWithRarityDrawer.TryGetPercentShare(property, out float percent, out int index, out int totalRarity);
        string displayName = "Provide a valid Dungeon ID with Rarity (0, 1 or 4)";

        SerializedProperty intWithRarityProp = property.FindPropertyRelative(nameof(IntWithRarity.id));
        SerializedProperty rarityProp = property.FindPropertyRelative("rarity");

        if (intWithRarityProp != null && (intWithRarityProp.intValue == 0 || intWithRarityProp.intValue == 1 || intWithRarityProp.intValue == 4))
        {
            switch (intWithRarityProp.intValue)
            {
                case 0:
                    displayName = $"Facility - {rarityProp.intValue}";
                    break;
                case 1:
                    displayName = $"Mansion - {rarityProp.intValue}";
                    break;
                case 4:
                    displayName = $"Mineshaft - {rarityProp.intValue}";
                    break;
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
}