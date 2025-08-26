using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CodeRebirthLib.CRMod;
using CodeRebirthLib.Utils;
using DunGen.Graph;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CodeRebirthLib.Editor;

[CustomEditor(typeof(ContentContainer))]
public class ContentContainerEditor : UnityEditor.Editor
{
	static Dictionary<AssetBundleData, List<string>> fails = [];

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

		ContentContainer content = (ContentContainer)target;

		if (GUILayout.Button("Generate 'namespaced_keys.json'"))
		{
			List<CRMEnemyDefinition> enemies = FindAssetsByType<CRMEnemyDefinition>().ToList();
			List<CRMWeatherDefinition> weathers = FindAssetsByType<CRMWeatherDefinition>().ToList();
			List<CRMUnlockableDefinition> unlockables = FindAssetsByType<CRMUnlockableDefinition>().ToList();
			List<CRMItemDefinition> items = FindAssetsByType<CRMItemDefinition>().ToList();
			List<CRMMapObjectDefinition> mapObjects = FindAssetsByType<CRMMapObjectDefinition>().ToList();
			List<CRMAchievementDefinition> achievements = FindAssetsByType<CRMAchievementDefinition>().ToList();
			List<CRMAdditionalTilesDefinition> additionalTiles = FindAssetsByType<CRMAdditionalTilesDefinition>().ToList();

			// className -> { "__type": "...", <CSharpName>:<NamespacedKey> }
			Dictionary<string, Dictionary<string, string>> definitionsDict = new();

			void Build<TDef>(IEnumerable<TDef> defs, string suffix, string typeTag, Func<TDef, string> getEntityName, Func<TDef, NamespacedKey> getKey) where TDef : CRMContentDefinition
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

			Build(enemies, "EnemyKeys", "CREnemyInfo", d => d.EntityNameReference, d => d.Key);
			Build(weathers,	"WeatherKeys", "CRWeatherEffectInfo", d => d.EntityNameReference, d => d.Key);
			Build(unlockables, "UnlockableItemKeys","CRUnlockableItemInfo", d => d.EntityNameReference, d => d.Key);
			Build(items, "ItemKeys", "CRItemInfo", d => d.EntityNameReference, d => d.Key);
			Build(mapObjects, "MapObjectKeys", "CRMapObjectInfo", d => d.EntityNameReference, d => d.Key);
			Build(additionalTiles, "AdditionalTilesKeys", "CRAdditionalTilesInfo", d => d.EntityNameReference, d => d.Key);
			Build(achievements, "AchievementKeys", "CodeRebirthLib.CRMod.CRMAchievementDefinition", d => d.EntityNameReference, d => d.Key);

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
					Debug.Log($"Checking {className.Replace("Keys","")}: {displayName}");

					string csharpName = NormalizeNamespacedKey(displayName, true);
					if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(csharpName))
						continue;

					string nsKey = "lethal_company:" + NormalizeNamespacedKey(displayName, false);

					bucket[csharpName] = nsKey;

					Debug.Log($"It has className: {className}, with C# name: {csharpName}, with NamespacedKey: {nsKey}");
				}
			}

			BuildVanilla(enemies, "EnemyKeys", "CREnemyInfo",
				enemyType => enemyType.enemyName,
				obj => obj);

			BuildVanilla(unlockablesList.unlockables, "UnlockableItemKeys", "CRUnlockableItemInfo",
				u => u.unlockableName,
				obj => unlockablesList);

			BuildVanilla(items, "ItemKeys", "CRItemInfo",
				item => item.itemName,
				obj => obj);

			BuildVanilla(levels, "MoonKeys", "CRMoonInfo",
				l => l.PlanetName,
				obj => obj);

			BuildVanilla(dungeons, "DungeonKeys", "CRDungeonInfo",
				d => d.name,
				obj => obj);
			
			BuildVanilla(mapObjects, "MapObjectKeys", "CRMapObjectInfo",
				m => m.name,
				obj => obj);

			List<WeatherEffect> weathers = LoadSampleSceneRelayTimeOfDay().effects.ToList();

			BuildVanilla(weathers, "WeatherKeys", "CRWeatherInfo",
				w => w.name,
				obj => null,
				true); // there is no asset for this so idk just gonna use the unlockablesList one to fake it.

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