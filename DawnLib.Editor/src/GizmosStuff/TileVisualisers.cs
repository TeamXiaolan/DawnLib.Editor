using UnityEditor;
using UnityEngine;
using DunGen;

namespace Dawn.Editor.GizmosStuff;

public static class TileGizmoDrawer
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawTileGizmo(Tile tile, GizmoType gizmoType)
    {
        Gizmos.color = Color.red;

        Bounds bounds = GetTileWorldBounds(tile);

        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    [MenuItem("CONTEXT/Tile/Copy Automatic Bounds To Override")]
    private static void CopyAutomaticBoundsToOverride(MenuCommand command)
    {
        Tile tile = (Tile)command.context;

        Bounds worldBounds = GetTileWorldBounds(tile);
        Bounds localBounds = WorldBoundsToLocal(tile.transform, worldBounds);

        Undo.RecordObject(tile, "Copy Automatic Tile Bounds To Override");

        tile.TileBoundsOverride = localBounds;

        EditorUtility.SetDirty(tile);
        SceneView.RepaintAll();
    }

    private static Bounds GetTileWorldBounds(Tile tile)
    {
        if (tile.OverrideAutomaticTileBounds)
        {
            return tile.transform.TransformBounds(tile.TileBoundsOverride);
        }

        if (tile.placement != null && !tile.placement.Bounds.Equals(default))
        {
            return tile.transform.parent != null ? tile.transform.parent.TransformBounds(tile.placement.Bounds) : tile.placement.Bounds;
        }

        Bounds bounds = UnityUtil.CalculateProxyBounds(tile.gameObject, Vector3.up);
        bounds = UnityUtil.CondenseBounds(bounds, tile.GetComponentsInChildren<Doorway>());

        return bounds;
    }

    private static Bounds WorldBoundsToLocal(Transform transform, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        Vector3[] corners =
        [
            new(min.x, min.y, min.z),
            new(min.x, min.y, max.z),
            new(min.x, max.y, min.z),
            new(min.x, max.y, max.z),
            new(max.x, min.y, min.z),
            new(max.x, min.y, max.z),
            new(max.x, max.y, min.z),
            new(max.x, max.y, max.z),
        ];

        Bounds localBounds = new(transform.InverseTransformPoint(corners[0]), Vector3.zero);

        for (int i = 1; i < corners.Length; i++)
        {
            localBounds.Encapsulate(transform.InverseTransformPoint(corners[i]));
        }

        return localBounds;
    }
}