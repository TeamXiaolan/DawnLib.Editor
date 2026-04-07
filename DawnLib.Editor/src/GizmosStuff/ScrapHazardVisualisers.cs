using UnityEngine;
using UnityEditor;

namespace Dawn.Editor.GizmosStuff;

public static class RandomScrapSpawnGizmoDrawer
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawRandomScrapSpawnGizmo(RandomScrapSpawn area, GizmoType gizmoType)
    {
        if (area == null)
            return;

        Vector3 center = area.transform.position;
        float radius = area.itemSpawnRange;

        if ((gizmoType & GizmoType.Selected) != 0)
        {
            Handles.color = new Color(0f, 1f, 1f, 0.10f);
            Handles.DrawSolidDisc(center, area.transform.up, radius);
        }

        Handles.color = new Color(0f, 1f, 1f, 0.95f);
        Handles.DrawWireDisc(center, area.transform.up, radius);

        Gizmos.color = new Color(0f, 1f, 1f, 1f);
        Gizmos.DrawSphere(center, 0.1f);
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawRandomMapObjectSpawnGizmo(RandomMapObject area, GizmoType gizmoType)
    {
        if (area == null)
            return;

        Vector3 center = area.transform.position;
        float radius = area.spawnRange;

        if ((gizmoType & GizmoType.Selected) != 0)
        {
            Handles.color = new Color(1f, 0f, 0f, 0.10f);
            Handles.DrawSolidDisc(center, area.transform.up, radius);
        }

        Handles.color = new Color(1f, 0f, 0f, 0.95f);
        Handles.DrawWireDisc(center, area.transform.up, radius);

        Gizmos.color = new Color(1f, 0f, 0f, 1f);
        Gizmos.DrawSphere(center, 0.1f);
    }
}