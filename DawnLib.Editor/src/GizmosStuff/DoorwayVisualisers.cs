using UnityEngine;
using UnityEditor;
using DunGen;

namespace Dawn.Editor.GizmosStuff;

public static class DoorwayGizmoDrawer
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawDoorwayGizmo(Doorway area, GizmoType gizmoType)
    {
        Vector2 size = area.Socket.Size;
        Vector2 halfSize = size * 0.5f;
        Color doorwayColour;

        bool isValidPlacement = area.ValidateTransform(out var localTileBounds, out bool isAxisAligned, out bool isEdgePositioned);

        if (isValidPlacement)
        {
            doorwayColour = EditorConstants.DoorRectColourValid;
        }
        else if (!isAxisAligned)
        {
            doorwayColour = EditorConstants.DoorRectColourError;
        }
        else
        {
            doorwayColour = EditorConstants.DoorRectColourWarning;
        }


        // Draw Forward Vector
        float lineLength = Mathf.Min(size.x, size.y);

        Gizmos.color = EditorConstants.DoorDirectionColour;
        Gizmos.DrawLine(area.transform.position + area.transform.up * halfSize.y, area.transform.position + area.transform.up * halfSize.y + area.transform.forward * lineLength);


        // Draw Up Vector
        Gizmos.color = EditorConstants.DoorUpColour;
        Gizmos.DrawLine(area.transform.position + area.transform.up * halfSize.y, area.transform.position + area.transform.up * size.y);


        // Draw Rectangle
        Gizmos.color = doorwayColour;
        Vector3 topLeft = area.transform.position - (area.transform.right * halfSize.x) + (area.transform.up * size.y);
        Vector3 topRight = area.transform.position + (area.transform.right * halfSize.x) + (area.transform.up * size.y);
        Vector3 bottomLeft = area.transform.position - (area.transform.right * halfSize.x);
        Vector3 bottomRight = area.transform.position + (area.transform.right * halfSize.x);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);


        // Draw position correction line
        if (!isValidPlacement)
        {
            area.GetTileRoot(out _, out var tile);

            // Projected position is meaningless if the Doorway isn't attached to a Tile
            if (tile != null)
            {
                Vector3 projectedPosition = area.ProjectPositionToTileBounds(localTileBounds);

                Gizmos.color = Color.red;
                Gizmos.DrawLine(area.transform.position, projectedPosition);
            }
        }
    }
}