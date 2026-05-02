using System.Collections.Generic;
using UnityEngine;

// Utilitaire de dispatch multi-cibles : répartit N unités sur N cases distinctes autour d'un point.
// Pas de couplage entre unités — produit juste une liste de cases candidates et un assignment greedy.
// Usage : OrderManager pour les ordres de mouvement/attaque groupés.
public static class DispatchHelper
{
    // Génère jusqu'à `count` cases walkable en spirale autour du centre.
    // Ordre : centre, puis anneau 1 (rayon 1), anneau 2 (rayon 2), etc.
    // Chaque case est testée via GridManager.IsWalkableFor(unit) — on prend la première unité comme référent
    // pour les capacités de terrain (eau, vol, etc.). Toutes les unités du groupe sont supposées avoir des capacités similaires.
    // Si moins de `count` cases libres trouvées dans un rayon raisonnable, retourne ce qu'on a.
    public static List<Vector2Int> ComputeSpiral(Vector2Int center, int count, GameObject referenceUnit)
    {
        List<Vector2Int> result = new List<Vector2Int>();

        if (count <= 0 || referenceUnit == null) return result;

        // Centre en premier si walkable
        if (GridManager.Instance.IsWalkableFor(center, referenceUnit))
        {
            result.Add(center);
            if (result.Count >= count) return result;
        }

        // Anneaux successifs
        const int MAX_RING = 10; // safety : 10 anneaux = 21x21 cases max
        for (int ring = 1; ring <= MAX_RING; ring++)
        {
            // Parcourt le périmètre de l'anneau de rayon `ring`
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    // Ne garde que le bord de l'anneau (Chebyshev = ring)
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;

                    Vector2Int cell = center + new Vector2Int(dx, dy);
                    if (!GridManager.Instance.IsInBounds(cell)) continue;
                    if (!GridManager.Instance.IsWalkableFor(cell, referenceUnit)) continue;

                    result.Add(cell);
                    if (result.Count >= count) return result;
                }
            }
        }

        return result;
    }

    // Assignment greedy : pour chaque unité (dans l'ordre fourni), prend la case disponible la plus proche
    // Retourne un dictionnaire unité → case assignée.
    // Si moins de cases que d'unités, les unités en surplus ne sont pas dans le dictionnaire (caller doit gérer le fallback).
    public static Dictionary<GameObject, Vector2Int> AssignCellsGreedy(List<GameObject> units, List<Vector2Int> cells)
    {
        Dictionary<GameObject, Vector2Int> assignment = new Dictionary<GameObject, Vector2Int>();
        if (units == null || units.Count == 0 || cells == null || cells.Count == 0) return assignment;

        HashSet<int> takenIndices = new HashSet<int>();

        foreach (GameObject unit in units)
        {
            if (unit == null) continue;

            MovementController mc = unit.GetComponent<MovementController>();
            Vector2Int unitPos = mc != null ? mc.GetCurrentGridPosition() : GridManager.Instance.WorldToGrid(unit.transform.position);

            int bestIdx = -1;
            int bestDist = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                if (takenIndices.Contains(i)) continue;
                int dist = ChebyshevDistance(unitPos, cells[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                assignment[unit] = cells[bestIdx];
                takenIndices.Add(bestIdx);
            }
        }

        return assignment;
    }

    // Distance Chebyshev (8-direction grid)
    private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
