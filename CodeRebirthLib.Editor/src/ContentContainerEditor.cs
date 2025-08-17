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
		return NamespacedKeyRegex.Replace(input.Replace(" ", ""), string.Empty);
	}

	private static string ClassNameFromNamespace(string ns, string suffix)
	{
		var words = ns.Split('_');
		for (int i = 0; i < words.Length; i++)
			words[i] = words[i].Trim().ToCapitalized();

		return string.Join("", words) + suffix;
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

			// className -> { "__type": "...", <CSharpName>:<NamespacedKey> }
			Dictionary<string, Dictionary<string, string>> definitionsDict = new();

			void Build<TDef>(IEnumerable<TDef> defs, string suffix, string typeTag, Func<TDef, string> getEntityName, Func<TDef, NamespacedKey> getKey) where TDef : CRMContentDefinition
			{
				foreach (var def in defs)
				{
					Debug.Log($"Checking definition: {def.name}");

					var className = ClassNameFromNamespace(getKey(def).Namespace, suffix);
					if (!definitionsDict.TryGetValue(className, out var bucket))
						definitionsDict[className] = bucket = new Dictionary<string, string> { { "__type", typeTag } };

					var csharpName = NormalizeNamespacedKey(getEntityName(def));
					var nsKey = getKey(def).ToString();

					bucket[csharpName] = nsKey;

					Debug.Log($"It has className: {className}, with C# name: {csharpName}, with NamespacedKey: {nsKey}");
				}
			}

			Build(enemies,	 "EnemyKeys",		"CREnemyInfo",		d => d.EntityNameReference, d => d.Key);
			Build(weathers,	"WeatherKeys",	  "CRWeatherInfo",	  d => d.EntityNameReference, d => d.Key);
			Build(unlockables, "UnlockableItemKeys","CRUnlockableItemInfo", d => d.EntityNameReference, d => d.Key);
			Build(items,	   "ItemKeys",		 "CRItemInfo",		 d => d.EntityNameReference, d => d.Key);
			Build(mapObjects,  "MapObjectKeys",	"CRMapObjectInfo",	d => d.EntityNameReference, d => d.Key);

			string text = JsonConvert.SerializeObject(definitionsDict, Formatting.Indented);
			string outputPath = EditorUtility.SaveFilePanel("NamespacedKeys", Application.dataPath, "namespaced_keys", "json");
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

			void BuildVanilla<T>(IEnumerable<T> src, string className, string typeTag, Func<T, string> nameGetter, Func<T, bool>? includePredicate = null)
			{
				if (!definitionsDict.TryGetValue(className, out var bucket))
					definitionsDict[className] = bucket = new Dictionary<string, string> { { "__type", typeTag } };

				foreach (var it in src)
				{
					if (includePredicate != null && !includePredicate(it))
						continue;

					string displayName = nameGetter(it);
					Debug.Log($"Checking {className.Replace("Keys","")}: {displayName}");

					string csharpName = NormalizeNamespacedKey(displayName);
					string nsKey = "lethal_company:" + NormalizeNamespacedKey(displayName.ToLowerInvariant().Replace(" ", "_"));

					bucket[csharpName] = nsKey;

					Debug.Log($"It has className: {className}, with C# name: {csharpName}, with NamespacedKey: {nsKey}");
				}
			}

			BuildVanilla(enemies, "EnemyKeys", "CREnemyInfo",
				enemyType => enemyType.enemyName,
				enemyType => !enemyType.name.Contains("Obj", StringComparison.InvariantCultureIgnoreCase));

			BuildVanilla(unlockablesList.unlockables, "UnlockableItemKeys", "CRUnlockableItemInfo", u => u.unlockableName);

			BuildVanilla(items, "ItemKeys", "CRItemInfo",
				item => item.itemName,
				item => !item.name.Contains("Obj", StringComparison.InvariantCultureIgnoreCase));

			BuildVanilla(levels, "MoonKeys", "CRMoonInfo", l => l.PlanetName);
			BuildVanilla(dungeons, "DungeonKeys", "CRDungeonInfo", d => d.name);

			string text = JsonConvert.SerializeObject(definitionsDict, Formatting.Indented);
			string outputPath = EditorUtility.SaveFilePanel("VanillaNamespacedKeys", Application.dataPath, "vanilla_namespaced_keys", "json");
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

	public static void DoMigrations<TEntity, TDef, TRef>(AssetBundleData bundleData, List<TEntity> entityDataList, List<TDef> definitions, Func<TRef> newCallback)
		where TEntity : EntityData<TRef>
		where TDef : CRMContentDefinition
		where TRef : CRMContentReference, new()
	{
		foreach (TEntity data in entityDataList)
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