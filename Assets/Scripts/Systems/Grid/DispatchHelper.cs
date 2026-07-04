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
                    if (CombatSystem.CalculateChebyshevDistance(Vector2Int.zero, new Vector2Int(dx, dy)) != ring) continue;

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

    // Génère cases walkable à distance Chebyshev EXACTE = `range` autour du centre.
    public static List<Vector2Int> ComputeRing(Vector2Int center, int range, GameObject referenceUnit)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (referenceUnit == null || range < 1) return result;

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (CombatSystem.CalculateChebyshevDistance(Vector2Int.zero, new Vector2Int(dx, dy)) != range) continue;

                Vector2Int cell = center + new Vector2Int(dx, dy);
                if (!GridManager.Instance.IsInBounds(cell)) continue;
                if (!GridManager.Instance.IsWalkableFor(cell, referenceUnit)) continue;
                result.Add(cell);
            }
        }
        return result;
    }

    // Génère cases d'approche pour une unité avec un range donné.
    // Priorité : anneau exact = range, fallback range-1, range+1, range-2, range+2...
    public static List<Vector2Int> ComputeRangeApproachCells(Vector2Int center, int range, int maxCells, GameObject referenceUnit)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (referenceUnit == null || maxCells <= 0) return result;

        int effectiveRange = Mathf.Max(1, range);

        result.AddRange(ComputeRing(center, effectiveRange, referenceUnit));
        if (result.Count >= maxCells) return result;

        const int MAX_FALLBACK_DELTA = 3;
        for (int delta = 1; delta <= MAX_FALLBACK_DELTA; delta++)
        {
            int lower = effectiveRange - delta;
            if (lower >= 1)
            {
                foreach (var cell in ComputeRing(center, lower, referenceUnit))
                    if (!result.Contains(cell)) result.Add(cell);
                if (result.Count >= maxCells) return result;
            }
            int upper = effectiveRange + delta;
            foreach (var cell in ComputeRing(center, upper, referenceUnit))
                if (!result.Contains(cell)) result.Add(cell);
            if (result.Count >= maxCells) return result;
        }
        return result;
    }

    // Assignment spirale : les cases sont tri�es par proximit� � la destination (centre en premier).
    // Pour chaque case dans cet ordre, on assigne l'unit� la plus proche de cette case.
    // Garantit que la case cliqu�e et l'anneau 1 sont pris en priorit� absolue.
    public static Dictionary<GameObject, Vector2Int> AssignCellsGreedy(
        List<GameObject> units, List<Vector2Int> cells, Vector2Int destination)
    {
        Dictionary<GameObject, Vector2Int> assignment = new Dictionary<GameObject, Vector2Int>();
        if (units == null || units.Count == 0 || cells == null || cells.Count == 0) return assignment;

        // Trie les cases par distance Chebyshev � la destination (spirale : centre d'abord)
        List<Vector2Int> sortedCells = new List<Vector2Int>(cells);
        sortedCells.Sort((a, b) =>
            CombatSystem.CalculateChebyshevDistance(a, destination)
                .CompareTo(CombatSystem.CalculateChebyshevDistance(b, destination)));

        HashSet<GameObject> assignedUnits = new HashSet<GameObject>();

        // Pour chaque case (du centre vers l'ext�rieur), prend l'unit� la plus proche
        foreach (Vector2Int cell in sortedCells)
        {
            if (assignedUnits.Count >= units.Count) break;

            GameObject bestUnit = null;
            int bestDist = int.MaxValue;

            foreach (GameObject unit in units)
            {
                if (unit == null || assignedUnits.Contains(unit)) continue;

                MovementController mc = unit.GetComponent<MovementController>();
                Vector2Int unitPos = mc != null
                    ? mc.GetCurrentGridPosition()
                    : GridManager.Instance.WorldToGrid(unit.transform.position);

                int dist = CombatSystem.CalculateChebyshevDistance(unitPos, cell);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestUnit = unit;
                }
            }

            if (bestUnit != null)
            {
                assignment[bestUnit] = cell;
                assignedUnits.Add(bestUnit);
            }
        }

        return assignment;
    }
}
