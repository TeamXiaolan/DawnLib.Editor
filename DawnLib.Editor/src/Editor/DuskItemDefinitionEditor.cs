using System.Collections.Generic;
using Dusk;
using Dusk.Weights;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor;

[CustomEditor(typeof(DuskItemDefinition))]
public class DuskItemDefinitionEditor : UnityEditor.Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		DuskItemDefinition itemDefinition = (DuskItemDefinition)target;

		if (GUILayout.Button("Generate Default Empty Weights"))
		{
            string? modNamespace = itemDefinition.Key.Namespace;
            if (string.IsNullOrEmpty(modNamespace))
            {
                modNamespace = "lethal_company";
            }

            string? modKey = itemDefinition.Key.Key;
            if (string.IsNullOrEmpty(modKey))
            {
                Debug.LogError($"DuskItemDefinition {itemDefinition.name} has no key.");
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

            List<NamespacedKey> weatherNamespacedKeysToAdd =
            [
                NamespacedKey.From("lethal_company", "vanilla"),
                NamespacedKey.From("lethal_company", "custom"),
                NamespacedKey.From("lethal_company", "none"),
                NamespacedKey.From("lethal_company", "rainy"),
                NamespacedKey.From("lethal_company", "stormy"),
                NamespacedKey.From("lethal_company", "foggy"),
                NamespacedKey.From("lethal_company", "flooded"),
                NamespacedKey.From("lethal_company", "eclipsed"),
                NamespacedKey.From(modNamespace, $"{modKey}_none"),
                NamespacedKey.From(modNamespace, $"{modKey}_low"),
                NamespacedKey.From(modNamespace, $"{modKey}_medium"),
                NamespacedKey.From(modNamespace, $"{modKey}_high"),
                NamespacedKey.From(modNamespace, $"{modKey}_ultra_high"),
            ];

            foreach (NamespacedKey namespacedKey in moonNamespacedKeysToAdd)
            {
                bool moonNamespacedKeyExists = false;
                foreach (NamespacedConfigWeight namespacedConfigWeight in itemDefinition.MoonSpawnWeightsConfig)
                {
                    if (namespacedConfigWeight.NamespacedKey.Namespace == namespacedKey.Namespace && namespacedConfigWeight.NamespacedKey.Key == namespacedKey.Key)
                    {
                        moonNamespacedKeyExists = true;
                        break;
                    }
                }

                if (!moonNamespacedKeyExists)
                {
                    NamespacedConfigWeight namespacedConfigWeight = new()
                    {
                        NamespacedKey = namespacedKey,
                        MathOperation = MathOperation.Additive,
                        Weight = 0f
                    };
                    itemDefinition.MoonSpawnWeightsConfig.Add(namespacedConfigWeight);
                }
            }

            foreach (NamespacedKey namespacedKey in interiorNamespacedKeysToAdd)
            {
                bool interiorNamespacedKeyExists = false;
                foreach (NamespacedConfigWeight namespacedConfigWeight in itemDefinition.InteriorSpawnWeightsConfig)
                {
                    if (namespacedConfigWeight.NamespacedKey.Namespace == namespacedKey.Namespace && namespacedConfigWeight.NamespacedKey.Key == namespacedKey.Key)
                    {
                        interiorNamespacedKeyExists = true;
                        break;
                    }
                }

                if (!interiorNamespacedKeyExists)
                {
                    NamespacedConfigWeight namespacedConfigWeight = new()
                    {
                        NamespacedKey = namespacedKey,
                        MathOperation = MathOperation.Additive,
                        Weight = 0f
                    };
                    itemDefinition.InteriorSpawnWeightsConfig.Add(namespacedConfigWeight);
                }
            }

            foreach (NamespacedKey namespacedKey in weatherNamespacedKeysToAdd)
            {
                bool weatherNamespacedKeyExists = false;
                foreach (NamespacedConfigWeight namespacedConfigWeight in itemDefinition.WeatherSpawnWeightsConfig)
                {
                    if (namespacedConfigWeight.NamespacedKey.Namespace == namespacedKey.Namespace && namespacedConfigWeight.NamespacedKey.Key == namespacedKey.Key)
                    {
                        weatherNamespacedKeyExists = true;
                        break;
                    }
                }

                if (!weatherNamespacedKeyExists)
                {
                    NamespacedConfigWeight namespacedConfigWeight = new()
                    {
                        NamespacedKey = namespacedKey,
                        MathOperation = MathOperation.Multiplicative,
                        Weight = 1f
                    };
                    itemDefinition.WeatherSpawnWeightsConfig.Add(namespacedConfigWeight);
                }
            }

            EditorUtility.SetDirty(itemDefinition);
		}
	}
}