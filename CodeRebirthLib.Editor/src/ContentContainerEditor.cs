using System;
using System.Collections.Generic;
using System.Linq;
using CodeRebirthLib.AssetManagement;
using CodeRebirthLib.ContentManagement;
using CodeRebirthLib.ContentManagement.Enemies;
using CodeRebirthLib.ContentManagement.Items;
using CodeRebirthLib.ContentManagement.MapObjects;
using CodeRebirthLib.ContentManagement.Unlockables;
using CodeRebirthLib.ContentManagement.Weathers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodeRebirthLib.Editor;

[CustomEditor(typeof(ContentContainer))]
public class ContentContainerEditor : UnityEditor.Editor {
	public override void OnInspectorGUI() {
		base.OnInspectorGUI();

		ContentContainer content = (ContentContainer)target;
		if(GUILayout.Button("Migrate entityName -> references")) {
			Debug.Log("beginning migration");

			List<CREnemyDefinition> enemies = FindAssetsByType<CREnemyDefinition>().ToList();
			List<CRWeatherDefinition> weathers = FindAssetsByType<CRWeatherDefinition>().ToList();
			List<CRUnlockableDefinition> unlockables = FindAssetsByType<CRUnlockableDefinition>().ToList();
			List<CRItemDefinition> items = FindAssetsByType<CRItemDefinition>().ToList();
			List<CRMapObjectDefinition> mapObjects = FindAssetsByType<CRMapObjectDefinition>().ToList();
			foreach(AssetBundleData bundleData in content.assetBundles) {
				Debug.Log($"migrating: {bundleData.configName}");
				DoMigrations(bundleData.enemies, enemies, () => new CREnemyReference(""));
				DoMigrations(bundleData.unlockables, unlockables, () => new CRUnlockableReference(""));
				DoMigrations(bundleData.weathers, weathers, () => new CRWeatherReference(""));
				DoMigrations(bundleData.items, items, () => new CRItemReference(""));
				DoMigrations(bundleData.mapObjects, items, () => new CRMapObjectReference(""));
			}
		}
	}

	public static void DoMigrations<TEntity, TDef, TRef>(List<TEntity> entityDataList, List<TDef> definitions, Func<TRef> newCallback) where TEntity : EntityData<TRef> where TDef : CRContentDefinition where TRef : CRContentReference {
		foreach(TEntity data in entityDataList) {
			if(!string.IsNullOrEmpty(data.EntityName)) continue; // already migrated

			(string guid, TDef def) = GetMigration(definitions, data);
			TRef reference = newCallback();
			reference.entityName = def.EntityNameReference;
			reference.assetGUID = guid;
			data._reference = reference;
		}
	}
	
	public static (string guid, T definition) GetMigration<T>(List<T> definitions, EntityData data) where T : CRContentDefinition {
		T definition = definitions.First(it => it.EntityNameReference == data.entityName);
		string path = AssetDatabase.GetAssetPath(definition);
		string guid = AssetDatabase.GUIDFromAssetPath(path).ToString();
		return (guid, definition);
	}
	
	public static IEnumerable<T> FindAssetsByType<T>() where T : Object {
		var guids = AssetDatabase.FindAssets($"t:{typeof(T)}");
		foreach (var t in guids) {
			var assetPath = AssetDatabase.GUIDToAssetPath(t);
			var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
			if (asset != null) {
				yield return asset;
			}
		}
	}
}