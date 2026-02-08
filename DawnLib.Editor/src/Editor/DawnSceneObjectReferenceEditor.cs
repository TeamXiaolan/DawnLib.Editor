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
    public Material wireframeMaterial = null!;
    public Material hologramMaterial = null!;
    public bool dataCollected;

    public void CollectData(DawnSceneObjectReference target)
    {
        dataCollected = false;
        cachedMeshes.Clear();
        cachedTransforms.Clear();

        GameObject go = FindObjectOnLoadedScenes(target);
        if (!go) return;

        CollectDataRecursive(go.transform, Matrix4x4.identity);

        hologramColor = target.hologramColor;
        wireframeColor = target.wireframeColor;

        GetMaterial();

        dataCollected = true;
    }

    void GetMaterial()
    {
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;

        hologramMaterial = new Material(shader);
        hologramMaterial.hideFlags = HideFlags.HideAndDontSave;
        hologramMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        hologramMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        hologramMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        hologramMaterial.SetInt("_ZWrite", 0);
        hologramMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        hologramMaterial.color = hologramColor;

        wireframeMaterial = new Material(shader);
        wireframeMaterial.hideFlags = HideFlags.HideAndDontSave;
        wireframeMaterial.color = wireframeColor;
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

            if (!string.IsNullOrEmpty(target.cachedObjectPath) && target.cachedObjectPath.Split('/')[^1] == target.sceneObjectReferenceSearch)
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
    public DawnSceneObjectReference curentTarget = null!;
    public DawnSceneObjectReferenceData curentData = null!;
    private static bool initialized = false;

    public static Dictionary<DawnSceneObjectReference, DawnSceneObjectReferenceData> visibleReferences = new();

    void OnEnable()
    {
        curentTarget = (DawnSceneObjectReference)target;
        if (!visibleReferences.TryGetValue(curentTarget, out curentData))
            curentData = new DawnSceneObjectReferenceData();

        curentData.CollectData(curentTarget);

        if (curentTarget.keepVisible && curentData.dataCollected && !visibleReferences.ContainsKey(curentTarget))
            visibleReferences.Add(curentTarget, curentData);
    }

    void OnDisable()
    {
        if (curentTarget.keepVisible)
        {
            if (visibleReferences.ContainsKey(curentTarget)) return;
            else visibleReferences.Add(curentTarget, curentData);
        }
        else
        {
            visibleReferences.Remove(curentTarget);
            DestroyImmediate(curentData.hologramMaterial);
            DestroyImmediate(curentData.wireframeMaterial);
        }
    }

    void OnSceneGUI()
    {
        if (UnityEngine.Event.current.type != EventType.Repaint) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() == null) return;

        if (!visibleReferences.ContainsKey(curentTarget)) DrawHologram(curentTarget, curentData);

        //Debug.Log($"count = {visibleReferences.Count}");
        //foreach (var data in visibleReferences)
        //    DrawHologram(data.Key, data.Value);
    }

    static void DrawHologram(DawnSceneObjectReference target, DawnSceneObjectReferenceData data)
    {
        if (data.cachedMeshes.Count == 0) return;

        for (int i = 0; i < data.cachedMeshes.Count; i++)
        {
            if (data.cachedMeshes[i] == null || data.cachedTransforms[i] == null) continue;

            Matrix4x4 targetMatrix = target.transform.localToWorldMatrix;

            Matrix4x4 worldMatrix = targetMatrix * data.cachedTransforms[i];

            data.hologramMaterial.SetPass(0);
            Graphics.DrawMeshNow(data.cachedMeshes[i], worldMatrix);

            data.wireframeMaterial.SetPass(0);
            GL.wireframe = true;
            Graphics.DrawMeshNow(data.cachedMeshes[i], worldMatrix);
            GL.wireframe = false;
        }
    }

    void OnValidate() => OnEnable();
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneObjectReferenceSearch"));
        EditorGUILayout.LabelField($"{curentTarget.cachedObjectPath}");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keepVisible"));

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("hologramColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("wireframeColor"));

        EditorGUILayout.Space();

        EditorGUILayout.LabelField($"Currently cached: {visibleReferences.Count}");

        if (GUILayout.Button("Clear cache"))
        {
            foreach(var data in visibleReferences)
            {
                DestroyImmediate(data.Value.hologramMaterial);
                DestroyImmediate(data.Value.wireframeMaterial);
            }
            visibleReferences.Clear();
        }

        serializedObject.ApplyModifiedProperties();
    }

    static void OnSceneGUIStatic(SceneView sceneView)
    {
        if (UnityEngine.Event.current.type != EventType.Repaint) return;
        if (PrefabStageUtility.GetCurrentPrefabStage() == null) return;

        foreach (var data in visibleReferences)
            DrawHologram(data.Key, data.Value);
    }

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        if (initialized) return;

        visibleReferences.Clear();
        SceneView.duringSceneGui += OnSceneGUIStatic;
        initialized = true;
    }
}