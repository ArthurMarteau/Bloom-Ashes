using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

// Script de renommage des sprites découpés dans les fichiers .meta
// Corrige le bug Unity où les sprites internes prennent le nom d'un autre asset
// Accès via clic droit sur un dossier → Bloom & Ashes > 1 - Rename Sprites
public class SpriteRenamer
{
    [MenuItem("Assets/Bloom & Ashes/1 - Rename Sprites", false, 1)]
    private static void RenameSprites()
    {
        // Récupère le dossier sélectionné dans le panneau Project
        string selectedPath = GetSelectedFolderPath();
        if (selectedPath == null)
        {
            EditorUtility.DisplayDialog("Erreur", "Veuillez sélectionner un dossier dans le panneau Project.", "OK");
            return;
        }

        // Confirmation avant de modifier les fichiers
        string folderName = Path.GetFileName(selectedPath);
        bool confirmed = EditorUtility.DisplayDialog(
            "Renommer les sprites",
            $"Renommer tous les sprites dans le dossier \"{folderName}\" ?\n\nCette opération modifie les fichiers .meta directement.",
            "Renommer", "Annuler"
        );

        if (!confirmed) return;

        // Traite tous les fichiers .meta de type spritesheet dans le dossier
        ProcessFolder(selectedPath);

        // Rafraîchit l'AssetDatabase pour que Unity prenne en compte les modifications
        AssetDatabase.Refresh();
        Debug.Log($"✅ SpriteRenamer: Renommage terminé pour le dossier {folderName}");
    }

    // Valide que la sélection est bien un dossier (pas un fichier)
    [MenuItem("Assets/Bloom & Ashes/1 - Rename Sprites", true)]
    private static bool ValidateRenameSprites()
    {
        return GetSelectedFolderPath() != null;
    }

    // Parcourt le dossier et traite chaque fichier *.png.meta (nouveau format sans -sheet)
    private static void ProcessFolder(string folderPath)
    {
        // Cherche tous les fichiers .meta dans le dossier (non récursif)
        string[] metaFiles = Directory.GetFiles(folderPath, "*.png.meta", SearchOption.TopDirectoryOnly);

        if (metaFiles.Length == 0)
        {
            Debug.LogWarning($"⚠️ SpriteRenamer: Aucun fichier *.png.meta trouvé dans {folderPath}");
            return;
        }

        int totalRenamed = 0;

        foreach (string metaPath in metaFiles)
        {
            int renamed = ProcessMetaFile(metaPath);
            totalRenamed += renamed;
        }

        Debug.Log($"✅ SpriteRenamer: {totalRenamed} sprites renommés dans {metaFiles.Length} fichiers");
    }

    // Traite un fichier .meta et renomme les sprites découpés
    // Extrait le nom correct depuis le nom du fichier parent
    // Retourne le nombre de sprites renommés
    private static int ProcessMetaFile(string metaPath)
    {
        // Récupère le nom du fichier PNG parent (sans .meta)
        string pngFileName = Path.GetFileNameWithoutExtension(metaPath); // ex: MycelianPriest_Attack_N.png
        string correctPrefix = Path.GetFileNameWithoutExtension(pngFileName); // ex: MycelianPriest_Attack_N

        // Lit le contenu du fichier .meta
        string content = File.ReadAllText(metaPath);

        // Trouve tous les noms de sprites existants dans le .meta
        // Format dans le .meta : "name: NomDuSprite"
        MatchCollection nameMatches = Regex.Matches(content, @"(?<=\n\s*name: ).+");

        if (nameMatches.Count == 0)
        {
            Debug.LogWarning($"⚠️ SpriteRenamer: Aucun sprite trouvé dans {Path.GetFileName(metaPath)}");
            return 0;
        }

        int renamedCount = 0;
        string updatedContent = content;

        // Renomme chaque sprite en utilisant le préfixe correct + index
        for (int i = 0; i < nameMatches.Count; i++)
        {
            string oldName = nameMatches[i].Value.Trim();
            string newName = $"{correctPrefix}_{i}";

            if (oldName == newName)
            {
                // Déjà correctement nommé, on skip
                continue;
            }

            // Remplace le nom dans le contenu (name: et nameFileIdTable)
            // Remplacement précis pour éviter les faux positifs
            updatedContent = Regex.Replace(
                updatedContent,
                $@"(?<=\n\s*name: ){Regex.Escape(oldName)}(?=\r?\n)",
                newName
            );

            // Met à jour aussi la nameFileIdTable en bas du .meta
            updatedContent = Regex.Replace(
                updatedContent,
                $@"(?<=\n\s*){Regex.Escape(oldName)}(?=: )",
                newName
            );

            Debug.Log($"🔄 SpriteRenamer: {Path.GetFileName(pngFileName)} → {oldName} ➜ {newName}");
            renamedCount++;
        }

        // Sauvegarde le fichier .meta modifié
        if (renamedCount > 0)
        {
            File.WriteAllText(metaPath, updatedContent);
            Debug.Log($"✅ SpriteRenamer: {Path.GetFileName(metaPath)} — {renamedCount} sprites renommés");
        }
        else
        {
            Debug.Log($"✔️ SpriteRenamer: {Path.GetFileName(metaPath)} — déjà correctement nommé");
        }

        return renamedCount;
    }

    // Retourne le chemin absolu du dossier sélectionné dans le panneau Project
    // Retourne null si la sélection n'est pas un dossier
    private static string GetSelectedFolderPath()
    {
        Object selected = Selection.activeObject;
        if (selected == null) return null;

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return null;

        // Vérifie que c'est bien un dossier
        if (!AssetDatabase.IsValidFolder(path)) return null;

        // Convertit le chemin Assets/ en chemin absolu sur disque
        return Path.Combine(Application.dataPath, "..", path).Replace("/", "\\");
    }
}
