using System.Collections.Generic;
using UnityEngine;

// Registre centralisé de toutes les entités IDamageable actives dans la scène
// Remplace Physics2D.OverlapBoxAll pour la recherche d'ennemis — critique pour 80x80 avec 50+ unités
// MonoBehaviour singleton (PAS static) pour compatibilité netcode future
// Les entités s'enregistrent/désenregistrent automatiquement via leurs Awake/OnDestroy
// Priorité d'exécution négative : s'initialise avant UnitStatsComponent et BuildingStatsComponent
[DefaultExecutionOrder(-100)]
public class EntityRegistry : MonoBehaviour
{
    // Instance singleton accessible globalement
    public static EntityRegistry Instance { get; private set; }

    // Stockage central : GameObject → IDamageable
    // Dictionary pour accès O(1) à l'enregistrement/désenregistrement
    private Dictionary<GameObject, IDamageable> entities = new Dictionary<GameObject, IDamageable>();

    // Registre des positions logiques : position grille source de vérité, indépendante du Lerp visuel
    // Mis à jour par MovementController à chaque étape logique
    private Dictionary<GameObject, Vector2Int> logicalPositions = new Dictionary<GameObject, Vector2Int>();

    // Cases en cours d'abandon : unité logiquement partie mais Lerp visuel pas encore terminé
    // Permet à une autre unité de s'y engager sans attendre la fin du Lerp
    private Dictionary<GameObject, Vector2Int> leavingPositions = new Dictionary<GameObject, Vector2Int>();
    private Dictionary<Vector2Int, GameObject> positionToUnit = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> leavingPositionToUnit = new Dictionary<Vector2Int, GameObject>();

    [Header("Debug")]
    [SerializeField] private bool debugRegistry = false;

