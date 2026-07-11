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
    private Dictionary<GameObject, ITeamMember> teamMembers = new Dictionary<GameObject, ITeamMember>();
    private Dictionary<GameObject, IEvictable> evictables = new Dictionary<GameObject, IEvictable>();
    private Dictionary<GameObject, long> entityIDs = new Dictionary<GameObject, long>();
    private Dictionary<long, GameObject> entitiesByID = new Dictionary<long, GameObject>();
    private Dictionary<GameObject, MovementController> movementControllers = new Dictionary<GameObject, MovementController>();
    private HashSet<GameObject> movingUnits = new HashSet<GameObject>();
    private long nextEntityID = 1;

    // Registre des positions logiques : position grille source de vérité, indépendante du Lerp visuel
    // Mis à jour par MovementController à chaque étape logique
    private Dictionary<GameObject, Vector2Int> logicalPositions = new Dictionary<GameObject, Vector2Int>();

    // Cases en cours d'abandon : unité logiquement partie mais Lerp visuel pas encore terminé
    // Permet à une autre unité de s'y engager sans attendre la fin du Lerp
    private Dictionary<GameObject, Vector2Int> leavingPositions = new Dictionary<GameObject, Vector2Int>();
    private Dictionary<Vector2Int, GameObject> positionToUnit = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, long> claimTicks = new Dictionary<Vector2Int, long>();
    private Dictionary<Vector2Int, HashSet<GameObject>> cellToUnits = new Dictionary<Vector2Int, HashSet<GameObject>>();
    private Dictionary<Vector2Int, GameObject> leavingPositionToUnit = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> incomingByCell = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<GameObject, Vector2Int> incomingCellsByUnit = new Dictionary<GameObject, Vector2Int>();
    private Dictionary<Vector2Int, List<ICellListener>> cellListeners = new Dictionary<Vector2Int, List<ICellListener>>();
    private Dictionary<Vector2Int, GameObject> buildingPositions = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<GameObject, Vector2Int> buildingLogicalPositions = new Dictionary<GameObject, Vector2Int>();

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
        GameLogger.Log(GameLogger.Category.Spawn, "📋 EntityRegistry: Initialisé et prêt à recevoir les entités", null);
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
    public void RegisterEntity(IDamageable entity, GameObject go, ITeamMember teamMember, IEvictable evictable)
    {
        if (go == null || entity == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, "📋 EntityRegistry.RegisterEntity: go ou entity est null — enregistrement ignoré", null);
            return;
        }

        // Écrase silencieusement si déjà présent (re-activation d'objet poolé par ex.)
        entities[go] = entity;
        teamMembers[go] = teamMember;
        evictables[go] = evictable;
        MovementController movementController = evictable as MovementController;
        if (movementController != null)
            movementControllers[go] = movementController;
        if (!entityIDs.ContainsKey(go))
            entityIDs[go] = nextEntityID++;
        entitiesByID[entityIDs[go]] = go;

        if (debugRegistry)
        {
            GameLogger.Log(GameLogger.Category.Spawn, $"📋 EntityRegistry: ✅ {go.name} enregistré ({entities.Count} entités au total)", null);
        }
    }

    // Désenregistre une entité du registre
    // Appelé dans OnDestroy() de UnitStatsComponent et BuildingStatsComponent
    public void UnregisterEntity(GameObject go)
    {
        if (go == null) return;

        entities.Remove(go);
        teamMembers.Remove(go);
        evictables.Remove(go);
        if (entityIDs.TryGetValue(go, out long entityID))
            entitiesByID.Remove(entityID);
        entityIDs.Remove(go);
        movementControllers.Remove(go);
        movingUnits.Remove(go);
        ReleasePositionInternal(go);

        if (debugRegistry)
        {
            GameLogger.Log(GameLogger.Category.Spawn, $"📋 EntityRegistry: ❌ {go.name} désenregistré ({entities.Count} entités restantes)", null);
        }
    }

    // Met à jour la position logique d'une unité dans le registre
    // Appelé par MovementController à chaque étape logique (avant le Lerp visuel)
    public void UpdateLogicalPosition(GameObject go, Vector2Int position)
    {
        if (go == null) return;

        if (logicalPositions.TryGetValue(go, out var oldPos))
        {
            RemoveUnitFromCell(oldPos, go);
            if (positionToUnit.TryGetValue(oldPos, out var registered) && registered == go)
            {
                positionToUnit.Remove(oldPos);
                claimTicks.Remove(oldPos);
            }
        }

        logicalPositions[go] = position;
        AddUnitToCell(position, go);

        if (positionToUnit.TryGetValue(position, out var existing) && existing != go && existing != null
            && debugRegistry && GameLogger.IsEnabled(GameLogger.Category.Spawn))
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $" EntityRegistry: collision logique sur {position} entre {existing.name} et {go.name}", null);
        }
        claimTicks.Remove(position);
        positionToUnit[position] = go;
    }

    // Tente de réserver une cellule avec arbitrage déterministe par tick puis joueur
    // Une unité immobile ou un occupant sans claim comparable ne peut jamais être évincé
    public bool TryClaimPosition(GameObject go, Vector2Int cell, long orderTick, int playerID)
    {
        if (go == null) return false;
        bool hadOldPos = logicalPositions.TryGetValue(go, out var previousPosition);
        if (positionToUnit.TryGetValue(cell, out var existing) && existing != null && existing != go)
        {
            if (!IsMoving(existing) || !claimTicks.TryGetValue(cell, out long existingOrderTick))
                return false;

            int existingPlayerID = teamMembers.TryGetValue(existing, out ITeamMember existingTeamMember) && existingTeamMember != null
                ? existingTeamMember.GetTeamID()
                : 0;
            bool incomingClaimWins = orderTick < existingOrderTick
                || (orderTick == existingOrderTick && playerID < existingPlayerID);
            if (!incomingClaimWins)
                return false;

            if (logicalPositions.TryGetValue(existing, out var existingPos) && existingPos == cell)
            {
                logicalPositions.Remove(existing);
                RemoveUnitFromCell(existingPos, existing);
                NotifyCellLeft(existingPos, existing);
            }
            if (logicalPositions.TryGetValue(go, out var oldPos) && oldPos != cell
                && positionToUnit.TryGetValue(oldPos, out var rl) && rl == go)
            {
                positionToUnit.Remove(oldPos);
                claimTicks.Remove(oldPos);
                RemoveUnitFromCell(oldPos, go);
            }
            logicalPositions[go] = cell;
            AddUnitToCell(cell, go);
            positionToUnit[cell] = go;
            claimTicks[cell] = orderTick;
            NotifyCellEntered(cell, go);
            if (hadOldPos && previousPosition != cell)
                NotifyCellLeft(previousPosition, go);

            if (evictables.TryGetValue(existing, out IEvictable evictable) && evictable != null)
                evictable.OnEvicted(cell);

            return true;
        }
        UpdateLogicalPosition(go, cell);
        claimTicks[cell] = orderTick;
        NotifyCellEntered(cell, go);
        if (hadOldPos && previousPosition != cell)
            NotifyCellLeft(previousPosition, go);
        return true;
    }

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

    // Met à jour l'état de déplacement d'une unité pour les lectures O(1) du pathfinding
    public void SetMoving(GameObject go, bool isMoving)
    {
        if (go == null) return;

        if (isMoving)
            movingUnits.Add(go);
        else
            movingUnits.Remove(go);
    }

    // Retourne si une unité est actuellement en mouvement
    public bool IsMoving(GameObject go)
    {
        return go != null && movingUnits.Contains(go);
    }

    // Enregistre l'unité en approche d'une cellule avec un seul entrant visible par case.
    public void SetIncoming(GameObject go, Vector2Int cell)
    {
        if (go == null) return;

        ClearIncoming(go);
        if (incomingByCell.TryGetValue(cell, out GameObject existing) && existing != null && existing != go)
        {
            if (debugRegistry && GameLogger.IsEnabled(GameLogger.Category.Spawn))
            {
                GameLogger.Warn(GameLogger.Category.Spawn,
                    $" EntityRegistry: conflit d'entrants sur {cell} entre {existing.name} et {go.name} — dernier entrant conservé", null);
            }
            incomingCellsByUnit.Remove(existing);
        }

        incomingByCell[cell] = go;
        incomingCellsByUnit[go] = cell;
    }

    // Retire l'unité de l'index des cellules en approche en O(1).
    public void ClearIncoming(GameObject go)
    {
        if (go == null) return;
        if (incomingCellsByUnit.TryGetValue(go, out Vector2Int cell)
            && incomingByCell.TryGetValue(cell, out GameObject registered) && registered == go)
        {
            incomingByCell.Remove(cell);
        }
        incomingCellsByUnit.Remove(go);
    }

    // Retourne l'unité actuellement indexée comme entrante sur la cellule.
    public GameObject GetIncomingUnit(Vector2Int cell)
    {
        if (incomingByCell.TryGetValue(cell, out GameObject go) && go != null)
            return go;
        return null;
    }

    // Retourne le contrôleur mis en cache à l'enregistrement, sans GetComponent pendant les ticks.
    public MovementController GetMovementController(GameObject go)
    {
        if (go != null && movementControllers.TryGetValue(go, out MovementController controller))
            return controller;
        return null;
    }

    // Libère la position logique d'une entité sans la désenregistrer du registry
    // Appelée à la mort d'une unité : la case devient walkable immédiatement,
    // mais l'entité reste dans `entities` pendant l'animation de mort (filtrée par IsDead() ailleurs)
    public void ReleaseLogicalPosition(GameObject go)
    {
        if (go == null) return;

        ReleasePositionInternal(go);
    }

    // Libère tous les index de position logique et de cellule en cours d'abandon
    private void ReleasePositionInternal(GameObject go)
    {
        ClearIncoming(go);
        if (logicalPositions.TryGetValue(go, out var lp))
        {
            RemoveUnitFromCell(lp, go);
            if (positionToUnit.TryGetValue(lp, out var rl) && rl == go)
            {
                positionToUnit.Remove(lp);
                claimTicks.Remove(lp);
                NotifyCellLeft(lp, go);
            }
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

    // Retourne la position logique enregistrée d'une entité sans consulter son Transform.
    public bool TryGetLogicalPosition(GameObject go, out Vector2Int position)
    {
        if (go != null)
        {
            if (logicalPositions.TryGetValue(go, out position))
                return true;
            if (buildingLogicalPositions.TryGetValue(go, out position))
                return true;
        }

        position = default;
        return false;
    }

    // Retourne les cellules logiques occupées par un bâtiment dans un ordre stable.
    public List<Vector2Int> GetBuildingOccupiedCells(GameObject building)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (building == null)
            return cells;

        foreach (var entry in buildingPositions)
        {
            if (entry.Value == building)
                cells.Add(entry.Key);
        }
        cells.Sort((left, right) => left.x != right.x ? left.x.CompareTo(right.x) : left.y.CompareTo(right.y));
        return cells;
    }

    // Ajoute un listener aux notifications d'une cellule logique.
    public void RegisterCellListener(Vector2Int cell, ICellListener listener)
    {
        if (listener == null) return;

        if (!cellListeners.TryGetValue(cell, out List<ICellListener> listeners))
        {
            listeners = new List<ICellListener>();
            cellListeners[cell] = listeners;
        }

        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    // Retire un listener d'une cellule logique.
    public void UnregisterCellListener(Vector2Int cell, ICellListener listener)
    {
        if (listener == null) return;
        if (!cellListeners.TryGetValue(cell, out List<ICellListener> listeners)) return;

        listeners.Remove(listener);
        if (listeners.Count == 0)
            cellListeners.Remove(cell);
    }

    // Retire un listener de toutes les cellules où il était enregistré.
    public void UnregisterAllCellListeners(ICellListener listener)
    {
        if (listener == null) return;

        foreach (Vector2Int cell in new List<Vector2Int>(cellListeners.Keys))
        {
            cellListeners[cell].Remove(listener);
            if (cellListeners[cell].Count == 0)
                cellListeners.Remove(cell);
        }
    }

    // Enregistre un bâtiment sur une cellule logique occupée.
    public void RegisterBuilding(Vector2Int cell, GameObject building)
    {
        if (building == null) return;
        buildingPositions[cell] = building;
        if (!buildingLogicalPositions.TryGetValue(building, out Vector2Int anchor)
            || cell.x < anchor.x || (cell.x == anchor.x && cell.y < anchor.y))
        {
            buildingLogicalPositions[building] = cell;
        }
    }

    // Désenregistre le bâtiment occupant une cellule logique.
    public void UnregisterBuilding(Vector2Int cell)
    {
        if (buildingPositions.TryGetValue(cell, out GameObject building) && building != null)
        {
            buildingLogicalPositions.Remove(building);
            foreach (var entry in buildingPositions)
            {
                if (entry.Key == cell || entry.Value != building)
                    continue;
                if (!buildingLogicalPositions.TryGetValue(building, out Vector2Int anchor)
                    || entry.Key.x < anchor.x || (entry.Key.x == anchor.x && entry.Key.y < anchor.y))
                {
                    buildingLogicalPositions[building] = entry.Key;
                }
            }
        }
        buildingPositions.Remove(cell);
    }

    // Retourne le bâtiment occupant une cellule logique, ou null.
    public GameObject GetBuildingAtLogicalPosition(Vector2Int cell)
    {
        if (buildingPositions.TryGetValue(cell, out GameObject building) && building != null)
            return building;

        return null;
    }

    // Retourne tous les GameObjects dans un rayon (distance Chebyshev) autour d'un centre de grille
    // Inclut les entités mortes — à filtrer par l'appelant si nécessaire
    // Performance : O(range²) via l'index des cellules occupées, zéro appel Physics2D
    public List<GameObject> FindEntitiesInRange(Vector2Int center, int range)
    {
        List<GameObject> result = new List<GameObject>();
        HashSet<GameObject> addedEntities = new HashSet<GameObject>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                Vector2Int cell = center + new Vector2Int(dx, dy);
                if (!cellToUnits.TryGetValue(cell, out HashSet<GameObject> units)) continue;

                foreach (GameObject go in units)
                {
                    if (go != null && addedEntities.Add(go))
                        result.Add(go);
                }
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
        HashSet<GameObject> addedEnemies = new HashSet<GameObject>();

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                Vector2Int cell = center + new Vector2Int(dx, dy);
                if (!cellToUnits.TryGetValue(cell, out HashSet<GameObject> units)) continue;

                foreach (GameObject go in units)
                {
                    IDamageable damageable = go != null && entities.TryGetValue(go, out var d) ? d : null;
                    if (go == null || damageable == null) continue;
                    if (damageable.IsDead() || damageable.IsInvulnerable()) continue;

                    if (!teamMembers.TryGetValue(go, out ITeamMember otherTeam) || otherTeam == null) continue;

                    int otherTeamID = otherTeam.GetTeamID();
                    if (myTeamID == 0 || otherTeamID == 0) continue;
                    if (otherTeamID == myTeamID) continue;

                    if (addedEnemies.Add(go))
                        result.Add(go);
                }
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

    // Retourne l'identifiant séquentiel stable de l'entité ou -1 si elle n'est pas enregistrée
    public long GetEntityID(GameObject go)
    {
        if (go != null && entityIDs.TryGetValue(go, out long entityID))
            return entityID;

        return -1;
    }

    // Retourne l'objet de scène associé à un identifiant stable, ou null s'il a disparu.
    public GameObject GetGameObjectByEntityID(long entityID)
    {
        if (entitiesByID.TryGetValue(entityID, out GameObject go) && go != null)
            return go;
        return null;
    }

    // Indique si une entité enregistrée est encore vivante au tick courant.
    public bool IsEntityAlive(GameObject go)
    {
        return go != null
            && entities.TryGetValue(go, out IDamageable damageable)
            && damageable != null
            && !damageable.IsDead();
    }

    // === Utilitaires ===

    // Notifie les listeners qu'une unité est entrée sur une cellule.
    private void NotifyCellEntered(Vector2Int cell, GameObject unit)
    {
        if (!cellListeners.TryGetValue(cell, out List<ICellListener> listeners)) return;

        foreach (ICellListener listener in new List<ICellListener>(listeners))
        {
            listener?.OnUnitEntered(cell, unit);
        }
    }

    // Notifie les listeners qu'une unité a quitté une cellule.
    private void NotifyCellLeft(Vector2Int cell, GameObject unit)
    {
        if (!cellListeners.TryGetValue(cell, out List<ICellListener> listeners)) return;

        foreach (ICellListener listener in new List<ICellListener>(listeners))
        {
            listener?.OnUnitLeft(cell, unit);
        }
    }

    // Ajoute une unité à l'index inversé des cellules occupées.
    private void AddUnitToCell(Vector2Int cell, GameObject unit)
    {
        if (unit == null) return;

        if (!cellToUnits.TryGetValue(cell, out HashSet<GameObject> units))
        {
            units = new HashSet<GameObject>();
            cellToUnits[cell] = units;
        }

        units.Add(unit);
    }

    // Retire une unité de l'index inversé et supprime la cellule si elle devient vide.
    private void RemoveUnitFromCell(Vector2Int cell, GameObject unit)
    {
        if (unit == null) return;
        if (!cellToUnits.TryGetValue(cell, out HashSet<GameObject> units)) return;

        units.Remove(unit);
        if (units.Count == 0)
            cellToUnits.Remove(cell);
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
            if (entityIDs.TryGetValue(go, out long entityID))
                entitiesByID.Remove(entityID);
            entities.Remove(go);
            teamMembers.Remove(go);
            evictables.Remove(go);
            entityIDs.Remove(go);
            movementControllers.Remove(go);
            movingUnits.Remove(go);
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
        claimTicks.Clear();
        cellToUnits.Clear();
        foreach (var kvp in logicalPositions)
        {
            if (kvp.Key != null)
            {
                positionToUnit[kvp.Value] = kvp.Key;
                AddUnitToCell(kvp.Value, kvp.Key);
            }
        }

        leavingPositionToUnit.Clear();
        foreach (var kvp in leavingPositions)
        {
            if (kvp.Key != null) leavingPositionToUnit[kvp.Value] = kvp.Key;
        }

        if (toRemove.Count > 0)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"📋 EntityRegistry: {toRemove.Count} entrées orphelines nettoyées", null);
        }
    }

    // === Debug ===

    // Liste toutes les entités enregistrées avec leur état
    [ContextMenu("Debug: Lister toutes les entités")]
    private void DebugListAllEntities()
    {
        CleanupNullEntities();
        GameLogger.Log(GameLogger.Category.Spawn, $"📋 EntityRegistry: {entities.Count} entités enregistrées ===", null);

        foreach (var kvp in entities)
        {
            GameObject go = kvp.Key;
            IDamageable d = kvp.Value;
            ITeamMember t = go?.GetComponent<ITeamMember>();
            string teamStr = t != null ? $"Équipe {t.GetTeamID()}" : "Pas d'équipe";
            string hpStr = d != null ? $"HP {d.GetCurrentHP()}/{d.GetMaxHP()}" : "IDamageable null";
            string deadStr = d != null && d.IsDead() ? " 💀" : "";
            GameLogger.Log(GameLogger.Category.Spawn, $"  → {(go != null ? go.name : "NULL")} | {hpStr} | {teamStr}{deadStr}", null);
        }
    }

    // Force le nettoyage des entrées nulles via l'inspecteur
    [ContextMenu("Debug: Nettoyer les entrées nulles")]
    private void DebugCleanup()
    {
        int before = entities.Count;
        CleanupNullEntities();
        GameLogger.Log(GameLogger.Category.Spawn, $"📋 EntityRegistry: Nettoyage — {before} → {entities.Count} entités", null);
    }

    // Liste les listeners enregistrés par cellule logique.
    [ContextMenu("Debug: Lister les cell listeners")]
    private void DebugListCellListeners()
    {
        GameLogger.Log(GameLogger.Category.Spawn, $"EntityRegistry: {cellListeners.Count} cellule(s) avec listeners", null);

        foreach (var kvp in cellListeners)
        {
            GameLogger.Log(GameLogger.Category.Spawn, $"  {kvp.Key} : {kvp.Value.Count} listener(s)", null);
        }
    }

    // Affiche l'état de l'index inversé des unités par cellule.
    [ContextMenu("Debug: Lister cellToUnits")]
    private void DebugListCellToUnits()
    {
        int unitCount = 0;
        foreach (var kvp in cellToUnits)
        {
            unitCount += kvp.Value.Count;
        }

        GameLogger.Log(GameLogger.Category.Spawn, $"EntityRegistry: cellToUnits = {cellToUnits.Count} cellule(s), {unitCount} unité(s)", null);

        foreach (var kvp in cellToUnits)
        {
            GameLogger.Log(GameLogger.Category.Spawn, $"  {kvp.Key} : {kvp.Value.Count} unité(s)", null);
        }
    }
}
