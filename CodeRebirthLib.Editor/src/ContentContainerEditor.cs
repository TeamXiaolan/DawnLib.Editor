using System;
using System.Collections.Generic;
using System.Linq;
using CodeRebirthLib.CRMod;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodeRebirthLib.Editor;

[CustomEditor(typeof(ContentContainer))]
public class ContentContainerEditor : UnityEditor.Editor
{
	static Dictionary<AssetBundleData, List<string>> fails = [];
	
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();

		ContentContainer content = (ContentContainer)target;
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

	public static void DoMigrations<TEntity, TDef, TRef>(AssetBundleData bundleData, List<TEntity> entityDataList, List<TDef> definitions, Func<TRef> newCallback) where TEntity : EntityData<TRef> where TDef : CRMContentDefinition where TRef : CRMContentReference 
	{
		foreach(TEntity data in entityDataList) 
		{
			if(data.Key != null) 
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