    // Initialise le singleton en priorité (-100) avant les composants d'entités
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("📋 EntityRegistry: Initialisé et prêt à recevoir les entités");
    }

    // Nettoie la référence singleton lors de la destruction
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // === API PUBLIQUE ===

    // Enregistre une entité IDamageable dans le registre
    // Appelé dans Awake() de UnitStatsComponent et BuildingStatsComponent
    public void RegisterEntity(IDamageable entity, GameObject go)
    {
        if (go == null || entity == null)
        {
            Debug.LogWarning("📋 EntityRegistry.RegisterEntity: go ou entity est null — enregistrement ignoré");
            return;
        }

        // Écrase silencieusement si déjà présent (re-activation d'objet poolé par ex.)
        entities[go] = entity;

        if (debugRegistry)
        {
            Debug.Log($"📋 EntityRegistry: ✅ {go.name} enregistré ({entities.Count} entités au total)");
        }
    }

    // Désenregistre une entité du registre
    // Appelé dans OnDestroy() de UnitStatsComponent et BuildingStatsComponent
    public void UnregisterEntity(GameObject go)
    {
        if (go == null) return;

        entities.Remove(go);
        if (logicalPositions.TryGetValue(go, out var lp) && positionToUnit.TryGetValue(lp, out var rl) && rl == go)
        {
            positionToUnit.Remove(lp);
        }
        logicalPositions.Remove(go);
        if (leavingPositions.TryGetValue(go, out var leP) && leavingPositionToUnit.TryGetValue(leP, out var reL) && reL == go)
        {
            leavingPositionToUnit.Remove(leP);
        }
        leavingPositions.Remove(go);

        if (debugRegistry)
        {
            Debug.Log($"📋 EntityRegistry: ❌ {go.name} désenregistré ({entities.Count} entités restantes)");
        }
    }

    // Met à jour la position logique d'une unité dans le registre
    // Appelé par MovementController à chaque étape logique (avant le Lerp visuel)
    public void UpdateLogicalPosition(GameObject go, Vector2Int position)
    {
        if (go == null) return;

        if (logicalPositions.TryGetValue(go, out var oldPos) && positionToUnit.TryGetValue(oldPos, out var registered) && registered == go)
        {
            positionToUnit.Remove(oldPos);
        }

        logicalPositions[go] = position;

        if (positionToUnit.TryGetValue(position, out var existing) && existing != go && existing != null)
        {
            Debug.LogWarning($" EntityRegistry: collision logique sur {position} entre {existing.name} et {go.name}");
        }
        positionToUnit[position] = go;
    }

    // Enregistre qu'une unité est en train de quitter une case (Lerp pas encore terminé)
    public void SetLeaving(GameObject go, Vector2Int position)
    {
        if (go == null) return;
        leavingPositions[go] = position;
        leavingPositionToUnit[position] = go;
    }

    // Retire l'entrée de départ quand le Lerp est terminé
    public void ClearLeaving(GameObject go)
    {
        if (go == null) return;
        if (leavingPositions.TryGetValue(go, out var leftPos) && leavingPositionToUnit.TryGetValue(leftPos, out var registered) && registered == go)
        {
            leavingPositionToUnit.Remove(leftPos);
        }
        leavingPositions.Remove(go);
    }

    // Libère la position logique d'une entité sans la désenregistrer du registry
    // Appelée à la mort d'une unité : la case devient walkable immédiatement,
    // mais l'entité reste dans `entities` pendant l'animation de mort (filtrée par IsDead() ailleurs)
    public void ReleaseLogicalPosition(GameObject go)
    {
        if (go == null) return;

        if (logicalPositions.TryGetValue(go, out var lp) && positionToUnit.TryGetValue(lp, out var rl) && rl == go)
        {
            positionToUnit.Remove(lp);
        }
        logicalPositions.Remove(go);

        if (leavingPositions.TryGetValue(go, out var leP) && leavingPositionToUnit.TryGetValue(leP, out var reL) && reL == go)
        {
            leavingPositionToUnit.Remove(leP);
        }
        leavingPositions.Remove(go);
    }

    // Retourne true si une unité est en train de quitter cette case (case contestable) (reverse map O(1))
    public bool IsBeingLeft(Vector2Int position)
    {
        return leavingPositionToUnit.TryGetValue(position, out GameObject go) && go != null;
    }

    // Retourne l'unité occupant une position logique donnée (null si aucune) (reverse map O(1))
    // Fiable pendant le Lerp contrairement à Physics2D.OverlapPoint
    public GameObject GetUnitAtLogicalPosition(Vector2Int position)
    {
        if (positionToUnit.TryGetValue(position, out GameObject go) && go != null) return go;
        return null;
    }

    // Retourne tous les GameObjects dans un rayon (distance Chebyshev) autour d'un centre de grille
    // Inclut les entités mortes — à filtrer par l'appelant si nécessaire
    // Performance : O(n) sur les entités enregistrées, zéro appel Physics2D
    public List<GameObject> FindEntitiesInRange(Vector2Int center, int range)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (var kvp in entities)
        {
            GameObject go = kvp.Key;
            if (go == null) continue;

            Vector2Int entityPosition = GridManager.Instance.WorldToGrid(go.transform.position);
            int distance = ChebyshevDistance(center, entityPosition);

            if (distance <= range)
            {
                result.Add(go);
            }
        }

        return result;
    }

    // Retourne les ennemis vivants de myTeamID dans un rayon (distance Chebyshev)
    // Filtre automatiquement : vivant + équipe différente + non-neutre + dans la portée
    // Utilisé par AttackController pour remplacer Physics2D.OverlapBoxAll
    public List<GameObject> FindEnemiesInRange(Vector2Int center, int range, int myTeamID)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (var kvp in entities)
        {
            GameObject go = kvp.Key;
            IDamageable damageable = kvp.Value;

            // Saute les entrées invalides, mortes ou invulnérables
            // Les entités invulnérables (ex: Nexus) ne doivent pas être ciblées automatiquement
            if (go == null || damageable == null) continue;
            if (damageable.IsDead()) continue;
            if (damageable.IsInvulnerable()) continue;

            // Vérifie l'équipe via ITeamMember
            ITeamMember otherTeamMember = go.GetComponent<ITeamMember>();
            if (otherTeamMember == null) continue;

            int otherTeamID = otherTeamMember.GetTeamID();

            // Neutres (0) ne sont ennemis de personne
            if (myTeamID == 0 || otherTeamID == 0) continue;

            // Même équipe = allié, pas ennemi
            if (otherTeamID == myTeamID) continue;

            // Vérifie la distance Chebyshev
            Vector2Int entityPosition = GridManager.Instance.WorldToGrid(go.transform.position);
            int distance = ChebyshevDistance(center, entityPosition);

            if (distance <= range)
            {
                result.Add(go);
            }
        }

        return result;
    }

    // Retourne tous les GameObjects actuellement enregistrés (vivants ET morts)
    // Utile pour les systèmes de debug et les futurs systèmes globaux (minimap, etc.)
    public List<GameObject> GetAllEntities()
    {
        return new List<GameObject>(entities.Keys);
    }

    // Retourne le nombre d'entités enregistrées
    public int GetEntityCount()
    {
        return entities.Count;
    }

    // === Utilitaires ===

    // Distance Chebyshev : délègue à CombatSystem — source unique de vérité
    private int ChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return CombatSystem.CalculateChebyshevDistance(a, b);
    }

    // Supprime les entrées dont le GameObject a été détruit sans OnDestroy propre
    // Filet de sécurité — normalement géré par UnregisterEntity()
    private void CleanupNullEntities()
    {
        List<GameObject> toRemove = new List<GameObject>();
        List<GameObject> logicalPositionsToRemove = new List<GameObject>();
        List<GameObject> leavingPositionsToRemove = new List<GameObject>();

        foreach (var kvp in entities)
        {
            if (kvp.Key == null)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var kvp in logicalPositions)
        {
            if (kvp.Key == null)
            {
                logicalPositionsToRemove.Add(kvp.Key);
            }
        }

        foreach (var kvp in leavingPositions)
        {
            if (kvp.Key == null)
            {
                leavingPositionsToRemove.Add(kvp.Key);
            }
        }

        foreach (GameObject go in toRemove)
        {
            entities.Remove(go);
        }

        foreach (GameObject go in logicalPositionsToRemove)
        {
            logicalPositions.Remove(go);
        }

        foreach (GameObject go in leavingPositionsToRemove)
        {
            leavingPositions.Remove(go);
        }

        positionToUnit.Clear();
        foreach (var kvp in logicalPositions)
        {
            if (kvp.Key != null) positionToUnit[kvp.Value] = kvp.Key;
        }

        leavingPositionToUnit.Clear();
        foreach (var kvp in leavingPositions)
        {
            if (kvp.Key != null) leavingPositionToUnit[kvp.Value] = kvp.Key;
        }

        if (toRemove.Count > 0)
        {
            Debug.LogWarning($"📋 EntityRegistry: {toRemove.Count} entrées orphelines nettoyées");
        }
    }

    // === Debug ===

    // Liste toutes les entités enregistrées avec leur état
    [ContextMenu("Debug: Lister toutes les entités")]
    private void DebugListAllEntities()
    {
        CleanupNullEntities();
        Debug.Log($"📋 EntityRegistry: {entities.Count} entités enregistrées ===");

        foreach (var kvp in entities)
        {
            GameObject go = kvp.Key;
            IDamageable d = kvp.Value;
            ITeamMember t = go?.GetComponent<ITeamMember>();
            string teamStr = t != null ? $"Équipe {t.GetTeamID()}" : "Pas d'équipe";
            string hpStr = d != null ? $"HP {d.GetCurrentHP()}/{d.GetMaxHP()}" : "IDamageable null";
            string deadStr = d != null && d.IsDead() ? " 💀" : "";
            Debug.Log($"  → {(go != null ? go.name : "NULL")} | {hpStr} | {teamStr}{deadStr}");
        }
    }

    // Force le nettoyage des entrées nulles via l'inspecteur
    [ContextMenu("Debug: Nettoyer les entrées nulles")]
    private void DebugCleanup()
    {
        int before = entities.Count;
        CleanupNullEntities();
        Debug.Log($"📋 EntityRegistry: Nettoyage — {before} → {entities.Count} entités");
    }
}
