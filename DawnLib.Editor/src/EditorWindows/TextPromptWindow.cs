using System;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.EditorWindows;

public class TextPromptWindow : EditorWindow
{
    private string prompt = string.Empty;
    private string input = string.Empty;
    private Action<string>? onClose;

    public void OnGUI()
    {
        GUILayout.Label(prompt, EditorStyles.wordWrappedLabel);
        input = EditorGUILayout.TextField(input);

        GUILayout.Space(10);

        if (GUILayout.Button("OK"))
        {
            onClose?.Invoke(input);
            Close();
        }

        if (GUILayout.Button("Cancel"))
        {
            onClose?.Invoke("place_holder");
            Close();
        }
    }

    public static void Show(string prompt, string defaultText, Action<string> onClose)
    {
        TextPromptWindow window = CreateInstance<TextPromptWindow>();
        window.prompt = prompt;
        window.input = defaultText;
        window.onClose = onClose;
        window.titleContent = new GUIContent($"Input Required");
        window.position = new Rect(Screen.width / 2, Screen.height/ 2, 300, 100);
        window.ShowUtility();
    }
}