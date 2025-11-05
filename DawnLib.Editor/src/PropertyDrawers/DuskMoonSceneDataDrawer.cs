using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using Dusk;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(DuskMoonSceneData))]
public class DuskMoonSceneDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Provide a Scene";

        if (property.GetTargetObjectOfProperty() is DuskMoonSceneData data && data.Scene != null)
        {
            string sceneName = data.Scene.SceneName;
            string bundleName = data.Scene.BundleName;

            if (!string.IsNullOrEmpty(sceneName))
                displayName = sceneName;
            if (!string.IsNullOrEmpty(bundleName))
                displayName = $"{displayName} ({bundleName})";
        }

        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}