using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

// Script de découpe automatique des spritesheets via Grid By Cell Count
// Invoque directement le Sprite Editor interne d'Unity pour découper
// Accès via clic droit sur un dossier → Bloom & Ashes > 3 - Slice Sprites
public class SpriteSlicer
{
    [MenuItem("Assets/Bloom & Ashes/3 - Slice Sprites", false, 3)]
    private static void SliceSprites()
    {
        string selectedAssetPath = GetSelectedFolderAssetPath();
        if (selectedAssetPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un dossier unité dans le panneau Project.", "OK");
            return;
        }

        string unitName = Path.GetFileName(selectedAssetPath);

        bool confirmed = EditorUtility.DisplayDialog(
            "Découper les sprites",
            $"Découper tous les spritesheets de \"{unitName}\" via Grid By Cell Count ?\n\nN colonnes détectées automatiquement, 1 ligne.",
            "Découper", "Annuler"
        );

        if (!confirmed) return;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { selectedAssetPath });
        int modifiedCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!assetPath.EndsWith(".png")) continue;

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;

            // Lit les dimensions via GetSourceTextureWidthAndHeight (pas besoin de charger la texture)
            importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
            if (textureHeight == 0) continue;

            int columnCount = textureWidth / textureHeight;
            if (columnCount <= 0)
            {
                Debug.LogWarning($"⚠️ SpriteSlicer: colonnes indéterminées pour {assetPath}");
                continue;
            }

            int frameWidth  = textureWidth  / columnCount;
            int frameHeight = textureHeight;

            // Passe en mode Multiple
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode       = FilterMode.Point;

            // Génère les SpriteMetaData via Grid By Cell Count (même logique qu'Unity en interne)
            string spriteName = Path.GetFileNameWithoutExtension(assetPath);
            SpriteMetaData[] metaData = new SpriteMetaData[columnCount];

            for (int col = 0; col < columnCount; col++)
            {
                metaData[col] = new SpriteMetaData
                {
                    name      = $"{spriteName}_{col + 1}",
                    rect      = new Rect(col * frameWidth, 0, frameWidth, frameHeight),
                    pivot     = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center,
                    border    = Vector4.zero,
                };
            }

#pragma warning disable CS0618
            importer.spritesheet = metaData;
#pragma warning restore CS0618

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            modifiedCount++;
            Debug.Log($"✂️ SpriteSlicer: {spriteName} → {columnCount} colonnes ({frameWidth}×{frameHeight}px)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✅ SpriteSlicer: {modifiedCount} spritesheets découpés pour {unitName}");
        EditorUtility.DisplayDialog("Succès", $"{modifiedCount} spritesheets découpés pour \"{unitName}\".", "OK");
    }

    [MenuItem("Assets/Bloom & Ashes/3 - Slice Sprites", true)]
    private static bool ValidateSliceSprites()
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
}
