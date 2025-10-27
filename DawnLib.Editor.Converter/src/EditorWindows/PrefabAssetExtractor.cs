using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor;
public static class PrefabAssetExtractor
{
    public static void ExtractPrefabAssets(GameObject prefab, string folderPath)
    {
        if (prefab == null)
        {
            return;
        }

        HashSet<string> modelPaths = new();
        HashSet<string> animPaths = new();
        HashSet<string> audioPaths = new();
        HashSet<string> copiedTexturePaths = new();

        string modelsOut = Path.Combine(folderPath, "Models");
        string materialsOut = Path.Combine(folderPath, "Materials");
        string texturesOut = Path.Combine(folderPath, "Textures");
        string animsOut = Path.Combine(folderPath, "Animations");
        string soundsOut = Path.Combine(folderPath, "Sounds");

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
                            continue;

                        EnsureStandaloneMaterialAsset(material, materialsOut, out Material savedMat);
                        CopyAllMaterialTextures(savedMat, texturesOut, copiedTexturePaths);
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

        CopyAll(modelPaths, modelsOut);
        CopyAll(animPaths, animsOut);
        CopyAll(audioPaths, soundsOut);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void AddPath(Object obj, HashSet<string> set)
    {
        if (obj == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
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

    private static void EnsureStandaloneMaterialAsset(Material material, string materialsOut, out Material savedMat)
    {
        Directory.CreateDirectory(materialsOut);

        string path = AssetDatabase.GetAssetPath(material);
        if (!string.IsNullOrEmpty(path) && path.EndsWith(".mat"))
        {
            CopyAssetFile(path, materialsOut);
            string newPath = Path.Combine(materialsOut, Path.GetFileName(path)).Replace("\\", "/");
            savedMat = AssetDatabase.LoadAssetAtPath<Material>(newPath) ?? material;
            return;
        }

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(materialsOut, $"{material.name}.mat").Replace("\\", "/"));

        Material matClone = Object.Instantiate(material);
        matClone.name = Path.GetFileNameWithoutExtension(targetPath);
        AssetDatabase.CreateAsset(matClone, targetPath);
        EditorUtility.CopySerialized(material, matClone);
        savedMat = matClone;
    }

    private static void CopyAllMaterialTextures(Material material, string texturesOut, HashSet<string> copiedTexturePaths)
    {
        if (material == null)
        {
            return;
        }

        Directory.CreateDirectory(texturesOut);
        foreach (string propName in material.GetTexturePropertyNames())
        {
            Texture tex = material.GetTexture(propName);
            if (tex == null) continue;

            string texPath = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(texPath))
                continue;

            if (!texPath.StartsWith("Assets/"))
                continue;

            if (copiedTexturePaths.Add(texPath))
            {
                CopyAssetFile(texPath, texturesOut);
            }
        }
    }

    private static void CopyAssetFile(string sourcePath, string targetFolder)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return;

        Directory.CreateDirectory(targetFolder);

        string fileName = Path.GetFileName(sourcePath);
        string destinationPath = Path.Combine(targetFolder, fileName).Replace("\\", "/");
        destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);
        AssetDatabase.CopyAsset(sourcePath, destinationPath);
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