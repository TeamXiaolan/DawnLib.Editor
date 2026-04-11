using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dawn.Editor.GizmosStuff;

[InitializeOnLoad]
public static class BeeHiveVisualiser
{
    private static readonly Vector3 SphereCenter = new Vector3(4.48f, -0.38f, -14.3f);
    private const float SphereRadius = 40f;

    private const string ToggleKey = "Dawn.Editor.BeeHiveVisualiser.Enabled";

    private static List<SelectableLevel> cachedLevels = new();

    private static bool IsEnabled
    {
        get => EditorPrefs.GetBool(ToggleKey, true);
        set => EditorPrefs.SetBool(ToggleKey, value);
    }

    static BeeHiveVisualiser()
    {
        RefreshSelectableLevels();

        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.delayCall += UpdateMenuCheckmark;
    }

    [MenuItem("DawnLib/Gizmos/Toggle Bee Hive Visualiser")]
    private static void ToggleVisualiser()
    {
        IsEnabled = !IsEnabled;
        UpdateMenuCheckmark();
        SceneView.RepaintAll();
    }

    [MenuItem("DawnLib/Gizmos/Toggle Bee Hive Visualiser", true)]
    private static bool ToggleVisualiserValidate()
    {
        Menu.SetChecked("DawnLib/Gizmos/Toggle Bee Hive Visualiser", IsEnabled);
        return true;
    }

    private static void UpdateMenuCheckmark()
    {
        Menu.SetChecked("DawnLib/Gizmos/Toggle Bee Hive Visualiser", IsEnabled);
    }

    private static void OnProjectChanged()
    {
        RefreshSelectableLevels();
    }

    private static void RefreshSelectableLevels()
    {
        cachedLevels = ContentContainerEditor.FindAssetsByType<SelectableLevel>().ToList();
        Debug.Log($"[ DawnLib.Editor ] BeeHiveVisualiser refreshed SelectableLevel cache. Count: {cachedLevels.Count}");
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!IsEnabled)
            return;

        SelectableLevel? currentLevel = null;
        foreach (SelectableLevel level in cachedLevels)
        {
            if (level.sceneName == SceneManager.GetActiveScene().name)
            {
                currentLevel = level;
                break;
            }
        }

        if (currentLevel == null)
        {
            return;
        }

        bool contains = false;
        foreach (SpawnableEnemyWithRarity spawnableEnemyWithRarity in currentLevel.DaytimeEnemies)
        {
            if (spawnableEnemyWithRarity.enemyType == null)
                continue;

            if (spawnableEnemyWithRarity.enemyType.enemyName == "Red Locust Bees")
            {
                contains = true;
                break;
            }
        }

        if (!contains)
        {
            return;
        }

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        Handles.color = Color.yellow;

        DrawWireSphere(SphereCenter, SphereRadius);
    }

    private static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }

    public static IReadOnlyList<SelectableLevel> CachedLevels => cachedLevels;
}