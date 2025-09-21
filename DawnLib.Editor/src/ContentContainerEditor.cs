using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dawn.Utils;
using DunGen;
using DunGen.Graph;
using Dusk;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Dawn.Editor;

[CustomEditor(typeof(ContentContainer))]
public class ContentContainerEditor : UnityEditor.Editor
{
	private static ContentContainer? _contentContainer = null;
	private static ContentContainer? ContentContainer
	{
		get
		{
			if (_contentContainer == null)
			{
				_contentContainer = FindAssetsByType<ContentContainer>().FirstOrDefault();
			}

			return _contentContainer;
		}
	}

	private static readonly Regex NamespacedKeyRegex = new(@"[?!.\n\t""`\[\]'-]");

	private static readonly Dictionary<char, string> NumberWords = new()
	{
		{ '0', "Zero" },
		{ '1', "One" },
		{ '2', "Two" },
		{ '3', "Three" },
		{ '4', "Four" },
		{ '5', "Five" },
		{ '6', "Six" },
		{ '7', "Seven" },
		{ '8', "Eight" },
		{ '9', "Nine" },
	};

	internal static string NormalizeNamespacedKey(string input, bool CSharpName)
	{
		if (string.IsNullOrEmpty(input))
			return string.Empty;

		string cleanedString = NamespacedKeyRegex.Replace(input, string.Empty);

		StringBuilder cleanBuilder = new StringBuilder(cleanedString.Length);
		bool foundAllBeginningDigits = false;
		foreach (char character in cleanedString)
		{
			if (!foundAllBeginningDigits && (char.IsDigit(character) || character == ' '))
			{
				continue;
			}
			foundAllBeginningDigits = true;
			cleanBuilder.Append(character);
		}

		StringBuilder actualWordBuilder = new StringBuilder(cleanBuilder.Length);
		foreach (char character in cleanBuilder.ToString())
		{
			if (NumberWords.TryGetValue(character, out var word))
				actualWordBuilder.Append(word);
			else
				actualWordBuilder.Append(character);
		}

		string result = actualWordBuilder.ToString();
		if (CSharpName)
		{
			result = result.Replace(" ", "");
			result = result.Replace("_", "");
			result = result.ToCapitalized();
		}
		else
		{
			result = result.ToLowerInvariant().Replace(" ", "_");
		}
		return result.ToString();
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
		EditorGUILayout.LabelField("[TIP] Hover over a red field to get more information about what's wrong.");
		base.OnInspectorGUI();
		_contentContainer = (ContentContainer)target;

		if (GUILayout.Button("Generate 'namespaced_keys.json'"))
		{
			List<DuskEnemyDefinition> enemies = FindAssetsByType<DuskEnemyDefinition>().ToList();
			List<DuskWeatherDefinition> weathers = FindAssetsByType<DuskWeatherDefinition>().ToList();
			List<DuskUnlockableDefinition> unlockables = FindAssetsByType<DuskUnlockableDefinition>().ToList();
			List<DuskItemDefinition> items = FindAssetsByType<DuskItemDefinition>().ToList();
			List<DuskMapObjectDefinition> mapObjects = FindAssetsByType<DuskMapObjectDefinition>().ToList();
			List<DuskAchievementDefinition> achievements = FindAssetsByType<DuskAchievementDefinition>().ToList();
			List<DuskAdditionalTilesDefinition> additionalTiles = FindAssetsByType<DuskAdditionalTilesDefinition>().ToList();
			List<DuskVehicleDefinition> vehicles = FindAssetsByType<DuskVehicleDefinition>().ToList();
			List<DuskEntityReplacementDefinition> entityReplacements = FindAssetsByType<DuskEntityReplacementDefinition>().ToList();

			// className -> { "__type": "...", <CSharpName>:<NamespacedKey> }
			Dictionary<string, Dictionary<string, string>> definitionsDict = new();

			void Build<TDef>(IEnumerable<TDef> defs, string suffix, string typeTag, Func<TDef, string> getEntityName, Func<TDef, NamespacedKey> getKey) where TDef : DuskContentDefinition
			{
				foreach (TDef def in defs)
				{
					Debug.Log($"Checking definition: {def.name}");

					string className = ClassNameFromNamespace(getKey(def).Namespace, suffix);
					if (!definitionsDict.TryGetValue(className, out var bucket))
						definitionsDict[className] = bucket = new Dictionary<string, string> { { "__type", typeTag } };

					string csharpName = NormalizeNamespacedKey(getEntityName(def), true);
					string nsKey = getKey(def).ToString();

					bucket[csharpName] = nsKey;

					Debug.Log($"It has className: {className}, with C# name: {csharpName}, with NamespacedKey: {nsKey}");
				}
			}

			Build(enemies, "EnemyKeys", "DawnEnemyInfo", d => d.EntityNameReference, d => d.Key);
			Build(weathers, "WeatherKeys", "DawnWeatherEffectInfo", d => d.EntityNameReference, d => d.Key);
			Build(unlockables, "UnlockableItemKeys", "DawnUnlockableItemInfo", d => d.EntityNameReference, d => d.Key);
			Build(items, "ItemKeys", "DawnItemInfo", d => d.EntityNameReference, d => d.Key);
			Build(mapObjects, "MapObjectKeys", "DawnMapObjectInfo", d => d.EntityNameReference, d => d.Key);
			Build(additionalTiles, "AdditionalTilesKeys", "DawnAdditionalTilesInfo", d => d.EntityNameReference, d => d.Key);
			Build(achievements, "AchievementKeys", "DawnLib.Dusk.DuskAchievementDefinition", d => d.EntityNameReference, d => d.Key);
			Build(vehicles, "VehicleKeys", "DawnVehicleInfo", d => d.EntityNameReference, d => d.Key);
			Build(entityReplacements, "EntityReplacementKeys", "DawnLib.Dusk.DustEntityReplacementDefinition", d => d.EntityNameReference, d => d.Key);

			string text = JsonConvert.SerializeObject(definitionsDict, Formatting.Indented);
			string outputPath = EditorUtility.SaveFilePanel("NamespacedKeys", Application.dataPath, "namespaced_keys", "json");
			File.WriteAllText(outputPath, text);
		}

		if (GUILayout.Button("Migrate old configs in ContentContainer to the ContentDefinitions"))
		{
			foreach (EnemyData enemyData in ContentContainer.assetBundles.SelectMany(bundle => bundle.enemies))
			{
				DuskEnemyDefinition enemyDefinition = AssetDatabase.LoadAssetAtPath<DuskEnemyDefinition>(AssetDatabase.GUIDToAssetPath(enemyData._reference.assetGUID));
				enemyDefinition.MoonSpawnWeights = enemyData.moonSpawnWeights;
				enemyDefinition.InteriorSpawnWeights = enemyData.interiorSpawnWeights;
				enemyDefinition.WeatherSpawnWeights = enemyData.weatherSpawnWeights;
				enemyDefinition.GenerateSpawnWeightsConfig = enemyData.generateSpawnWeightsConfig;
				enemyDefinition.EnemyType.PowerLevel = enemyData.powerLevel;
				enemyDefinition.EnemyType.MaxCount = enemyData.maxSpawnCount;
				EditorUtility.SetDirty(enemyDefinition);
			}

			foreach (WeatherData weatherData in ContentContainer.assetBundles.SelectMany(bundle => bundle.weathers))
			{
				DuskWeatherDefinition weatherDefinition = AssetDatabase.LoadAssetAtPath<DuskWeatherDefinition>(AssetDatabase.GUIDToAssetPath(weatherData._reference.assetGUID));
				weatherDefinition.SpawnWeight = weatherData.spawnWeight;
				weatherDefinition.ScrapValueMultiplier = weatherData.scrapValueMultiplier;
				weatherDefinition.ScrapAmountMultiplier = weatherData.scrapMultiplier;
				weatherDefinition.IsExclude = weatherData.isExclude;
				weatherDefinition.CreateExcludeConfig = weatherData.createExcludeConfig;
				weatherDefinition.ExcludeOrIncludeList = weatherData.excludeOrIncludeList;
				EditorUtility.SetDirty(weatherDefinition);
			}

			foreach (UnlockableData unlockableData in ContentContainer.assetBundles.SelectMany(bundle => bundle.unlockables))
			{
				DuskUnlockableDefinition unlockableDefinition = AssetDatabase.LoadAssetAtPath<DuskUnlockableDefinition>(AssetDatabase.GUIDToAssetPath(unlockableData._reference.assetGUID));
				unlockableDefinition.IsShipUpgrade = unlockableData.isShipUpgrade;
				unlockableDefinition.IsDecor = unlockableData.isDecor;
				unlockableDefinition.Cost = unlockableData.cost;
				unlockableDefinition.GenerateDisablePricingStrategyConfig = unlockableData.generateDisablePricingStrategyConfig;
				unlockableDefinition.GenerateDisableUnlockRequirementConfig = unlockableData.generateDisableUnlockRequirementConfig;
				EditorUtility.SetDirty(unlockableDefinition);
			}

			foreach (ItemData itemData in ContentContainer.assetBundles.SelectMany(bundle => bundle.items))
			{
				DuskItemDefinition itemDefinition = AssetDatabase.LoadAssetAtPath<DuskItemDefinition>(AssetDatabase.GUIDToAssetPath(itemData._reference.assetGUID));
				itemDefinition.Cost = itemData.cost;
				itemDefinition.MoonSpawnWeights = itemData.moonSpawnWeights;
				itemDefinition.WeatherSpawnWeights = itemData.weatherSpawnWeights;
				itemDefinition.InteriorSpawnWeights = itemData.interiorSpawnWeights;
				itemDefinition.GenerateDisableUnlockConfig = itemData.generateDisableUnlockConfig;
				itemDefinition.GenerateDisablePricingStrategyConfig = itemData.generateDisablePricingStrategyConfig;
				itemDefinition.IsScrap = itemData.isScrap;
				itemDefinition.IsShopItem = itemData.isShopItem;
				itemDefinition.GenerateSpawnWeightsConfig = itemData.generateSpawnWeightsConfig;
				itemDefinition.GenerateScrapConfig = itemData.generateScrapConfig;
				itemDefinition.GenerateShopItemConfig = itemData.generateShopItemConfig;
				EditorUtility.SetDirty(itemDefinition);
			}

			foreach (MapObjectData mapObjectData in ContentContainer.assetBundles.SelectMany(bundle => bundle.mapObjects))
			{
				DuskMapObjectDefinition mapObjectDefinition = AssetDatabase.LoadAssetAtPath<DuskMapObjectDefinition>(AssetDatabase.GUIDToAssetPath(mapObjectData._reference.assetGUID));
				mapObjectDefinition.IsInsideHazard = mapObjectData.isInsideHazard;
				mapObjectDefinition.CreateInsideHazardConfig = mapObjectData.createInsideHazardConfig;
				mapObjectDefinition.DefaultInsideCurveSpawnWeights = mapObjectData.defaultInsideCurveSpawnWeights;
				mapObjectDefinition.CreateInsideCurveSpawnWeightsConfig = mapObjectData.createInsideCurveSpawnWeightsConfig;
				mapObjectDefinition.IsOutsideHazard = mapObjectData.isOutsideHazard;
				mapObjectDefinition.CreateOutsideHazardConfig = mapObjectData.createOutsideHazardConfig;
				mapObjectDefinition.DefaultOutsideCurveSpawnWeights = mapObjectData.defaultOutsideCurveSpawnWeights;
				mapObjectDefinition.CreateOutsideCurveSpawnWeightsConfig = mapObjectData.createOutsideCurveSpawnWeightsConfig;
				EditorUtility.SetDirty(mapObjectDefinition);
			}
			AssetDatabase.SaveAssets();
		}

		if (GUILayout.Button("Generate 'vanilla_namespaced_keys.json' (Debug)"))
		{
			List<DungeonFlow> dungeons = FindAssetsByType<DungeonFlow>().ToList();
			List<TileSet> tilesets = FindAssetsByType<TileSet>().ToList();
			List<DungeonArchetype> archetypes = FindAssetsByType<DungeonArchetype>().ToList();
			List<SelectableLevel> levels = FindAssetsByType<SelectableLevel>().ToList();
			List<EnemyType> enemies = FindAssetsByType<EnemyType>().ToList();
			UnlockablesList unlockablesList = FindAssetsByType<UnlockablesList>().ToList().First();
			List<Item> items = FindAssetsByType<Item>().ToList();
			List<GameObject> mapObjects = levels.SelectMany(level => level.spawnableMapObjects.Select(m => m.prefabToSpawn).Concat(level.spawnableOutsideObjects.Select(o => o.spawnableObject.prefabToSpawn))).Distinct().ToList();

			Dictionary<string, Dictionary<string, string>> definitionsDict = new();

			void BuildVanilla<T>(IEnumerable<T> src, string className, string typeTag, Func<T, string> nameGetter, Func<T, Object?>? assetSelector = null, bool bypassCheck = false)
			{
				if (!definitionsDict.TryGetValue(className, out var bucket))
					definitionsDict[className] = bucket = new Dictionary<string, string> { { "__type", typeTag } };

				foreach (var it in src)
				{
					Object? asset = null;

					if (assetSelector != null)
					{
						asset = assetSelector(it);
					}
					else if (it is Object unityObj)
					{
						asset = unityObj;
					}

					if (asset == null && !bypassCheck)
						continue;

					if (!bypassCheck)
					{
						string assetPath = AssetDatabase.GetAssetPath(asset);
						if (string.IsNullOrEmpty(assetPath) || !assetPath.Contains("/Game/", StringComparison.InvariantCultureIgnoreCase))
							continue;
					}

					string displayName = nameGetter(it);
					Debug.Log($"Checking {className.Replace("Keys", "")}: {displayName}");

					string csharpName = string.Empty;
					if (it is TileSet tileSet)
					{
						csharpName = AdditionalTilesRegistrationHandler.FormatTileSetName(tileSet);
					}
					else if (it is DungeonArchetype dungeonArchetype)
					{
						csharpName = AdditionalTilesRegistrationHandler.FormatArchetypeName(dungeonArchetype);
					}
					else if (it is DungeonFlow dungeonFlow)
					{
						csharpName = AdditionalTilesRegistrationHandler.FormatFlowName(dungeonFlow);
					}
					else
					{
						csharpName = NormalizeNamespacedKey(displayName, true);
					}

					if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(csharpName))
						continue;

					string nsKey = "lethal_company:" + NormalizeNamespacedKey(displayName, false);

					bucket[csharpName] = nsKey;

					Debug.Log($"It has className: {className}, with C# name: {csharpName}, with NamespacedKey: {nsKey}");
				}
			}

			BuildVanilla(enemies, "EnemyKeys", "DawnEnemyInfo",
				enemyType => enemyType.enemyName,
				obj => obj);

			BuildVanilla(unlockablesList.unlockables, "UnlockableItemKeys", "DawnUnlockableItemInfo",
				u => u.unlockableName,
				obj => unlockablesList);

			BuildVanilla(items, "ItemKeys", "DawnItemInfo",
				item => item.itemName,
				obj => obj);

			BuildVanilla(levels, "MoonKeys", "DawnMoonInfo",
				l => l.PlanetName,
				obj => obj);

			BuildVanilla(dungeons, "DungeonKeys", "DawnDungeonInfo",
				d => d.name,
				obj => obj);

			BuildVanilla(tilesets, "DungeonTileSetKeys", "DawnTileSetInfo",
				t => t.name,
				obj => obj);

			BuildVanilla(archetypes, "DungeonArchetypeKeys", "DawnArchetypeInfo",
				a => a.name,
				obj => obj);

			BuildVanilla(mapObjects, "MapObjectKeys", "DawnMapObjectInfo",
				m => m.name,
				obj => obj);

			List<WeatherEffect> weathers = LoadSampleSceneRelayTimeOfDay().effects.ToList();

			BuildVanilla(weathers, "WeatherKeys", "DawnWeatherInfo",
				w => w.name,
				obj => null,
				true);

			string text = JsonConvert.SerializeObject(definitionsDict, Formatting.Indented);
			string outputPath = EditorUtility.SaveFilePanel("VanillaNamespacedKeys", Application.dataPath, "vanilla_namespaced_keys", "json");
			File.WriteAllText(outputPath, text);
		}
	}

	private TimeOfDay LoadSampleSceneRelayTimeOfDay()
	{
		SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();

		Scene scene = EditorSceneManager.OpenScene("Assets/LethalCompany/Game/Scenes/SampleSceneRelay.unity", OpenSceneMode.Additive);

		TimeOfDay timeOfDay = scene.GetRootGameObjects()
			.SelectMany(go => go.GetComponentsInChildren<TimeOfDay>(true))
			.First();

		EditorSceneManager.CloseScene(scene, true);
		if (setup != null && setup.Length > 0)
			EditorSceneManager.RestoreSceneManagerSetup(setup);

		return timeOfDay;
	}

	static void ClearConsole()
	{
		var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
		var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
		clearMethod.Invoke(null, null);
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