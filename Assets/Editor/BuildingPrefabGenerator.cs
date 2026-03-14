using UnityEngine;
using UnityEditor;
using System.IO;

// Script de génération automatique du prefab de bâtiment avec tous ses composants
// Accès via clic droit sur un dossier bâtiment → Bloom & Ashes > 5 - Generate Building Prefab
public class BuildingPrefabGenerator
{
    [MenuItem("Assets/Bloom & Ashes/5 - Generate Building Prefab", false, 5)]
    private static void GenerateBuildingPrefab()
    {
        string selectedAssetPath = GetSelectedFolderAssetPath();
        if (selectedAssetPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un dossier bâtiment dans le panneau Project.", "OK");
            return;
        }

        string buildingName = Path.GetFileName(selectedAssetPath);

        bool confirmed = EditorUtility.DisplayDialog(
            "Générer le prefab bâtiment",
            $"Générer le prefab pour \"{buildingName}\" ?\n\nUn prefab et un BuildingStatsData seront créés ou mis à jour.",
            "Générer", "Annuler"
        );

        if (!confirmed) return;

        // Vérifie que le layer Obstacle existe
        if (!LayerExists("Obstacle"))
        {
            EditorUtility.DisplayDialog("Erreur", "Le layer \"Obstacle\" n'existe pas. Ajoutez-le dans Edit > Project Settings > Tags and Layers.", "OK");
            return;
        }

        // Cherche un sprite de preview (premier sprite disponible dans le dossier)
        Sprite previewSprite = FindFirstSprite(selectedAssetPath, buildingName);
        if (previewSprite == null)
            Debug.LogWarning($"⚠️ BuildingPrefabGenerator: Aucun sprite trouvé dans {selectedAssetPath}");

        // Charge le material
        Material teamColorMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TeamColorMaterial.mat");
        if (teamColorMaterial == null)
            Debug.LogWarning("⚠️ BuildingPrefabGenerator: TeamColorMaterial introuvable dans Assets/Materials/.");

        // Charge l'Animator Controller si disponible (optionnel pour les bâtiments)
        string animatorPath = $"Assets/Animation/Buildings/{buildingName}/{buildingName}Animator.controller";
        RuntimeAnimatorController animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animatorPath);
        if (animatorController == null)
            Debug.Log($"ℹ️ BuildingPrefabGenerator: Pas d'Animator Controller pour {buildingName} (normal si bâtiment statique).");

        // Crée ou charge le BuildingStatsData
        BuildingStatsData statsData = GetOrCreateStatsData(buildingName);

        // Crée les dossiers prefab si nécessaire
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Buildings"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Buildings");

        // Crée le GameObject
        GameObject go = new GameObject(buildingName);

        // Layer Obstacle (pas de tag spécifique pour les bâtiments)
        go.layer = LayerMask.NameToLayer("Obstacle");

        // SpriteRenderer
        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = previewSprite;
        if (teamColorMaterial != null)
            spriteRenderer.material = teamColorMaterial;
        spriteRenderer.sortingLayerName = "Obstacle";

        // Animator (conditionnel)
        if (animatorController != null)
        {
            Animator animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
        }

        // BoxCollider2D
        BoxCollider2D boxCollider = go.AddComponent<BoxCollider2D>();
        boxCollider.size   = new Vector2(1f, 1f);
        boxCollider.offset = new Vector2(0f, 0f);

        // Scripts — TeamComponent en premier (BuildingStatsComponent en a besoin dans Awake)
        go.AddComponent<TeamComponent>();

        BuildingStatsComponent buildingStatsComponent = go.AddComponent<BuildingStatsComponent>();
        if (statsData != null)
        {
            SerializedObject serializedStats = new SerializedObject(buildingStatsComponent);
            SerializedProperty baseStatsProp = serializedStats.FindProperty("baseStats");
            if (baseStatsProp != null)
            {
                baseStatsProp.objectReferenceValue = statsData;
                serializedStats.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("⚠️ BuildingPrefabGenerator: Champ 'baseStats' introuvable sur BuildingStatsComponent.");
            }
        }

        go.AddComponent<HighlightTarget>();

        // Sauvegarde le prefab
        string prefabPath = $"Assets/Prefabs/Buildings/{buildingName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ BuildingPrefabGenerator: Prefab créé → {prefabPath}");
        EditorUtility.DisplayDialog("Succès", $"Prefab \"{buildingName}\" généré avec succès !", "OK");
    }

    // Crée ou charge le BuildingStatsData dans Assets/Data/Buildings/
    private static BuildingStatsData GetOrCreateStatsData(string buildingName)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
            AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/Data/Buildings"))
            AssetDatabase.CreateFolder("Assets/Data", "Buildings");

        string statsPath = $"Assets/Data/Buildings/{buildingName}_Stats.asset";
        BuildingStatsData statsData = AssetDatabase.LoadAssetAtPath<BuildingStatsData>(statsPath);

        if (statsData == null)
        {
            statsData = ScriptableObject.CreateInstance<BuildingStatsData>();
            AssetDatabase.CreateAsset(statsData, statsPath);
            Debug.Log($"📊 BuildingPrefabGenerator: BuildingStatsData créé → {statsPath}");
        }
        else
        {
            Debug.Log($"📊 BuildingPrefabGenerator: BuildingStatsData existant réutilisé → {statsPath}");
        }

        return statsData;
    }

    // Cherche le premier sprite disponible dans le dossier (preview du bâtiment)
    private static Sprite FindFirstSprite(string folderPath, string buildingName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite)
                    return sprite;
            }
        }
        return null;
    }

    [MenuItem("Assets/Bloom & Ashes/5 - Generate Building Prefab", true)]
    private static bool ValidateGenerateBuildingPrefab()
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

    // Vérifie qu'un layer existe dans le projet
    private static bool LayerExists(string layer)
    {
        return LayerMask.NameToLayer(layer) != -1;
    }
}
