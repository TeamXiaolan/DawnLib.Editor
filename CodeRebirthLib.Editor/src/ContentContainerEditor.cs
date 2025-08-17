using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeRebirthLib.CRMod;
using CodeRebirthLib.Utils;
using DunGen.Graph;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodeRebirthLib.Editor;

[CustomEditor(typeof(ContentContainer))]
public class ContentContainerEditor : UnityEditor.Editor
{
	static Dictionary<AssetBundleData, List<string>> fails = [];

	private static readonly Regex NamespacedKeyRegex = new(@"[\n\t""`\[\]'-]");

    internal static string NormalizeNamespacedKey(string input)
    {
        // The regex pattern matches: newline, tab, double quote, backtick, apostrophe, dash, [ or ].
        return NamespacedKeyRegex.Replace(input.Replace(" ", ""), string.Empty);
    }

	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		ContentContainer content = (ContentContainer)target;

		if (GUILayout.Button("Generate 'namespaced_keys.json'"))
		{
			List<CRMEnemyDefinition> enemies = FindAssetsByType<CRMEnemyDefinition>().ToList();
			List<CRMWeatherDefinition> weathers = FindAssetsByType<CRMWeatherDefinition>().ToList();
			List<CRMUnlockableDefinition> unlockables = FindAssetsByType<CRMUnlockableDefinition>().ToList();
			List<CRMItemDefinition> items = FindAssetsByType<CRMItemDefinition>().ToList();
			List<CRMMapObjectDefinition> mapObjects = FindAssetsByType<CRMMapObjectDefinition>().ToList();

			Dictionary<string, Dictionary<string, string>> definitionsDict = new();

			foreach (CRMEnemyDefinition definition in enemies)
			{
				Debug.Log($"Checking definition: {definition.name}");

				string[] words = definition.Key.Namespace.Split('_');
				for (int i = 0; i < words.Length; i++)
				{
					words[i].Trim();
					words[i] = words[i].ToCapitalized();
				}
				string className = string.Join("", words);
				className += "EnemyKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CREnemyInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(definition.EntityNameReference)] = definition.Key.ToString();
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(definition.EntityNameReference)}, with NamespacedKey: {definition.Key.ToString()}");
			}

