using UnityEngine;

// Centralise les offsets de portée utilisés par AttackController et ChaseController
// Evite la duplication de ces tableaux entre les deux controllers
public static class RangeOffsets
{
    // Range 1 : Mêlée - 8 cases adjacentes
    private static readonly Vector2Int[] range1Offsets = {
        new Vector2Int(0, 1),   // Nord
        new Vector2Int(1, 0),   // Est
        new Vector2Int(0, -1),  // Sud
        new Vector2Int(-1, 0),  // Ouest
        new Vector2Int(1, 1),   // Nord-Est
        new Vector2Int(1, -1),  // Sud-Est
        new Vector2Int(-1, -1), // Sud-Ouest
        new Vector2Int(-1, 1)   // Nord-Ouest
    };

    // Range 2 : Portée 1 case - 20 cases (carré 5x5 sans centre)
    private static readonly Vector2Int[] range2Offsets = {
        // Reprend range1
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
        // Deuxième couronne
        new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(0, -2), new Vector2Int(-2, 0),
        new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2)
    };

    // Range 3 : Portée 2 cases - 36 cases (carré 7x7 sans centre et coins extrêmes)
    private static readonly Vector2Int[] range3Offsets = {
        // Reprend range2
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
        new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(0, -2), new Vector2Int(-2, 0),
        new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
        // Troisième couronne
        new Vector2Int(0, 3), new Vector2Int(3, 0), new Vector2Int(0, -3), new Vector2Int(-3, 0),
        new Vector2Int(1, 3), new Vector2Int(3, 1), new Vector2Int(3, -1), new Vector2Int(1, -3),
        new Vector2Int(-1, -3), new Vector2Int(-3, -1), new Vector2Int(-3, 1), new Vector2Int(-1, 3),
        new Vector2Int(2, 2), new Vector2Int(2, -2), new Vector2Int(-2, -2), new Vector2Int(-2, 2)
    };

    // Retourne le tableau d'offsets correspondant à la portée demandée
    // Range 1 = mêlée (8 cases), Range 2 = portée courte (20 cases), Range 3 = portée longue (36 cases)
    // Toute valeur non reconnue retourne range1 avec un avertissement
    public static Vector2Int[] GetOffsets(int range)
    {
        switch (range)
        {
            case 1: return range1Offsets;
            case 2: return range2Offsets;
            case 3: return range3Offsets;
            default:
                GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ RangeOffsets.GetOffsets: portée {range} non supportée, utilisation range 1", null);
                return range1Offsets;
        }
    }
}
