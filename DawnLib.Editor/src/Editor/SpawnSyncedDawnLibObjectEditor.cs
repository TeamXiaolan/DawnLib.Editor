using System.Collections.Generic;
using Dawn.Utils;
using UnityEditor;
using UnityEngine;
using Dusk;
using System.Diagnostics.CodeAnalysis;

namespace Dawn.Editor;

[InitializeOnLoad]
public static class DuskMapObjectDefinitionCache
{
    private static readonly Dictionary<string, GameObject> _prefabsByKey = new();

    private static Color _previewColor = new Color(0f, 1f, 1f, 0.25f);
    public static Color PreviewColor
    {
        get => _previewColor;
        set => _previewColor = value;
    }

    static DuskMapObjectDefinitionCache()
    {
        RefreshCache();
    }

    public static void RefreshCache()
    {
        _prefabsByKey.Clear();

        string[] guids = AssetDatabase.FindAssets("t:DuskMapObjectDefinition");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            DuskMapObjectDefinition duskMapObjectDefinition = AssetDatabase.LoadAssetAtPath<DuskMapObjectDefinition>(path);
            if (duskMapObjectDefinition == null)
                continue;

            if (duskMapObjectDefinition.GameObject == null)
                continue;

            string keyString = duskMapObjectDefinition.TypedKey.ToString();
            if (!_prefabsByKey.ContainsKey(keyString))
            {
                _prefabsByKey.Add(keyString, duskMapObjectDefinition.GameObject);
            }
        }
    }

    public static bool TryGetPrefab(object namespacedKey, [NotNullWhen(true)] out GameObject? prefab)
    {
        prefab = null;
        if (namespacedKey == null)
        {
            return false;
        }

        string keyString = namespacedKey.ToString();
        return _prefabsByKey.TryGetValue(keyString, out prefab);
    }
}

[CustomEditor(typeof(SpawnSyncedDawnLibObject))]
public class SpawnSyncedDawnLibObjectEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Refresh Dusk Map Object Cache"))
        {
            DuskMapObjectDefinitionCache.RefreshCache();
        }

        DuskMapObjectDefinitionCache.PreviewColor = EditorGUILayout.ColorField("Preview Colour", DuskMapObjectDefinitionCache.PreviewColor);

        EditorGUILayout.Space();

        DrawDefaultInspector();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawSpawnSyncedDawnLibObjectGizmos(SpawnSyncedDawnLibObject spawner, GizmoType gizmoType)
    {
        if (spawner == null)
            return;

        if (spawner.objectTypesWithRarity == null || spawner.objectTypesWithRarity.Count == 0)
            return;

        foreach (var entry in spawner.objectTypesWithRarity)
        {
            if (!DuskMapObjectDefinitionCache.TryGetPrefab(entry.NamespacedMapObjectKey, out GameObject? prefab))
                continue;

            DrawPrefabHologram(prefab, spawner.transform);
        }
    }

    private static void DrawPrefabHologram(GameObject prefab, Transform spawnerTransform)
    {
        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();

        if (meshFilters.Length == 0 && skinnedMeshRenderers.Length == 0)
            return;

        Color prevColor = Gizmos.color;
        Matrix4x4 prevMatrix = Gizmos.matrix;

        Gizmos.color = DuskMapObjectDefinitionCache.PreviewColor;

        Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
                continue;

            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null || !meshRenderer.enabled)
                continue;

            Matrix4x4 childLocal = rootInverse * meshFilter.transform.localToWorldMatrix;
            Matrix4x4 matrix = spawnerTransform.localToWorldMatrix * childLocal;
            Gizmos.matrix = matrix;

            Gizmos.DrawMesh(meshFilter.sharedMesh, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            if (skinnedMeshRenderer.sharedMesh == null)
                continue;

            if (!skinnedMeshRenderer.enabled)
                continue;

            Matrix4x4 childLocal = rootInverse * skinnedMeshRenderer.transform.localToWorldMatrix;
            Matrix4x4 matrix = spawnerTransform.localToWorldMatrix * childLocal;
            Gizmos.matrix = matrix;

            Gizmos.DrawMesh(skinnedMeshRenderer.sharedMesh, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        Gizmos.matrix = prevMatrix;
        Gizmos.color = prevColor;
    }
}