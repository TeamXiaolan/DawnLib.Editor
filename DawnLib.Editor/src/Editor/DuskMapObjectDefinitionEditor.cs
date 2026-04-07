using System.Collections.Generic;
using Dusk;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor;

[CustomEditor(typeof(DuskMapObjectDefinition))]
public class DuskMapObjectDefinitionEditor : UnityEditor.Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		DuskMapObjectDefinition mapObjectDefinition = (DuskMapObjectDefinition)target;

		if (GUILayout.Button("Generate Default Empty Weights"))
		{
            string? modNamespace = mapObjectDefinition.Key.Namespace;
            if (string.IsNullOrEmpty(modNamespace))
            {
                modNamespace = "lethal_company";
            }

            string? modKey = mapObjectDefinition.Key.Key;
            if (string.IsNullOrEmpty(modKey))
            {
                Debug.LogError($"DuskMapObjectDefinition {mapObjectDefinition.name} has no key.");
                return;
            }

            List<NamespacedKey> moonNamespacedKeysToAdd =
            [
                NamespacedKey.From("lethal_company", "vanilla"),
                NamespacedKey.From("lethal_company", "custom"),
                NamespacedKey.From("lethal_company", "experimentation"),
                NamespacedKey.From("lethal_company", "vow"),
                NamespacedKey.From("lethal_company", "march"),
                NamespacedKey.From("lethal_company", "assurance"),
                NamespacedKey.From("lethal_company", "offense"),
                NamespacedKey.From("lethal_company", "adamance"),
                NamespacedKey.From("lethal_company", "embrion"),
                NamespacedKey.From("lethal_company", "rend"),
                NamespacedKey.From("lethal_company", "dine"),
                NamespacedKey.From("lethal_company", "titan"),
                NamespacedKey.From("lethal_company", "artifice"),
                NamespacedKey.From(modNamespace, $"{modKey}_none"),
                NamespacedKey.From(modNamespace, $"{modKey}_low"),
                NamespacedKey.From(modNamespace, $"{modKey}_medium"),
                NamespacedKey.From(modNamespace, $"{modKey}_high"),
                NamespacedKey.From(modNamespace, $"{modKey}_ultra_high"),
            ];

            List<NamespacedKey> interiorNamespacedKeysToAdd =
            [
                NamespacedKey.From("lethal_company", "vanilla"),
                NamespacedKey.From("lethal_company", "custom"),
                NamespacedKey.From("lethal_company", "facility"),
                NamespacedKey.From("lethal_company", "mansion"),
                NamespacedKey.From("lethal_company", "mineshaft"),
                NamespacedKey.From(modNamespace, $"{modKey}_none"),
                NamespacedKey.From(modNamespace, $"{modKey}_low"),
                NamespacedKey.From(modNamespace, $"{modKey}_medium"),
                NamespacedKey.From(modNamespace, $"{modKey}_high"),
                NamespacedKey.From(modNamespace, $"{modKey}_ultra_high"),
            ];

            foreach (NamespacedKey namespacedKey in moonNamespacedKeysToAdd)
            {
                if (mapObjectDefinition.IsInsideHazard)
                {
                    bool insideMoonCurveExists = false;
                    foreach (NamespacedKeyWithAnimationCurve namespacedKeyWithCurve in mapObjectDefinition.InsideMoonCurveSpawnWeights)
                    {
                        if (namespacedKeyWithCurve.Key.Namespace == namespacedKey.Namespace && namespacedKeyWithCurve.Key.Key == namespacedKey.Key)
                        {
                            insideMoonCurveExists = true;
                            break;
                        }
                    }

                    if (!insideMoonCurveExists)
                    {
                        NamespacedKeyWithAnimationCurve namespacedKeyWithAnimationCurve = new()
                        {
                            Key = namespacedKey,
                            Curve = AnimationCurve.Constant(0f, 1f, 0f)
                        };
                        mapObjectDefinition.InsideMoonCurveSpawnWeights.Add(namespacedKeyWithAnimationCurve);
                    }
                }

                if (mapObjectDefinition.IsOutsideHazard)
                {
                    bool outsideMoonCurveExists = false;
                    foreach (NamespacedKeyWithAnimationCurve namespacedKeyWithCurve in mapObjectDefinition.OutsideMoonCurveSpawnWeights)
                    {
                        if (namespacedKeyWithCurve.Key.Namespace == namespacedKey.Namespace && namespacedKeyWithCurve.Key.Key == namespacedKey.Key)
                        {
                            outsideMoonCurveExists = true;
                            break;
                        }
                    }

                    if (!outsideMoonCurveExists)
                    {
                        NamespacedKeyWithAnimationCurve namespacedKeyWithAnimationCurve = new()
                        {
                            Key = namespacedKey,
                            Curve = AnimationCurve.Constant(0f, 1f, 0f)
                        };
                        mapObjectDefinition.OutsideMoonCurveSpawnWeights.Add(namespacedKeyWithAnimationCurve);
                    }
                }
            }

            foreach (NamespacedKey namespacedKey in interiorNamespacedKeysToAdd)
            {
                if (mapObjectDefinition.IsInsideHazard)
                {
                    bool insideInteriorCurveExists = false;
                    foreach (NamespacedKeyWithAnimationCurve namespacedKeyWithCurve in mapObjectDefinition.InsideInteriorCurveSpawnWeights)
                    {
                        if (namespacedKeyWithCurve.Key.Namespace == namespacedKey.Namespace && namespacedKeyWithCurve.Key.Key == namespacedKey.Key)
                        {
                            insideInteriorCurveExists = true;
                            break;
                        }
                    }

                    if (!insideInteriorCurveExists)
                    {
                        NamespacedKeyWithAnimationCurve namespacedKeyWithAnimationCurve = new()
                        {
                            Key = namespacedKey,
                            Curve = AnimationCurve.Constant(0f, 1f, 0f)
                        };
                        mapObjectDefinition.InsideInteriorCurveSpawnWeights.Add(namespacedKeyWithAnimationCurve);
                    }
                }

                if (mapObjectDefinition.IsOutsideHazard)
                {
                    bool outsideInteriorCurveExists = false;
                    foreach (NamespacedKeyWithAnimationCurve namespacedKeyWithCurve in mapObjectDefinition.OutsideInteriorCurveSpawnWeights)
                    {
                        if (namespacedKeyWithCurve.Key.Namespace == namespacedKey.Namespace && namespacedKeyWithCurve.Key.Key == namespacedKey.Key)
                        {
                            outsideInteriorCurveExists = true;
                            break;
                        }
                    }

                    if (!outsideInteriorCurveExists)
                    {
                        NamespacedKeyWithAnimationCurve namespacedKeyWithAnimationCurve = new()
                        {
                            Key = namespacedKey,
                            Curve = AnimationCurve.Constant(0f, 1f, 0f)
                        };
                        mapObjectDefinition.OutsideInteriorCurveSpawnWeights.Add(namespacedKeyWithAnimationCurve);
                    }
                }
            }

            EditorUtility.SetDirty(mapObjectDefinition);
		}
	}
}