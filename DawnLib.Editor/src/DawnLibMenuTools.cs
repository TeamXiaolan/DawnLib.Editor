using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor;

public static class DawnMenuTools
{
    [MenuItem("DawnLib/Blank SO's")]
    private static void CreateAllBlankSOs()
    {
        // Create a folder in Assets
        string folderPath = "Assets/DawnLib/BlankSOs";
        if (!AssetDatabase.IsValidFolder("Assets/DawnLib"))
        {
            AssetDatabase.CreateFolder("Assets", "DawnLib");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib", "BlankSOs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs/Items"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib/BlankSOs", "Items");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs/EnemyTypes"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib/BlankSOs", "EnemyTypes");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs/IndoorMapHazardTypes"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib/BlankSOs", "IndoorMapHazardTypes");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs/SpawnableOutsideObjects"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib/BlankSOs", "SpawnableOutsideObjects");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs/LevelAmbienceLibraries"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib/BlankSOs", "LevelAmbienceLibraries");
        }

        if (!AssetDatabase.IsValidFolder("Assets/DawnLib/BlankSOs/ReverbPresets"))
        {
            AssetDatabase.CreateFolder("Assets/DawnLib/BlankSOs", "ReverbPresets");
        }

        Item[] items = ContentContainerEditor.FindAssetsByType<Item>().Where(x => AssetDatabase.GetAssetPath(x).Contains("Game")).Where(x => x.spawnPrefab != null).ToArray();
        EnemyType[] enemyTypes = ContentContainerEditor.FindAssetsByType<EnemyType>().Where(x => AssetDatabase.GetAssetPath(x).Contains("Game")).Where(x => x.enemyPrefab != null).ToArray();
        IndoorMapHazardType[] indoorMapHazardTypes = ContentContainerEditor.FindAssetsByType<IndoorMapHazardType>().Where(x => AssetDatabase.GetAssetPath(x).Contains("Game")).Where(x => x.prefabToSpawn != null).ToArray();
        SpawnableOutsideObject[] spawnableOutsideObjects = ContentContainerEditor.FindAssetsByType<SpawnableOutsideObject>().Where(x => AssetDatabase.GetAssetPath(x).Contains("Game")).Where(x => x.prefabToSpawn != null).ToArray();
        LevelAmbienceLibrary[] levelAmbienceLibraries = ContentContainerEditor.FindAssetsByType<LevelAmbienceLibrary>().Where(x => AssetDatabase.GetAssetPath(x).Contains("Game")).ToArray();
        ReverbPreset[] reverbPresets = ContentContainerEditor.FindAssetsByType<ReverbPreset>().Where(x => AssetDatabase.GetAssetPath(x).Contains("Game")).ToArray();

        AssetDatabase.DeleteAssets(AssetDatabase.GetAllAssetPaths().Where(x => x.Contains("BlankSOs/") && x.Contains(".asset")).ToArray(), new());
        foreach (Item item in items)
        {
            string itemFolder = $"{folderPath}/Items/{item.name}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(item), itemFolder))
            {
                Debug.Log($"Failed to copy {AssetDatabase.GetAssetPath(item)} to {itemFolder}");
                continue;
            }

            Item copy = AssetDatabase.LoadAssetAtPath<Item>(itemFolder);
            ClearItem(copy);
        }

        foreach (EnemyType enemyType in enemyTypes)
        {
            string enemyTypeFolder = $"{folderPath}/EnemyTypes/{enemyType.name}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(enemyType), enemyTypeFolder))
            {
                Debug.Log($"Failed to copy {AssetDatabase.GetAssetPath(enemyType)} to {enemyTypeFolder}");
                continue;
            }

            EnemyType copy = AssetDatabase.LoadAssetAtPath<EnemyType>(enemyTypeFolder);
            ClearEnemy(copy);
        }

        foreach (IndoorMapHazardType indoorMapHazardType in indoorMapHazardTypes)
        {
            string indoorMapHazardTypeFolder = $"{folderPath}/IndoorMapHazardTypes/{indoorMapHazardType.name}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(indoorMapHazardType), indoorMapHazardTypeFolder))
            {
                Debug.Log($"Failed to copy {AssetDatabase.GetAssetPath(indoorMapHazardType)} to {indoorMapHazardTypeFolder}");
                continue;
            }

            IndoorMapHazardType copy = AssetDatabase.LoadAssetAtPath<IndoorMapHazardType>(indoorMapHazardTypeFolder);
            ClearIndoorMapHazardType(copy);
        }

        foreach (SpawnableOutsideObject spawnableOutsideObject in spawnableOutsideObjects)
        {
            string spawnableOutsideObjectFolder = $"{folderPath}/SpawnableOutsideObjects/{spawnableOutsideObject.name}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(spawnableOutsideObject), spawnableOutsideObjectFolder))
            {
                Debug.Log($"Failed to copy {AssetDatabase.GetAssetPath(spawnableOutsideObject)} to {spawnableOutsideObjectFolder}");
                continue;
            }

            SpawnableOutsideObject copy = AssetDatabase.LoadAssetAtPath<SpawnableOutsideObject>(spawnableOutsideObjectFolder);
            ClearSpawnableOutsideObject(copy);
        }

        foreach (LevelAmbienceLibrary levelAmbienceLibrary in levelAmbienceLibraries)
        {
            string levelAmbienceLibraryFolder = $"{folderPath}/LevelAmbienceLibraries/{levelAmbienceLibrary.name}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(levelAmbienceLibrary), levelAmbienceLibraryFolder))
            {
                Debug.Log($"Failed to copy {AssetDatabase.GetAssetPath(levelAmbienceLibrary)} to {levelAmbienceLibraryFolder}");
                continue;
            }

            LevelAmbienceLibrary copy = AssetDatabase.LoadAssetAtPath<LevelAmbienceLibrary>(levelAmbienceLibraryFolder);
            ClearLevelAmbienceLibrary(copy);
        }

        foreach (ReverbPreset reverbPreset in reverbPresets)
        {
            string reverbPresetFolder = $"{folderPath}/ReverbPresets/{reverbPreset.name}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(reverbPreset), reverbPresetFolder))
            {
                Debug.Log($"Failed to copy {AssetDatabase.GetAssetPath(reverbPreset)} to {reverbPresetFolder}");
                continue;
            }
        }
    }

    private static void ClearLevelAmbienceLibrary(LevelAmbienceLibrary copy)
    {
        copy.insanityMusicAudios = [];
        copy.insideAmbience = [];
        copy.insideAmbienceInsanity = [];
        copy.shipAmbience = [];
        copy.shipAmbienceInsanity = [];
        copy.outsideAmbience = [];
        copy.outsideAmbienceInsanity = [];

        EditorUtility.SetDirty(copy);
    }

    private static void ClearSpawnableOutsideObject(SpawnableOutsideObject copy)
    {
        copy.prefabToSpawn = null;

        EditorUtility.SetDirty(copy);
    }

    private static void ClearIndoorMapHazardType(IndoorMapHazardType copy)
    {
        copy.prefabToSpawn = null;

        EditorUtility.SetDirty(copy);
    }

    private static void ClearEnemy(EnemyType copy)
    {
        copy.enemyPrefab = null;
        copy.nestSpawnPrefab = null;

        copy.hitBodySFX = null;
        copy.hitEnemyVoiceSFX = null;
        copy.deathSFX = null;
        copy.stunSFX = null;

        if (copy.miscAnimations != null)
        {
            foreach (var animation in copy.miscAnimations)
            {
                animation.AnimVoiceclip = null;
            }
        }

        copy.audioClips = [];

        EditorUtility.SetDirty(copy);
    }

    private static void ClearItem(Item copy)
    {
        copy.spawnPositionTypes = [];

        copy.spawnPrefab = null;

        copy.itemIcon = null;

        copy.grabSFX = null;
        copy.dropSFX = null;
        copy.pocketSFX = null;
        copy.throwSFX = null;

        copy.meshVariants = [];
        copy.materialVariants = [];

        copy.clinkAudios = [];

        EditorUtility.SetDirty(copy);
    }
}