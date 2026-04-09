using System.Collections.Generic;
using Dusk;
using Dusk.Weights;
using UnityEditor;
using UnityEngine;

namespace Dawn.Editor;

[CustomEditor(typeof(DuskEnemyDefinition))]
public class DuskEnemyDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        DuskEnemyDefinition enemyDefinition = (DuskEnemyDefinition)target;

        if (GUILayout.Button("Generate Default Empty Weights"))
        {
            string? modNamespace = enemyDefinition.Key.Namespace;
            if (string.IsNullOrEmpty(modNamespace))
            {
                modNamespace = "lethal_company";
            }

            string? modKey = enemyDefinition.Key.Key;
            if (string.IsNullOrEmpty(modKey))
            {
                Debug.LogError($"DuskEnemyDefinition {enemyDefinition.name} has no key.");
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
                foreach (NamespacedConfigWeight namespacedConfigWeight in enemyDefinition.MoonSpawnWeightsConfig)
                {
                    if (namespacedConfigWeight.NamespacedKey.Namespace == namespacedKey.Namespace && namespacedConfigWeight.NamespacedKey.Key == namespacedKey.Key)
                    {
                        moonNamespacedKeyExists = true;
                        break;
                    }
                }

                if (!moonNamespacedKeyExists)
                {
                    MathOperation mathOperation = MathOperation.Additive;
                    if (namespacedKey.Key == $"{modKey}_none")
                    {
                        mathOperation = MathOperation.Multiplicative;
                    }
                    NamespacedConfigWeight namespacedConfigWeight = new()
                    {
                        NamespacedKey = namespacedKey,
                        MathOperation = mathOperation,
                        Weight = 0f
                    };
                    enemyDefinition.MoonSpawnWeightsConfig.Add(namespacedConfigWeight);
                }
            }

            foreach (NamespacedKey namespacedKey in interiorNamespacedKeysToAdd)
            {
                bool interiorNamespacedKeyExists = false;
                foreach (NamespacedConfigWeight namespacedConfigWeight in enemyDefinition.InteriorSpawnWeightsConfig)
                {
                    if (namespacedConfigWeight.NamespacedKey.Namespace == namespacedKey.Namespace && namespacedConfigWeight.NamespacedKey.Key == namespacedKey.Key)
                    {
                        interiorNamespacedKeyExists = true;
                        break;
                    }
                }

                if (!interiorNamespacedKeyExists)
                {
                    MathOperation mathOperation = MathOperation.Additive;
                    if (namespacedKey.Key == $"{modKey}_none")
                    {
                        mathOperation = MathOperation.Multiplicative;
                    }
                    NamespacedConfigWeight namespacedConfigWeight = new()
                    {
                        NamespacedKey = namespacedKey,
                        MathOperation = mathOperation,
                        Weight = 0f
                    };
                    enemyDefinition.InteriorSpawnWeightsConfig.Add(namespacedConfigWeight);
                }
            }

            foreach (NamespacedKey namespacedKey in weatherNamespacedKeysToAdd)
            {
                bool weatherNamespacedKeyExists = false;
                foreach (NamespacedConfigWeight namespacedConfigWeight in enemyDefinition.WeatherSpawnWeightsConfig)
                {
                    if (namespacedConfigWeight.NamespacedKey.Namespace == namespacedKey.Namespace && namespacedConfigWeight.NamespacedKey.Key == namespacedKey.Key)
                    {
                        weatherNamespacedKeyExists = true;
                        break;
                    }
                }

                if (!weatherNamespacedKeyExists)
                {
                    float weight = 1f;
                    if (namespacedKey.Key == $"{modKey}_none")
                    {
                        weight = 0f;
                    }
                    NamespacedConfigWeight namespacedConfigWeight = new()
                    {
                        NamespacedKey = namespacedKey,
                        MathOperation = MathOperation.Multiplicative,
                        Weight = weight
                    };
                    enemyDefinition.WeatherSpawnWeightsConfig.Add(namespacedConfigWeight);
                }
            }

            Undo.RecordObject(enemyDefinition, "Generate Default Empty Weights");
            EditorUtility.SetDirty(enemyDefinition);
        }
    }
}