			foreach (CRMWeatherDefinition definition in weathers)
			{
				Debug.Log($"Checking definition: {definition.name}");

				string[] words = definition.Key.Namespace.Split('_');
				for (int i = 0; i < words.Length; i++)
				{
					words[i].Trim();
					words[i] = words[i].ToCapitalized();
				}
				string className = string.Join("", words);
				className += "WeatherKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRWeatherInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(definition.EntityNameReference)] = definition.Key.ToString();
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(definition.EntityNameReference)}, with NamespacedKey: {definition.Key.ToString()}");
			}

			foreach (CRMUnlockableDefinition definition in unlockables)
			{
				Debug.Log($"Checking definition: {definition.name}");

				string[] words = definition.Key.Namespace.Split('_');
				for (int i = 0; i < words.Length; i++)
				{
					words[i].Trim();
					words[i] = words[i].ToCapitalized();
				}
				string className = string.Join("", words);
				className += "UnlockableItemKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRUnlockableItemInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(definition.EntityNameReference)] = definition.Key.ToString();
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(definition.EntityNameReference)}, with NamespacedKey: {definition.Key.ToString()}");
			}

			foreach (CRMItemDefinition definition in items)
			{
				Debug.Log($"Checking definition: {definition.name}");

				string[] words = definition.Key.Namespace.Split('_');
				for (int i = 0; i < words.Length; i++)
				{
					words[i].Trim();
					words[i] = words[i].ToCapitalized();
				}
				string className = string.Join("", words);
				className += "ItemKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRItemInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(definition.EntityNameReference)] = definition.Key.ToString();
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(definition.EntityNameReference)}, with NamespacedKey: {definition.Key.ToString()}");
			}

			foreach (CRMMapObjectDefinition definition in mapObjects)
			{
				Debug.Log($"Checking definition: {definition.name}");

				string[] words = definition.Key.Namespace.Split('_');
				for (int i = 0; i < words.Length; i++)
				{
					words[i].Trim();
					words[i] = words[i].ToCapitalized();
				}
				string className = string.Join("", words);
				className += "MapObjectKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRMapObjectInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(definition.EntityNameReference)] = definition.Key.ToString();
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(definition.EntityNameReference)}, with NamespacedKey: {definition.Key.ToString()}");
			}
			string text = JsonConvert.SerializeObject(definitionsDict);
			string outputPath = EditorUtility.SaveFilePanel($"NamespacedKeys", Application.dataPath, "namespaced_keys", "json");
			File.WriteAllText(outputPath, text);
		}

		if (GUILayout.Button("Generate 'vanilla_namespaced_keys.json' (Debug)"))
		{
			List<DungeonFlow> dungeons = FindAssetsByType<DungeonFlow>().ToList();
			List<SelectableLevel> levels = FindAssetsByType<SelectableLevel>().ToList();
			List<EnemyType> enemies = FindAssetsByType<EnemyType>().ToList();
			UnlockablesList unlockablesList = FindAssetsByType<UnlockablesList>().ToList().First();
			List<Item> items = FindAssetsByType<Item>().ToList();

			Dictionary<string, Dictionary<string, string>> definitionsDict = new();

			foreach (EnemyType enemyType in enemies)
			{
				if (enemyType.name.Contains("Obj"))
					continue;

				Debug.Log($"Checking enemyType: {enemyType.enemyName}");

				string className = "EnemyKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CREnemyInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(enemyType.enemyName)] = "lethal_company:" + NormalizeNamespacedKey(enemyType.enemyName.ToLowerInvariant().Replace(" ", "_"));
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(enemyType.enemyName)}, with NamespacedKey: lethal_company:{NormalizeNamespacedKey(enemyType.enemyName.ToLowerInvariant().Replace(" ", "_"))}");
			}

			foreach (UnlockableItem unlockableItem in unlockablesList.unlockables)
			{
				Debug.Log($"Checking unlockableItem: {unlockableItem.unlockableName}");

				string className = "UnlockableItemKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRUnlockableItemInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(unlockableItem.unlockableName)] = "lethal_company:" + NormalizeNamespacedKey(unlockableItem.unlockableName.ToLowerInvariant().Replace(" ", "_"));
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(unlockableItem.unlockableName)}, with NamespacedKey: lethal_company:{NormalizeNamespacedKey(unlockableItem.unlockableName.ToLowerInvariant().Replace(" ", "_"))}");
			}

			foreach (Item item in items)
			{
				if (item.name.Contains("Obj"))
					continue;

				Debug.Log($"Checking Item: {item.itemName}");

				string className = "ItemKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRItemInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(item.itemName)] = "lethal_company:" + NormalizeNamespacedKey(item.itemName.ToLowerInvariant().Replace(" ", "_"));
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(item.itemName)}, with NamespacedKey: lethal_company:{NormalizeNamespacedKey(item.itemName.ToLowerInvariant().Replace(" ", "_"))}");
			}

			foreach (SelectableLevel level in levels)
			{
				Debug.Log($"Checking SelectableLevel: {level.PlanetName}");

				string className = "MoonKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRMoonInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(level.PlanetName)] = "lethal_company:" + NormalizeNamespacedKey(level.PlanetName.ToLowerInvariant().Replace(" ", "_"));
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(level.PlanetName)}, with NamespacedKey: lethal_company:{NormalizeNamespacedKey(level.PlanetName.ToLowerInvariant().Replace(" ", "_"))}");
			}

			foreach (DungeonFlow dungeon in dungeons)
			{
				Debug.Log($"Checking Dungeon: {dungeon.name}");

				string className = "DungeonKeys";

				if (!definitionsDict.ContainsKey(className))
				{
					definitionsDict[className] = new()
					{
						{ "__type", "CRDungeonInfo" }
					};
				}
				definitionsDict[className][NormalizeNamespacedKey(dungeon.name)] = "lethal_company:" + NormalizeNamespacedKey(dungeon.name.ToLowerInvariant().Replace(" ", "_"));
				Debug.Log($"It has className: {className}, with C# name: {NormalizeNamespacedKey(dungeon.name)}, with NamespacedKey: lethal_company:{NormalizeNamespacedKey(dungeon.name.ToLowerInvariant().Replace(" ", "_"))}");
			}

			string text = JsonConvert.SerializeObject(definitionsDict);
			string outputPath = EditorUtility.SaveFilePanel($"NamespacedKeys", Application.dataPath, "namespaced_keys", "json");
			File.WriteAllText(outputPath, text);
		}

		if (GUILayout.Button("Migrate entityName -> references"))
		{
			Debug.Log("beginning migration");
			fails.Clear();
			ClearConsole();

			List<CRMEnemyDefinition> enemies = FindAssetsByType<CRMEnemyDefinition>().ToList();
			List<CRMWeatherDefinition> weathers = FindAssetsByType<CRMWeatherDefinition>().ToList();
			List<CRMUnlockableDefinition> unlockables = FindAssetsByType<CRMUnlockableDefinition>().ToList();
			List<CRMItemDefinition> items = FindAssetsByType<CRMItemDefinition>().ToList();
			List<CRMMapObjectDefinition> mapObjects = FindAssetsByType<CRMMapObjectDefinition>().ToList();
			int totalBundleData = content.assetBundles.Count;
			int completedBundles = 0;
			foreach (AssetBundleData bundleData in content.assetBundles)
			{
				EditorUtility.DisplayProgressBar("Migrating", $"{completedBundles}/{totalBundleData}: {bundleData.configName}", (float)completedBundles / totalBundleData);
				Debug.Log($"migrating: {bundleData.configName}");
				DoMigrations(bundleData, bundleData.enemies, enemies, () => new CRMEnemyReference());
				DoMigrations(bundleData, bundleData.unlockables, unlockables, () => new CRMUnlockableReference());
				DoMigrations(bundleData, bundleData.weathers, weathers, () => new CRMWeatherReference());
				DoMigrations(bundleData, bundleData.items, items, () => new CRMItemReference());
				DoMigrations(bundleData, bundleData.mapObjects, mapObjects, () => new CRMMapObjectReference());
				completedBundles++;
			}

			foreach ((AssetBundleData bundleData, List<string> fails) in fails)
			{
				Debug.LogError($"Bundle '{bundleData.assetBundleName}' has {fails.Count} error(s) that will have to be referenced manually: {string.Join(", ", fails)}");
			}

			EditorUtility.ClearProgressBar();
			EditorUtility.SetDirty(target);
			serializedObject.ApplyModifiedProperties();
			serializedObject.Update();
		}
	}

	static void ClearConsole()
	{
		var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
		var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
		clearMethod.Invoke(null, null);
	}

	public static void DoMigrations<TEntity, TDef, TRef>(AssetBundleData bundleData, List<TEntity> entityDataList, List<TDef> definitions, Func<TRef> newCallback) where TEntity : EntityData<TRef> where TDef : CRMContentDefinition where TRef : CRMContentReference, new()
	{
		foreach(TEntity data in entityDataList) 
		{
			if (!string.IsNullOrEmpty(data.Key.Key)) 
				continue; // already migrated

			if (!TryGetMigration(bundleData, definitions, data, out string guid, out TDef def))
				continue;
			
			TRef reference = newCallback();
			reference.Key = def.Key;
			reference.assetGUID = guid;
			data._reference = reference;
		}
	}
	
	public static bool TryGetMigration<TDef, TEntity>(AssetBundleData bundleData, List<TDef> definitions, TEntity data, out string guid, out TDef definition) where TDef : CRMContentDefinition where TEntity : EntityData
	{
		guid = "";
		definition = null;
		try
		{
			definition = definitions.First(it => string.Equals(it.EntityNameReference, data.entityName, StringComparison.InvariantCultureIgnoreCase));
		}
		catch (InvalidOperationException exception)
		{
			if (!fails.TryGetValue(bundleData, out List<string> failList))
			{
				failList = [];
			}
			failList.Add($"{data.entityName} ({typeof(TEntity).Name})");
			fails[bundleData] = failList;
			return false;
		}

		string path = AssetDatabase.GetAssetPath(definition);
		guid = AssetDatabase.GUIDFromAssetPath(path).ToString();
		return true;
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
}