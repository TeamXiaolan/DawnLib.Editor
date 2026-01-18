using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.ProBuilder;

namespace Dawn.Editor;
[InitializeOnLoad]
public static class SpawnSyncedPrefabCache
{
    private static readonly Dictionary<string, List<GameObject>> _prefabsByName = new();

    static SpawnSyncedPrefabCache()
    {
        RefreshCache();
    }

    public static void RefreshCache()
    {
        _prefabsByName.Clear();

        string[] guids = AssetDatabase.FindAssets("t:GameObject");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                continue;

            if (!_prefabsByName.TryGetValue(go.name, out List<GameObject> list))
            {
                list = new List<GameObject>();
                _prefabsByName.Add(go.name, list);
            }

            list.Add(go);
        }
    }

    public static bool TryGetRealPrefab(GameObject placeholder, [NotNullWhen(true)] out GameObject? realPrefab)
    {
        realPrefab = null;
        if (placeholder == null)
        {
            return false;
        }

        if (placeholder.GetComponentInChildren<Renderer>() != null)
        {
            realPrefab = placeholder;
            return true;
        }

        if (!_prefabsByName.TryGetValue(placeholder.name, out List<GameObject> list) || list.Count == 0)
        {
            return false;
        }

        foreach (var candidate in list)
        {
            if (candidate != null && candidate != placeholder)
            {
                realPrefab = candidate;
                return true;
            }
        }

        realPrefab = placeholder;
        return true;
    }
}

[CustomEditor(typeof(SpawnSyncedObject))]
public class SpawnSyncedObjectEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Refresh SpawnSynced Prefab Cache"))
        {
            SpawnSyncedPrefabCache.RefreshCache();
        }

        DuskMapObjectDefinitionCache.PreviewColor = EditorGUILayout.ColorField("Preview Colour", DuskMapObjectDefinitionCache.PreviewColor);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawSpawnSyncedObjectGizmos(SpawnSyncedObject spawner, GizmoType gizmoType)
    {
        if (spawner == null || spawner.spawnPrefab == null)
            return;

        if (!SpawnSyncedPrefabCache.TryGetRealPrefab(spawner.spawnPrefab, out GameObject? prefab) || prefab == null)
            return;

        DrawPrefabHologram(prefab, spawner.transform);
    }

    private static void DrawPrefabHologram(GameObject prefab, Transform spawnerTransform)
    {
        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>();
        ProBuilderMesh[] pbMeshes = prefab.GetComponentsInChildren<ProBuilderMesh>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();

        if (meshFilters.Length == 0 && skinnedMeshRenderers.Length == 0 && pbMeshes.Length == 0)
            return;

        Color prevColor = Gizmos.color;
        Matrix4x4 prevMatrix = Gizmos.matrix;

        Gizmos.color = DuskMapObjectDefinitionCache.PreviewColor;

        Matrix4x4 prefabRootInverse = prefab.transform.worldToLocalMatrix;
        Matrix4x4 prefabRootScale = Matrix4x4.Scale(prefab.transform.localScale);
        Matrix4x4 spawnerWorld = spawnerTransform.localToWorldMatrix;

        Matrix4x4 rootScaleMatrix = prefabRootScale;

        foreach (ProBuilderMesh pbMesh in pbMeshes)
        {
            if (pbMesh.mesh == null)
                continue;

            MeshRenderer meshRenderer = pbMesh.GetComponent<MeshRenderer>();
            if (meshRenderer == null || !meshRenderer.enabled)
                continue;

            Matrix4x4 childToPrefabRoot = prefabRootInverse * pbMesh.transform.localToWorldMatrix;
            Gizmos.matrix = spawnerWorld * rootScaleMatrix * childToPrefabRoot;

            Gizmos.DrawMesh(pbMesh.mesh, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
                continue;

            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer == null || !meshRenderer.enabled)
                continue;

            Matrix4x4 childToPrefabRoot = prefabRootInverse * meshFilter.transform.localToWorldMatrix;
            Gizmos.matrix = spawnerWorld * rootScaleMatrix * childToPrefabRoot;

            Gizmos.DrawMesh(meshFilter.sharedMesh, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        foreach (SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers)
        {
            if (skinnedMeshRenderer.sharedMesh == null)
                continue;

            if (!skinnedMeshRenderer.enabled)
                continue;

            Matrix4x4 childToPrefabRoot = prefabRootInverse * skinnedMeshRenderer.transform.localToWorldMatrix;
            Gizmos.matrix = spawnerWorld * rootScaleMatrix * childToPrefabRoot;

            Gizmos.DrawMesh(skinnedMeshRenderer.sharedMesh, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        Gizmos.matrix = prevMatrix;
        Gizmos.color = prevColor;
    }
}