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

    private static List<SelectableLevel> cachedLevels = new();

    static BeeHiveVisualiser()
    {
        RefreshSelectableLevels();

        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.projectChanged += OnProjectChanged;
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

        // Example usage of cached levels:
        // foreach (SelectableLevel level in cachedLevels)
        // {
        //     if (level == null)
        //         continue;
        //
        //     Debug.Log(level.name);
        // }
    }

    private static void DrawWireSphere(Vector3 center, float radius)
    {
        Handles.DrawWireDisc(center, Vector3.up, radius);
        Handles.DrawWireDisc(center, Vector3.right, radius);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
    }

    public static IReadOnlyList<SelectableLevel> CachedLevels => cachedLevels;
}