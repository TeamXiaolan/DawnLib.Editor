using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor;
public static class PrefabAssetExtractor
{
    public static void ExtractPrefabAssets(GameObject prefab, string folderPath)
    {
        if (prefab == null) return;

        HashSet<string> modelPaths = new();
        HashSet<string> materialPaths = new();
        HashSet<string> texturePaths = new();
        HashSet<string> animPaths = new();
        HashSet<string> audioPaths = new();

        Component[] components = prefab.GetComponentsInChildren<Component>(true);

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            if (component is MeshFilter meshFilter && meshFilter.sharedMesh != null)
            {
                AddPath(meshFilter.sharedMesh, modelPaths);
            }

            if (component is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
            {
                AddPath(skinnedMeshRenderer.sharedMesh, modelPaths);
            }

            if (component is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                AddPath(meshCollider.sharedMesh, modelPaths);
            }

            if (component is Renderer renderer)
            {
                Material[] sharedMats = renderer.sharedMaterials;
                if (sharedMats != null)
                {
                    foreach (Material material in sharedMats)
                    {
                        if (material == null)
                        {
                            continue;
                        }
                        AddPath(material, materialPaths);

                        foreach (Texture texture in GetAllTexturesFromMaterial(material))
                        {
                            AddPath(texture, texturePaths);
                        }
                    }
                }
            }

            if (component is Animator animator)
            {
                RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
                foreach (AnimationClip animClip in GetClipsFromRuntimeController(runtimeController))
                {
                    AddPath(animClip, animPaths);
                }
            }

            if (component is AudioSource audioSource && audioSource.clip != null)
            {
                AddPath(audioSource.clip, audioPaths);
            }

            if (component is MonoBehaviour monoBehaviour)
            {
                foreach (AudioClip audioClip in GetObjectRefsFromSerialized<AudioClip>(monoBehaviour))
                {
                    AddPath(audioClip, audioPaths);
                }
            }
        }

        CopyAll(modelPaths, Path.Combine(folderPath, "Models"));
        CopyAll(materialPaths, Path.Combine(folderPath, "Materials"));
        CopyAll(texturePaths, Path.Combine(folderPath, "Textures"));
        CopyAll(animPaths, Path.Combine(folderPath, "Animations"));
        CopyAll(audioPaths, Path.Combine(folderPath, "Sounds"));
    }

    private static void AddPath(Object obj, HashSet<string> set)
    {
        if (obj == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
        {
            set.Add(path);
        }
    }

    private static void CopyAll(HashSet<string> paths, string targetFolder)
    {
        if (paths.Count == 0)
        {
            return;
        }

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        foreach (string path in paths)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null)
            {
                CopyAsset(obj, targetFolder);
            }
        }
    }

    private static IEnumerable<Texture> GetAllTexturesFromMaterial(Material material)
    {
        HashSet<Texture> results = new();
        if (material == null)
        {
            return results;
        }

        SerializedObject serializedObject = new(material);
        SerializedProperty serializedProperty = serializedObject.GetIterator();

        if (serializedProperty.Next(true))
        {
            do
            {
                if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Texture? texture = serializedProperty.objectReferenceValue as Texture;
                    if (texture != null)
                    {
                        results.Add(texture);
                    }
                }
            } while (serializedProperty.Next(false));
        }
        return results;
    }

    private static IEnumerable<AnimationClip> GetClipsFromRuntimeController(RuntimeAnimatorController runtimeController)
    {
        HashSet<AnimationClip> results = new();
        if (runtimeController == null)
        {
            return results;
        }

        foreach (var clip in runtimeController.animationClips)
        {
            if (clip != null)
            {
                results.Add(clip);
            }
        }

        if (runtimeController is AnimatorOverrideController animatorOverrideController)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new();
            animatorOverrideController.GetOverrides(overrides);
            foreach (var kv in overrides)
            {
                if (kv.Value != null)
                {
                    results.Add(kv.Value);
                }
                else if (kv.Key != null)
                {
                    results.Add(kv.Key);
                }
            }
        }
        return results;
    }

    private static IEnumerable<T> GetObjectRefsFromSerialized<T>(MonoBehaviour monoBehaviour) where T : Object
    {
        HashSet<T> results = new();
        if (monoBehaviour == null)
        {
            return results;
        }

        SerializedObject serializedObject = new(monoBehaviour);
        SerializedProperty serializedProperty = serializedObject.GetIterator();

        if (serializedProperty.Next(true))
        {
            do
            {
                if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (serializedProperty.objectReferenceValue is T tRef && tRef != null)
                    {
                        results.Add(tRef);
                    }
                }
            } while (serializedProperty.Next(false));
        }
        return results;
    }

    private static void CopyAsset(Object asset, string targetFolder)
    {
        string srcPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(srcPath))
        {
            return;
        }

        if (!Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        string fileName = Path.GetFileName(srcPath);
        string destinationPath = Path.Combine(targetFolder, fileName).Replace("\\", "/");

        destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);
        AssetDatabase.CopyAsset(srcPath, destinationPath);
    }
}