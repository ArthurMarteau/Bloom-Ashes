using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.IO;
using System.Collections.Generic;

// Script d'ajustement automatique des pivots sur les frames d'attaque
// Accès via clic droit sur un dossier unité → Bloom & Ashes > 4 - Adjust Attack Pivots
public class SpritePivotAdjuster
{
    // Pivot par défaut (frame _1 inchangée)
    private static readonly Vector2 DEFAULT_PIVOT = new Vector2(0.5f, 0.5f);

    // Pivots par direction pour les frames _2 à _6
    // NE et SE utilisent le même asset que NW et SW (miroir), donc non listés séparément
    private static readonly Dictionary<string, Vector2> ATTACK_PIVOTS = new Dictionary<string, Vector2>
    {
        { "N",  new Vector2(0.5f,  0.4f)  },
        { "S",  new Vector2(0.5f,  0.6f)  },
        { "NW", new Vector2(0.55f, 0.45f) },
        { "SW", new Vector2(0.55f, 0.55f) },
        { "W",  new Vector2(0.6f,  0.5f)  },
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
            $"Ajuster les pivots Attack (frames _2 à _6) pour \"{unitName}\" ?",
            "Ajuster", "Annuler"
        );

        if (!confirmed) return;

        int modifiedCount = 0;
        int sheetsFound = 0;

        foreach (var entry in ATTACK_PIVOTS)
        {
            string direction = entry.Key;
            Vector2 pivot = entry.Value;

            // Détection automatique du fichier : cherche *_Attack_{direction}.png dans le dossier
            string[] guids = AssetDatabase.FindAssets($"t:Texture2D", new[] { selectedAssetPath });
            string sheetPath = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileNameWithoutExtension(path);

                if (filename.EndsWith($"_Attack_{direction}"))
                {
                    sheetPath = path;
                    break;
                }
            }

            if (sheetPath == null)
            {
                Debug.LogWarning($"⚠️ SpritePivotAdjuster: Spritesheet introuvable pour la direction {direction} dans {selectedAssetPath}");
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"⚠️ SpritePivotAdjuster: Impossible d'obtenir le TextureImporter pour : {sheetPath}");
                continue;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
            bool changed = false;

            foreach (SpriteRect rect in spriteRects)
            {
                string spriteName = rect.name;
                string[] parts = spriteName.Split('_');
                if (parts.Length == 0) continue;

                string suffix = parts[parts.Length - 1];

                if (suffix == "1")
                {
                    // Frame _1 : pivot par défaut
                    rect.pivot = DEFAULT_PIVOT;
                    rect.alignment = SpriteAlignment.Center;
                    changed = true;
                    Debug.Log($"⬜ SpritePivotAdjuster: Pivot par défaut → {spriteName} (0.5, 0.5)");
                }
                else if (suffix == "2" || suffix == "3" || suffix == "4" || suffix == "5" || suffix == "6")
                {
                    // Frames _2 à _6 : pivot selon la direction
                    rect.pivot = pivot;
                    rect.alignment = SpriteAlignment.Custom;
                    changed = true;
                    modifiedCount++;
                    Debug.Log($"🎯 SpritePivotAdjuster: Pivot ajusté → {spriteName} ({pivot.x}, {pivot.y})");
                }
            }

            if (changed)
            {
                sheetsFound++;
                dataProvider.SetSpriteRects(spriteRects);
                dataProvider.Apply();
                importer.SaveAndReimport();
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ SpritePivotAdjuster: {modifiedCount} pivots ajustés sur {sheetsFound} spritesheets pour {unitName}");
        EditorUtility.DisplayDialog("Succès", $"{modifiedCount} pivots ajustés sur {sheetsFound} spritesheets pour \"{unitName}\".", "OK");
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
