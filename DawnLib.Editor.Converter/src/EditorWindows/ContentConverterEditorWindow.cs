using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dusk;
using LethalLevelLoader;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor.EditorWindows;
public class DuskContentConverter : EditorWindow
{
    private string modName = string.Empty;
    private string authorName = string.Empty;
    [Tooltip("Version format: Major.Minor.Patches")]
    private string version_number = string.Empty; // maybe make a custom class for this so it forces a format

    [MenuItem("DawnLib/Dusk/Content Converter")]
    private static void Open()
    {
        GetWindow<DuskContentConverter>("Dusk Content Converter");
    }

    public static IEnumerable<T> FindAssetsByType<T>() where T : Object
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T)}");
        foreach (var t in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(t);
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                yield return asset;
            }
        }
    }

    private void OnGUI()
    {
        modName = EditorGUILayout.TextField("Mod Name", modName);
        authorName = EditorGUILayout.TextField("Author Name", authorName);
        version_number = EditorGUILayout.TextField("Version Number", version_number);
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(authorName) || string.IsNullOrEmpty(version_number) || string.IsNullOrEmpty(modName)))
        {
            if (GUILayout.Button("Create folders → Port Content → Succeed"))
            {
                PortContent();
            }
        }
    }

    private void CopyAsset(Object asset, string destFolder)
    {
        string sourcePath = AssetDatabase.GetAssetPath(asset);
        string fileName = Path.GetFileName(sourcePath);
        string destPath = Path.Combine(destFolder, fileName);

        if (!AssetDatabase.CopyAsset(sourcePath, destPath))
        {
            Debug.LogWarning($"Failed to copy {fileName} to {destFolder}");
        }
    }

    private void CreateItemDefinition(Item item, string registryPath)
    {
        DuskItemDefinition definition = ScriptableObject.CreateInstance<DuskItemDefinition>();
        definition.Item = item;

        string savePath = Path.Combine(registryPath, $"{item.itemName.Replace(" ", "")}DuskItemDefinition.asset");
        AssetDatabase.CreateAsset(definition, savePath);
        AssetDatabase.SaveAssets();
    }

    public void PortContent()
    {
        CreateAppropriateFolders(out string modPath);
        CreateModInfoAndContainerAssets(modPath);
        HandleExtendedItems(modPath);
        // HandleExtendedEnemies();
        // Find the approrpriate folder in the path Assets/LethalCompany/Mods/plugins/ModName where ModName is a folder we create with this tool with the mod's name
        // inside the ModName folder we need to create the following folders: AssetBundles, Dependencies, OutsideHazards, InsideHazards, Items, Enemies, EntitySkins, Moons, Interiors, Unlockables, Weathers
        // Then we need to look for all instances of the SO ExtendedItem, grab the `Item` reference inside of it and make a folder for that item in the Items folder using `Item.itemName` but getting rid of all spaces in the name.
        // for that item's folder we need to make multiple folders: Models, Textures, Materials, Sounds, Animations and then a Registry folder.
        // iside the models folder try to find the item's fbx or model by going into Item.itemPrefab and looking for the models inside of that prefab and placing them there.
        // for the materials and textures it should be a super similar process, same with sounds and animations, keep in mind for all of these tasks we need to copy and paste rather than move, this is incase parts of the process breaks or isn't correct.
        // then inside of the Registry, make a DuskItemDefinition SO, place the item inside of the .Item field of that SO, leave it as is for now for the other fields
        // then for all of the other SOs we need to do the same but with the proper folder names like ExtendedEnemy.EnemyType.enemyName and ExtendedEnemy.EnemyType.enemyPrefab
        // for now let's only do this porting for enemies and items.
        // Also inside of the ModName folder you need to create a ContentContainer SO and a ModInformation SO, fill in the author name, mod name and version in the ModInformation SO.
        // leave ContentContainer as is, empty.
    }

    private void CreateAppropriateFolders(out string modPath)
    {
        string basePath = "Assets/LethalCompany/Mods/plugins";
        modPath = Path.Combine(basePath, modName.Replace(" ", ""));

        if (!AssetDatabase.IsValidFolder(basePath))
        {
            Debug.LogError($"Base path does not exist: {basePath}");
            return;
        }

        if (!AssetDatabase.IsValidFolder(modPath))
        {
            AssetDatabase.CreateFolder(basePath, modName);
        }

        // Folder structure for different content
        string[] subFolders =
        [
            "AssetBundles", "Dependencies", "OutsideHazards", "InsideHazards", "Items",
            "Enemies", "EntitySkins", "Moons", "Interiors", "Unlockables", "Weathers"
        ];

        foreach (string folder in subFolders)
        {
            string fullPath = Path.Combine(modPath, folder);
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(modPath, folder);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created base mod folder structure at: {modPath}");
    }

    private void HandleExtendedItems(string modPath)
    {
        // Find all ExtendedItem assets
        ExtendedItem[] extendedItems = FindAssetsByType<ExtendedItem>().ToArray();
        foreach (ExtendedItem extendedItem in extendedItems)
        {
            if (extendedItem.Item == null)
                continue;

            string itemName = extendedItem.Item.itemName.Replace(" ", "");
            string itemFolder = Path.Combine(modPath, "Items", itemName);
            if (!AssetDatabase.IsValidFolder(itemFolder))
            {
                AssetDatabase.CreateFolder(Path.Combine(modPath, "Items"), itemName);
            }

            // Create subfolders
            string[] itemSubFolders = { "Models", "Textures", "Materials", "Sounds", "Animations", "Registry" };
            foreach (string sub in itemSubFolders)
            {
                string subPath = Path.Combine(itemFolder, sub);
                if (!AssetDatabase.IsValidFolder(subPath))
                {
                    AssetDatabase.CreateFolder(itemFolder, sub);
                }
            }

            PrefabAssetExtractor.ExtractPrefabAssets(extendedItem.Item.spawnPrefab, itemFolder);
            CreateItemDefinition(extendedItem.Item, Path.Combine(itemFolder, "Registry"));
        }
    }

    private void CreateModInfoAndContainerAssets(string modPath)
    {
        // Mod Information
        DuskModInformation modInfo = ScriptableObject.CreateInstance<DuskModInformation>();
        modInfo.ModName = modName;
        modInfo.AuthorName = authorName;
        modInfo.Version = version_number;
        AssetDatabase.CreateAsset(modInfo, Path.Combine(modPath, "ModInformation.asset"));

        // Content Container
        ContentContainer container = ScriptableObject.CreateInstance<ContentContainer>();
        AssetDatabase.CreateAsset(container, Path.Combine(modPath, "ContentContainer.asset"));

        AssetDatabase.SaveAssets();
    }
}
