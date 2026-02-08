using Dawn.Editor.Extensions;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;

namespace Dawn.Editor;

public class DawnSceneObjectReferenceData
{
    public List<Mesh> cachedMeshes = new();
    public List<Matrix4x4> cachedTransforms = new();
    public Color hologramColor;
    public Color wireframeColor;
    public bool dataCollected;

    public void CollectData(DawnSceneObjectReference target)
    {
        dataCollected = false;

        GameObject go = FindObjectOnLoadedScenes(target);
        if (!go) return;

        CollectDataRecursive(go.transform, Matrix4x4.identity);

        hologramColor = target.hologramColor;
        wireframeColor = target.wireframeColor;

        dataCollected = true;
    }

    /*
     * Ok, so: i feel like i supposed to explain what is going on here at least for my own sanity.
     * Since Unity doesnt support scene cross-reference, I needed to come up with a way to "save" an object for reference in order to read its meshes.
     * I dont think we have convinient way to find object from another scene so that added even more curse to this method.
     * Also i was needed a way to retrive path to this object so thats why TransformExtensions was created.
     * Anyway:
     * 1. Get all scenes
     * 2. Find active scene (In our case its ship scene for uuh obv reason)
     * 3. Find our object
     *   3.1 If we have cachedObjectPath, then we just need to find it from root object
     *   3.2 If we dont have cachedObjectPath, then we need iterate thru every child of root object
     * 4. Return our object
     */
    public GameObject FindObjectOnLoadedScenes(DawnSceneObjectReference target)
    {
        if (string.IsNullOrEmpty(target.sceneObjectReferenceSearch)) return null!;

        Transform resultTransform = null!;

        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();

            if (!string.IsNullOrEmpty(target.cachedObjectPath))
            {
                var root = roots.FirstOrDefault(go => go.name == target.cachedObjectPath.Split('/')[0]);
                int index = target.cachedObjectPath.IndexOf('/');
                resultTransform = root.transform.Find(target.cachedObjectPath.Substring(index + 1));


                break;
            }

            string path = "";

            foreach (var root in roots)
            {
                resultTransform = root.transform.FindPathRecursive(target.sceneObjectReferenceSearch, out path);
                if (resultTransform)
                {
                    target.cachedObjectPath = path;
                    break;
                }
            }
        }

        return resultTransform ? resultTransform.gameObject : null!;
    }

    public void CollectDataRecursive(Transform targetTransform, Matrix4x4 parentMatrix)
    {
        Matrix4x4 localToWorld = parentMatrix * Matrix4x4.TRS(
            targetTransform.localPosition,
            targetTransform.localRotation,
            targetTransform.localScale
        );

        MeshFilter meshFilter = targetTransform.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedMesh = targetTransform.GetComponent<SkinnedMeshRenderer>();

        if (meshFilter && meshFilter.sharedMesh)
        {
            cachedMeshes.Add(meshFilter.sharedMesh);
            cachedTransforms.Add(localToWorld);
        }
        else if (skinnedMesh && skinnedMesh.sharedMesh)
        {
            cachedMeshes.Add(skinnedMesh.sharedMesh);
            cachedTransforms.Add(localToWorld);
        }

        foreach (Transform child in targetTransform)
            CollectDataRecursive(child, localToWorld);
    }
}

[CustomEditor(typeof(DawnSceneObjectReference))]
public class DawnSceneObjectReferenceEditor : UnityEditor.Editor
{
    public static DawnSceneObjectReference curentTarget = null!;
    public static DawnSceneObjectReferenceData curentData = null!;

    public static List<DawnSceneObjectReferenceData> visibleReferences = new();

    void OnEnable()
    {
        curentTarget = (DawnSceneObjectReference)target;
        curentData = new DawnSceneObjectReferenceData();

        curentData.CollectData(curentTarget);

        if (curentTarget.keepVisible && curentData.dataCollected)
            visibleReferences.Add(curentData);
    }

    void OnDisable()
    {
        visibleReferences.Clear();
    }

    //TODO: fuck gizmos, switch to graphics
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawGizmo(DawnSceneObjectReference spawner, GizmoType gizmoType)
    {
        foreach (var reference in visibleReferences) DrawHologram(reference);
        DrawHologram(curentData);
    }

    static void DrawHologram(DawnSceneObjectReferenceData data)
    {
        if (data.cachedMeshes.Count == 0) return;

        Gizmos.color = data.hologramColor;

        for (int i = 0; i < data.cachedMeshes.Count; i++)
        {
            if (data.cachedMeshes[i] == null) continue;

            Matrix4x4 worldMatrix = Matrix4x4.TRS(
                curentTarget.transform.position,
                curentTarget.transform.rotation,
                curentTarget.transform.lossyScale
            ) * data.cachedTransforms[i];

            Gizmos.matrix = worldMatrix;
            Gizmos.DrawMesh(data.cachedMeshes[i], Vector3.zero, Quaternion.identity, Vector3.one);
        }
    }
}

