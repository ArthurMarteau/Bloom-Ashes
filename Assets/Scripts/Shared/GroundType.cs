using UnityEngine;

// Types de terrain qui influencent l'accessibilité des unités
// Correspondent aux tuiles visibles sur le layer Ground dans Unity
// Configuration statique définie au démarrage de la partie
public enum GroundType
{
    Ground,    // Terrain normal - accessible à toutes les unités terrestres
    Water,     // Terrain aquatique - accessible seulement aux WaterUnit et AirUnit
    Mountain   // Terrain montagneux - accessible seulement aux MountainUnit et AirUnit
}

// Classe utilitaire pour la gestion des types de terrain
// Fournit des méthodes helper pour affichage et debug
// Utilisée par GridManager et outils de développement
public static class GroundTypeExtensions
{
    // Retourne une description lisible du type de terrain
    // Utilisé pour debug et interface utilisateur
    // Facilite l'identification dans les logs et outils
    public static string GetDisplayName(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Ground:
                return "Terrain Normal";
            case GroundType.Water:
                return "Eau";
            case GroundType.Mountain:
                return "Montagne";
            default:
                return "Terrain Inconnu";
        }
    }

    // Retourne la couleur associée au terrain pour debug visuel
    // Utilisé pour affichage dans l'éditeur et outils de développement
    // Permet de visualiser rapidement les zones de terrain
    public static Color GetDebugColor(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Ground:
                return Color.green;    // Vert pour terrain normal
            case GroundType.Water:
                return Color.blue;     // Bleu pour eau
            case GroundType.Mountain:
                return Color.gray;     // Gris pour montagne
            default:
                return Color.white;    // Blanc pour type inconnu
        }
    }

    // Retourne une description des unités pouvant accéder à ce terrain
    // Utilisé pour documentation et aide au joueur
    // Explique quels types d'unités peuvent traverser ce terrain
    public static string GetAccessibleUnits(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Ground:
                return "Toutes les unités";
            case GroundType.Water:
                return "Unités Aquatiques et Aériennes";
            case GroundType.Mountain:
                return "Unités Montagnardes et Aériennes";
            default:
                return "Aucune unité";
        }
    }
}