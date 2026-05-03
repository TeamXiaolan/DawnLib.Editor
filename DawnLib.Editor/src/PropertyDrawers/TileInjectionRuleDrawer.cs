using UnityEditor;
using UnityEngine;
using Dawn.Editor.Extensions;
using DunGen;
using System.Text;

namespace Dawn.Editor.PropertyDrawers;

[CustomPropertyDrawer(typeof(TileInjectionRule))]
public class TileInjectionRuleDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string displayName = "Min is higher than Max";

        if (property.GetTargetObjectOfProperty() is TileInjectionRule data)
        {
            if (data.NormalizedPathDepth.Min > data.NormalizedPathDepth.Max)
            {
                goto end;
            }

            if (data.NormalizedBranchDepth.Min > data.NormalizedBranchDepth.Max)
            {
                goto end;
            }

            if (data.TileSet == null)
            {
                displayName = "TileSet is null";
                goto end;
            }

            StringBuilder sb = new(data.TileSet.name.Length * 3);
            sb.Append(data.TileSet.name);
            if (data.CanAppearOnMainPath)
            {
                sb.Append($" Main: {data.NormalizedPathDepth.Min} | {data.NormalizedPathDepth.Max}");
                if (data.CanAppearOnBranchPath)
                {
                    sb.Append(" | ");
                }
            }

            if (data.CanAppearOnBranchPath)
            {
                sb.Append($" Branch: {data.NormalizedBranchDepth.Min} | {data.NormalizedBranchDepth.Max}");
            }
            displayName = sb.ToString();

        }

    end:
        label.text = displayName;
        EditorGUI.PropertyField(position, property, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}