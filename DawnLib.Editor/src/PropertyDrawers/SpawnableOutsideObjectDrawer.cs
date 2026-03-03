using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(SpawnableOutsideObjectWithRarity))]
public class SpawnableOutsideObjectWithRarityDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a valid Outside MapObject";

        SerializedProperty spawnableObjectProp = property.FindPropertyRelative(nameof(SpawnableOutsideObjectWithRarity.spawnableObject));

        if (spawnableObjectProp != null && spawnableObjectProp.objectReferenceValue != null)
        {
            ScriptableObject? spawnableSO = spawnableObjectProp.objectReferenceValue as ScriptableObject;
            if (spawnableSO != null && !string.IsNullOrEmpty(spawnableSO.name))
            {
                displayName = $"{spawnableSO.name}";
            }
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}