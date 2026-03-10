using System;
using UnityEngine;

// Logique de combat pure — POCO, aucun MonoBehaviour, aucun GameObject, aucun Vector3
// 100% testable sans Unity : dépend uniquement de Vector2Int (type valeur Unity) et System
// Source unique de vérité pour tous les calculs de combat (portée, dégâts, positionnement)
// Prépare le serveur autoritaire : la logique pure peut tourner côté serveur
public static class CombatSystem
{
    // Vérifie si l'attaquant est à portée de la cible
    // Utilise la distance Chebyshev (portée en 8 directions, standard RTS en grille)
    public static bool IsInRange(Vector2Int attackerPos, Vector2Int targetPos, int range)
    {
        return CalculateChebyshevDistance(attackerPos, targetPos) <= range;
    }

    // Calcule la distance Chebyshev entre deux positions de grille
    // max(|dx|, |dy|) — correspond au nombre de cases en 8-directions
    // Source unique de vérité pour les calculs de portée dans tout le projet
    public static int CalculateChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));
    }

    // Calcule les dégâts effectifs après application de la défense
    // Formule : baseDamage - defense (minimum 1 — toute attaque inflige au moins 1 dégât)
    // Centralise la formule de dégâts — UnitStatsLogic.CalculateEffectiveDamage délègue ici
    public static int CalculateDamage(int baseDamage, int defense)
    {
        return Math.Max(1, baseDamage - defense);
    }

    // Trouve la meilleure position d'attaque dans la portée de targetPos
    // Itère les offsets de RangeOffsets, filtre via isWalkable, sélectionne la plus proche de currentPos
    // isWalkable : délégué injecté par l'appelant — permet de tester sans Unity
    //   Exemple d'usage : pos => GridManager.Instance.IsInBounds(pos) && GridManager.Instance.IsWalkableFor(pos, unit)
    // Retourne Vector2Int.zero si aucune position valide trouvée
    public static Vector2Int FindBestAttackPosition(
        Vector2Int targetPos,
        Vector2Int currentPos,
        int range,
        Func<Vector2Int, bool> isWalkable)
    {
        if (isWalkable == null)
        {
            return Vector2Int.zero;
        }

        Vector2Int[] offsets = RangeOffsets.GetOffsets(range);

        Vector2Int bestCell = Vector2Int.zero;
        int shortestDistance = int.MaxValue;

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int candidate = targetPos + offset;

            // Délègue la vérification d'accessibilité à l'appelant (injection de dépendance)
            if (!isWalkable(candidate)) continue;

            // Sélectionne la case la plus proche de la position actuelle par distance Chebyshev
            int distance = CalculateChebyshevDistance(currentPos, candidate);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                bestCell = candidate;
            }
        }

        return bestCell;
    }
}
