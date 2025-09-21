using Dusk;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.PropertyDrawers;
[CustomPropertyDrawer(typeof(AssetBundleData), true)]
public class AssetBundleDataDrawer : PropertyDrawer
{
    static GUIStyle? _redFoldoutStyle;

    static GUIStyle RedFoldoutStyle
    {
        get
        {
            if (_redFoldoutStyle != null)
            {
                return _redFoldoutStyle;
            }
            _redFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            TintAllFoldoutStates(_redFoldoutStyle, Color.red);
            return _redFoldoutStyle;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty configNameProp = property.FindPropertyRelative("configName");
        bool configEmpty = configNameProp != null && string.IsNullOrWhiteSpace(configNameProp.stringValue);

        GUIStyle headerStyle = configEmpty ? RedFoldoutStyle : EditorStyles.foldout;
        string tooltip = configEmpty ? "ConfigName is empty." : string.Empty;

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        using (new EditorGUI.PropertyScope(headerRect, label, property))
        {
            label = new GUIContent(label.text, tooltip);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true, headerStyle);
        }

        if (!property.isExpanded)
        {
            return;
        }

        Rect childRect = new(position.x, position.y + headerHeight, position.width, 0f);
        using (new EditorGUI.IndentLevelScope(1))
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                childRect.height = height;

                if (configEmpty && iterator.propertyPath == configNameProp?.propertyPath)
                {
                    EditorGUI.DrawRect(new Rect(childRect.x, childRect.y, 2f, height), Color.red);
                }

                EditorGUI.PropertyField(childRect, iterator, true);
                childRect.y += height + EditorGUIUtility.standardVerticalSpacing;
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUI.GetPropertyHeight(property, label, true);

    static void TintAllFoldoutStates(GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        style.onNormal.textColor = color;
        style.focused.textColor = color;
        style.onFocused.textColor = color;
        style.active.textColor = color;
        style.onActive.textColor = color;
        style.hover.textColor = color;
        style.onHover.textColor = color;
    }
}