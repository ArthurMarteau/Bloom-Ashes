using UnityEngine;

// Système pur de gestion du watersap — aucune dépendance MonoBehaviour
public class WatersapSystem
{
    // Ajoute amount au montant courant, clampé entre 0 et max
    public int CalculateNewAmount(int current, int amount, int max)
    {
        return Mathf.Clamp(current + amount, 0, max);
    }

    // Soustrait amount au montant courant, clampé entre 0 et max
    public int CalculateSpend(int current, int amount, int max)
    {
        return Mathf.Clamp(current - amount, 0, max);
    }

    // Vérifie si le joueur peut se permettre un coût donné
    public bool CanAfford(int current, int cost)
    {
        return current >= cost;
    }

    // Retourne le ratio de remplissage entre 0.0 et 1.0
    public float CalculateFillRatio(int current, int max)
    {
        if (max <= 0) return 0f;
        return Mathf.Clamp01((float)current / max);
    }
}
