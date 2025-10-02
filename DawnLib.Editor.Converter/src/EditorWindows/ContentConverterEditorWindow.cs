using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Dawn.Utils;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.EditorWindows;
public class DuskContentConverter : EditorWindow
{
    private UnityEngine.Object? source;
    private MonoScript? chosenClass;
    private string outputFolder = "";
    private string authorName = "placeholder";


    [MenuItem("DawnLib/Dusk/Content Converter")]
    private static void Open()
    {
        GetWindow<DuskContentConverter>("Dusk Content Converter");
    }

    private void OnGUI()
    {
        source = EditorGUILayout.ObjectField("Source (AI)", source, typeof(MonoScript), false);
        chosenClass = source as MonoScript;

        if (source == null)
            return;

        if (chosenClass == null)
        {
            EditorGUILayout.HelpBox($"No MonoBehaviours found on this Object {source.name}", MessageType.Error);
            return;
        }

        outputFolder = EditorGUILayout.TextField("Output Folder (Assets)", outputFolder);
        authorName = EditorGUILayout.TextField("Author Name", authorName);
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(source == null))
        {
            if (GUILayout.Button("Bake → Build DLL → Import"))
            {
                Bake();
            }
        }
    }
}
