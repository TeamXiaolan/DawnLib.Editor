using CodeRebirthLib.CRMod;
using CodeRebirthLib.Editor.Extensions;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EntityData), true)]
public class EntityDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        BuildHeaderState(property, label, out string headerText, out bool headerRed, out string? tooltip);

        float headerHeight = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);
        Rect headerRect = new(position.x, position.y, position.width, headerHeight);

        GUIStyle foldoutStyle = EditorStyles.foldout;
        if (headerRed)
        {
            foldoutStyle = new GUIStyle(EditorStyles.foldout);
            Color color = Color.red;
            foldoutStyle.normal.textColor = color;
            foldoutStyle.onNormal.textColor = color;
            foldoutStyle.focused.textColor = color;
            foldoutStyle.onFocused.textColor = color;
            foldoutStyle.active.textColor = color;
            foldoutStyle.onActive.textColor = color;
            foldoutStyle.hover.textColor = color;
            foldoutStyle.onHover.textColor = color;
        }

        var headerContent = new GUIContent(headerText, tooltip);

        using (new EditorGUI.PropertyScope(headerRect, headerContent, property))
        {
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, headerContent, true, foldoutStyle);
        }

        if (property.isExpanded)
        {
            Rect childRect = new(position.x, position.y + headerHeight, position.width, 0f);
            using (new EditorGUI.IndentLevelScope(1))
            {
                var iter = property.Copy();
                var end  = iter.GetEndProperty();
                bool enterChildren = true;

                while (iter.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iter, end))
                {
                    enterChildren = false;

                    float h = EditorGUI.GetPropertyHeight(iter, true);
                    childRect.height = h;

                    EditorGUI.PropertyField(childRect, iter, true);
                    childRect.y += h + EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUI.GetPropertyHeight(property, label, includeChildren: false);

        if (property.isExpanded)
        {
            var iter = property.Copy();
            var end  = iter.GetEndProperty();
            bool enter = true;

            while (iter.NextVisible(enter) && !SerializedProperty.EqualContents(iter, end))
            {
                enter = false;
                height += EditorGUI.GetPropertyHeight(iter, true) + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        return height;
    }

    private static void BuildHeaderState(
        SerializedProperty property,
        GUIContent originalLabel,
        out string headerText,
        out bool headerRed,
        out string? tooltip)
    {
        headerText = originalLabel?.text ?? "Entity";
        headerRed = false;
        tooltip = null;

        object? obj = property.GetTargetObjectOfProperty();
        if (obj is not EntityData entity)
            return;

        if (entity is IInspectorHeaderWarning warn && warn.TryGetHeaderWarning(out string msg) && !string.IsNullOrEmpty(msg))
        {
            headerRed  = true;
            tooltip    = msg;
        }

        string? keyLabel = entity.Key?.ToString();
        if (!string.IsNullOrEmpty(keyLabel) && keyLabel != ":")
        {
            headerText = keyLabel;
        }
        else
        {
            if (!headerRed)
            {
                headerRed = true;
                tooltip   = "Entity key is empty.";
            }
            headerText = "Empty Entity";
        }
    }
}
