using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.IO;
using System.Collections.Generic;

// Script d'ajustement automatique des pivots sur les frames d'attaque
// Accès via clic droit sur un dossier unité → Bloom & Ashes > 4 - Adjust Attack Pivots
public class SpritePivotAdjuster
{
    // Pivots par direction pour les frames 4 à 7
    private static readonly Dictionary<string, Vector2> ATTACK_PIVOTS = new Dictionary<string, Vector2>
    {
        { "N",  new Vector2(0.5f, 0.4f) },
        { "NW", new Vector2(0.6f, 0.4f) },
        { "S",  new Vector2(0.5f, 0.6f) },
        { "SW", new Vector2(0.6f, 0.6f) },
        { "W",  new Vector2(0.6f, 0.5f) },
    };

    [MenuItem("Assets/Bloom & Ashes/4 - Adjust Attack Pivots", false, 4)]
    private static void AdjustPivots()
    {
        string selectedAssetPath = GetSelectedFolderAssetPath();
        if (selectedAssetPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un dossier unité dans le panneau Project.", "OK");
            return;
        }

        string unitName = Path.GetFileName(selectedAssetPath);

        bool confirmed = EditorUtility.DisplayDialog(
            "Ajuster les pivots",
            $"Ajuster les pivots Attack (frames 1-4) pour \"{unitName}\" ?",
            "Ajuster", "Annuler"
        );

        if (!confirmed) return;

        int modifiedCount = 0;

        foreach (var entry in ATTACK_PIVOTS)
        {
            string direction = entry.Key;
            Vector2 pivot = entry.Value;

            string sheetPath = $"{selectedAssetPath}/{unitName}_Attack_{direction}.png";
            TextureImporter importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning($"⚠️ SpritePivotAdjuster: Spritesheet introuvable : {sheetPath}");
                continue;
            }

            // Utilise ISpriteEditorDataProvider pour éviter l'API obsolète spritesheet
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
            bool changed = false;

            for (int i = 0; i < spriteRects.Length; i++)
            {
                // Cible uniquement les frames _1 à _4
                string spriteName = spriteRects[i].name;
                string[] parts = spriteName.Split('_');
                string suffix = parts.Length > 0 ? parts[parts.Length - 1] : "";

                if (suffix == "1" || suffix == "2" || suffix == "3" || suffix == "4")
                {
                    spriteRects[i].pivot = pivot;
                    spriteRects[i].alignment = SpriteAlignment.Custom;
                    changed = true;
                    modifiedCount++;
                    Debug.Log($"🎯 SpritePivotAdjuster: Pivot ajusté → {spriteName} ({pivot.x}, {pivot.y})");
                }
            }

            if (changed)
            {
                dataProvider.SetSpriteRects(spriteRects);
                dataProvider.Apply();
                importer.SaveAndReimport();
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ SpritePivotAdjuster: {modifiedCount} pivots ajustés pour {unitName}");
        EditorUtility.DisplayDialog("Succès", $"{modifiedCount} pivots ajustés pour \"{unitName}\".", "OK");
    }

    [MenuItem("Assets/Bloom & Ashes/4 - Adjust Attack Pivots", true)]
    private static bool ValidateAdjustPivots()
    {
        return GetSelectedFolderAssetPath() != null;
    }

    private static string GetSelectedFolderAssetPath()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return null;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return null;

        if (!AssetDatabase.IsValidFolder(path)) return null;

        return path;
    }
}
