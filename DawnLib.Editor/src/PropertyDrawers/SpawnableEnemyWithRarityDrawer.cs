using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(SpawnableEnemyWithRarity))]
public class SpawnableEnemyWithRarityDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        bool hasPercent = SpawnableItemWithRarityDrawer.TryGetPercentShare(property, out float percent, out int index, out int totalRarity);
        string displayName = "Provide a valid Enemy with Rarity";

        SerializedProperty enemyProp = property.FindPropertyRelative(nameof(SpawnableEnemyWithRarity.enemyType));
        SerializedProperty rarityProp = property.FindPropertyRelative("rarity");

        if (enemyProp != null && enemyProp.objectReferenceValue != null)
        {
            ScriptableObject? enemySO = enemyProp.objectReferenceValue as ScriptableObject;
            SerializedProperty enemyNameField = new SerializedObject(enemySO).FindProperty(nameof(EnemyType.enemyName));
            if (enemyNameField != null && !string.IsNullOrEmpty(enemyNameField.stringValue))
            {
                displayName = $"{enemyNameField.stringValue} - {rarityProp.intValue}";
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