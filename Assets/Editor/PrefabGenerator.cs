using UnityEngine;
using UnityEditor;
using System.IO;

// Script de génération automatique du prefab d'unité avec tous ses composants
// Accès via clic droit sur un dossier unité → Bloom & Ashes > 3 - Generate Unit Prefab
public class PrefabGenerator
{
    [MenuItem("Assets/Bloom & Ashes/3 - Generate Unit Prefab", false, 3)]
    private static void GeneratePrefab()
    {
        // Récupère le dossier sélectionné
        string selectedAssetPath = GetSelectedFolderAssetPath();
        if (selectedAssetPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un dossier unité dans le panneau Project.", "OK");
            return;
        }

        string unitName = Path.GetFileName(selectedAssetPath);

        bool confirmed = EditorUtility.DisplayDialog(
            "Générer le prefab",
            $"Générer le prefab pour \"{unitName}\" ?\n\nUn prefab et un UnitStatsData seront créés ou mis à jour.",
            "Générer", "Annuler"
        );

        if (!confirmed) return;

        // Vérifie que le layer existe
        if (!LayerExists("Units"))
        {
            EditorUtility.DisplayDialog("Erreur", "Le layer \"Units\" n'existe pas. Ajoutez-le dans Edit > Project Settings > Tags and Layers.", "OK");
            return;
        }

        // Charge le sprite Idle_SW_0
        string spritePath = $"{selectedAssetPath}/{unitName}_Idle_SW/{unitName}_Idle_SW_0";
        Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{selectedAssetPath}/{unitName}_Idle_SW.png");

        // Cherche le sprite nommé exactement "{unitName}_Idle_SW_0" dans le spritesheet
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath($"{selectedAssetPath}/{unitName}_Idle_SW.png");
        foreach (Object asset in allAssets)
        {
            if (asset is Sprite s && s.name == $"{unitName}_Idle_SW_0")
            {
                idleSprite = s;
                break;
            }
        }

        if (idleSprite == null)
            Debug.LogWarning($"⚠️ PrefabGenerator: Sprite {unitName}_Idle_SW_0 introuvable, SpriteRenderer sans sprite.");

        // Charge le material
        Material teamColorMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TeamColorMaterial.mat");
        if (teamColorMaterial == null)
            Debug.LogWarning("⚠️ PrefabGenerator: TeamColorMaterial introuvable dans Assets/Materials/.");

        // Charge l'Animator Controller
        string animatorPath = $"Assets/Animation/Units/{unitName}/{unitName}Animator.controller";
        RuntimeAnimatorController animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorPath);
        if (animatorController == null)
            Debug.LogWarning($"⚠️ PrefabGenerator: Animator Controller introuvable : {animatorPath}");

        // Crée ou charge le UnitStatsData
        UnitStatsData statsData = GetOrCreateStatsData(unitName);

        // Crée les dossiers prefab si nécessaire
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Units"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Units");

        // Crée le GameObject
        GameObject go = new GameObject(unitName);

        // Layer
        go.layer = LayerMask.NameToLayer("Units");

        // SpriteRenderer
        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = idleSprite;
        if (teamColorMaterial != null)
            spriteRenderer.material = teamColorMaterial;
        spriteRenderer.sortingLayerName = "Units";

        // Animator
        Animator animator = go.AddComponent<Animator>();
        if (animatorController != null)
            animator.runtimeAnimatorController = animatorController;

        // BoxCollider2D
        BoxCollider2D boxCollider = go.AddComponent<BoxCollider2D>();
        boxCollider.size = new Vector2(1f, 1f);
        boxCollider.offset = new Vector2(0f, 0f);

        // Scripts
        go.AddComponent<AnimatorController>();
        go.AddComponent<AttackController>();
        go.AddComponent<ChaseController>();
        go.AddComponent<MovementController>();
        go.AddComponent<TeamComponent>();

        UnitStatsComponent unitStatsComponent = go.AddComponent<UnitStatsComponent>();
        if (statsData != null)
        {
            SerializedObject serializedStats = new SerializedObject(unitStatsComponent);
            SerializedProperty baseStatsProp = serializedStats.FindProperty("baseStats");
            if (baseStatsProp != null)
            {
                baseStatsProp.objectReferenceValue = statsData;
                serializedStats.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("⚠️ PrefabGenerator: Champ 'baseStats' introuvable sur UnitStatsComponent.");
            }
        }

        go.AddComponent<HighlightTarget>();

        // Sauvegarde le prefab
        string prefabPath = $"Assets/Prefabs/Units/{unitName}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ PrefabGenerator: Prefab créé → {prefabPath}");
        EditorUtility.DisplayDialog("Succès", $"Prefab \"{unitName}\" généré avec succès !", "OK");
    }

    // Crée ou charge le UnitStatsData dans Assets/Data/Units/
    private static UnitStatsData GetOrCreateStatsData(string unitName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Units"))
            AssetDatabase.CreateFolder("Assets/Data", "Units");

        string statsPath = $"Assets/Data/Units/{unitName}_Stats.asset";
        UnitStatsData statsData = AssetDatabase.LoadAssetAtPath<UnitStatsData>(statsPath);

        if (statsData == null)
        {
            statsData = ScriptableObject.CreateInstance<UnitStatsData>();
            AssetDatabase.CreateAsset(statsData, statsPath);
            Debug.Log($"📊 PrefabGenerator: UnitStatsData créé → {statsPath}");
        }
        else
        {
            Debug.Log($"📊 PrefabGenerator: UnitStatsData existant réutilisé → {statsPath}");
        }

        return statsData;
    }

    // Valide que la sélection est bien un dossier
    [MenuItem("Assets/Bloom & Ashes/3 - Generate Unit Prefab", true)]
    private static bool ValidateGeneratePrefab()
    {
        return GetSelectedFolderAssetPath() != null;
    }

    // Retourne le chemin Assets/ du dossier sélectionné dans le panneau Project
    private static string GetSelectedFolderAssetPath()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return null;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return null;

        if (!AssetDatabase.IsValidFolder(path)) return null;

        return path;
    }

    // Vérifie qu'un tag existe dans le projet
    private static bool TagExists(string tag)
    {
        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == tag) return true;
        return false;
    }

    // Vérifie qu'un layer existe dans le projet
    private static bool LayerExists(string layer)
    {
        return LayerMask.NameToLayer(layer) != -1;
    }
}
