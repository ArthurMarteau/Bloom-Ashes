using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Environnement walkable 2x2 qui détecte 4 Rootborn locaux idle sur ses cases
// et permet au joueur de construire un bâtiment 2x2 via un panneau UI
// N'est PAS un bâtiment (pas de PV, pas d'IDamageable), pas dans le buildingLayer
// Pattern réutilisable pour les futures veines de Watersap
public class FoundationComponent : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private FoundationData foundationData;

    [Header("Debug")]
    [SerializeField] private bool debugFoundation = false;

    // Index du layer Environment_Walkable attendu
    private const int ExpectedLayer = 7;

    // Les 4 cases grille occupées par la Foundation (bottom-left + offsets)
    private Vector2Int[] occupiedCells;

    // Rootborn locaux actuellement sur les cases, avec leur MovementController caché
    private Dictionary<GameObject, MovementController> trackedRootborns = new Dictionary<GameObject, MovementController>();

    // Vrai après déclenchement du build : empêche tout double-build
    private bool isBuildLocked = false;

    [Header("Events")]
    // Déclenché à chaque changement d'occupant ou d'état — l'UI re-query via les getters
    public UnityEvent OnStateChanged = new UnityEvent();

    // Déclenché au début de OnDestroy — permet à l'UI de se masquer proprement
    public UnityEvent OnDestroyed = new UnityEvent();

    // Initialise les cases et s'abonne aux positions logiques du registry
    private void Start()
    {
        if (gameObject.layer != ExpectedLayer)
        {
            Debug.LogWarning($"🌱 FoundationComponent [{gameObject.name}]: Layer attendu = Environment_Walkable ({ExpectedLayer}), " +
                             $"layer actuel = {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError($"🌱 FoundationComponent [{gameObject.name}]: GridManager introuvable — Foundation désactivée");
            enabled = false;
            return;
        }

        // Calcule les 4 cases depuis la position monde (bottom-left = ancre du prefab)
        Vector2Int anchorCell = GridManager.Instance.WorldToGrid(transform.position);
        occupiedCells = new Vector2Int[4]
        {
            anchorCell,
            anchorCell + new Vector2Int(1, 0),
            anchorCell + new Vector2Int(0, 1),
            anchorCell + new Vector2Int(1, 1)
        };

        if (debugFoundation)
        {
            Debug.Log($"🌱 FoundationComponent [{gameObject.name}]: Cases occupées — " +
                      $"{occupiedCells[0]}, {occupiedCells[1]}, {occupiedCells[2]}, {occupiedCells[3]}");
        }

        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.OnLogicalPositionUpdated.AddListener(HandleLogicalPositionUpdated);
        }
        else
        {
            Debug.LogWarning($"🌱 FoundationComponent [{gameObject.name}]: EntityRegistry introuvable — détection désactivée");
        }

        // Scan initial au démarrage
        RefreshOccupants();
    }

    // Désabonne tous les listeners pour éviter les NullRef en fin de partie
    private void OnDestroy()
    {
        // Notifie l'UI avant tout nettoyage
        OnDestroyed.Invoke();

        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.OnLogicalPositionUpdated.RemoveListener(HandleLogicalPositionUpdated);
        }

        foreach (GameObject rootborn in new List<GameObject>(trackedRootborns.Keys))
        {
            if (rootborn != null)
            {
                UnsubscribeFromRootborn(rootborn);
            }
        }
        trackedRootborns.Clear();
    }

    // === DÉTECTION EVENT-DRIVEN ===

    // Reçoit toutes les mises à jour de position logique du registry
    // Filtre : ne rescanne que si la position touche nos cases ou si l'unité était trackée
    private void HandleLogicalPositionUpdated(GameObject unit, Vector2Int position)
    {
        if (unit == null) return;

        bool isInOurCells = IsCellOccupied(position);
        bool wasTracked   = trackedRootborns.ContainsKey(unit);

        if (isInOurCells || wasTracked)
        {
            RefreshOccupants();
        }
    }

    // Rescanne les 4 cases et met à jour trackedRootborns avec les Rootborn locaux présents
    // Appelé sur chaque changement de position logique pertinent et sur mort de Rootborn
    private void RefreshOccupants()
    {
        if (EntityRegistry.Instance == null) return;
        if (PlayerManager.Instance == null) return;

        int localTeamID = PlayerManager.Instance.LocalPlayer.teamID;

        // Désabonnement total avant rescan propre
        foreach (GameObject rootborn in new List<GameObject>(trackedRootborns.Keys))
        {
            if (rootborn != null)
            {
                UnsubscribeFromRootborn(rootborn);
            }
        }
        trackedRootborns.Clear();

        // Rescan des 4 cases
        foreach (Vector2Int cell in occupiedCells)
        {
            GameObject unitAtCell = EntityRegistry.Instance.GetUnitAtLogicalPosition(cell);
            if (unitAtCell == null) continue;

            UnitStatsComponent stats = unitAtCell.GetComponent<UnitStatsComponent>();
            if (stats == null || !stats.IsRootborn() || stats.IsDead()) continue;

            TeamComponent team = unitAtCell.GetComponent<TeamComponent>();
            if (team == null || team.GetTeamID() != localTeamID) continue;

            MovementController movement = unitAtCell.GetComponent<MovementController>();
            trackedRootborns[unitAtCell] = movement;
            SubscribeToRootborn(unitAtCell, movement, stats);
        }

        if (debugFoundation)
        {
            Debug.Log($"🌱 FoundationComponent [{gameObject.name}]: RefreshOccupants — {trackedRootborns.Count}/4 Rootborn locaux");
        }

        OnStateChanged.Invoke();
    }

    // Abonne les events de mouvement et de mort d'un Rootborn tracké
    private void SubscribeToRootborn(GameObject rootborn, MovementController movement, UnitStatsComponent stats)
    {
        if (movement != null)
        {
            movement.OnMovementStarted.AddListener(FireStateChanged);
            movement.OnArrived.AddListener(FireStateChanged);
        }

        if (stats != null)
        {
            stats.OnDeath.AddListener(OnRootbornDied);
        }
    }

    // Désabonne les events d'un Rootborn tracké
    private void UnsubscribeFromRootborn(GameObject rootborn)
    {
        if (rootborn == null) return;

        if (trackedRootborns.TryGetValue(rootborn, out MovementController movement))
        {
            if (movement != null)
            {
                movement.OnMovementStarted.RemoveListener(FireStateChanged);
                movement.OnArrived.RemoveListener(FireStateChanged);
            }
        }

        UnitStatsComponent stats = rootborn.GetComponent<UnitStatsComponent>();
        if (stats != null)
        {
            stats.OnDeath.RemoveListener(OnRootbornDied);
        }
    }

    // Callback OnDeath d'un Rootborn tracké — re-scan complet car le mort ne doit plus compter
    private void OnRootbornDied()
    {
        RefreshOccupants();
    }

    // Notifie l'UI d'un changement d'état sans re-scanner les occupants (mouvement seul)
    private void FireStateChanged()
    {
        OnStateChanged.Invoke();
    }

    // === API PUBLIQUE ===

    // Retourne les 4 cases grille occupées par la Foundation
    public Vector2Int[] GetOccupiedCells()
    {
        return occupiedCells;
    }

    // Retourne la case bottom-left (ancre du prefab)
    public Vector2Int GetAnchorCell()
    {
        return occupiedCells != null && occupiedCells.Length > 0 ? occupiedCells[0] : Vector2Int.zero;
    }

    // Retourne le ScriptableObject de configuration
    public FoundationData GetData()
    {
        return foundationData;
    }

    // Retourne les Rootborn locaux vivants actuellement trackés sur les cases
    public List<GameObject> GetCurrentOccupants()
    {
        List<GameObject> occupants = new List<GameObject>();

        foreach (GameObject rootborn in trackedRootborns.Keys)
        {
            if (rootborn == null) continue;

            UnitStatsComponent stats = rootborn.GetComponent<UnitStatsComponent>();
            if (stats != null && stats.IsDead()) continue;

            occupants.Add(rootborn);
        }

        return occupants;
    }

    // Retourne vrai si 4 Rootborn locaux idle sont présents et le build n'est pas verrouillé
    public bool IsReadyToBuild()
    {
        if (isBuildLocked) return false;
        if (trackedRootborns.Count != 4) return false;

        foreach (var entry in trackedRootborns)
        {
            if (entry.Key == null) return false;

            UnitStatsComponent stats = entry.Key.GetComponent<UnitStatsComponent>();
            if (stats == null || stats.IsDead()) return false;

            MovementController movement = entry.Value;
            if (movement == null) return false;
            if (movement.GetCurrentState() != MovementController.MovementState.Idle) return false;
        }

        return true;
    }

    // Déclenche la construction du bâtiment indiqué si toutes les conditions sont remplies
    // Build atomique : isBuildLocked est posé AVANT toute destruction
    public void RequestBuild(BuildingStatsData building)
    {
        if (!IsReadyToBuild())
        {
            Debug.LogWarning($"🌱 FoundationComponent [{gameObject.name}]: RequestBuild ignoré — conditions non remplies");
            return;
        }

        if (building == null)
        {
            Debug.LogError($"🌱 FoundationComponent [{gameObject.name}]: RequestBuild — BuildingStatsData est null");
            return;
        }

        if (building.sizeX != 2 || building.sizeY != 2)
        {
            Debug.LogError($"🌱 FoundationComponent [{gameObject.name}]: RequestBuild — '{building.buildingName}' n'est pas 2x2 " +
                           $"(sizeX={building.sizeX}, sizeY={building.sizeY})");
            return;
        }

        if (foundationData == null || !foundationData.availableBuildings.Contains(building))
        {
            Debug.LogWarning($"🌱 FoundationComponent [{gameObject.name}]: RequestBuild — '{building.buildingName}' non présent dans availableBuildings");
            return;
        }

        if (building.buildingPrefab == null)
        {
            Debug.LogError($"🌱 FoundationComponent [{gameObject.name}]: RequestBuild — buildingPrefab non assigné sur '{building.buildingName}'");
            return;
        }

        // Verrouillage atomique : empêche tout double-build même en cas de spam
        isBuildLocked = true;

        // Capture des occupants avant toute destruction
        List<GameObject> capturedRootborns = GetCurrentOccupants();

        if (capturedRootborns.Count != 4)
        {
            Debug.LogError($"🌱 FoundationComponent [{gameObject.name}]: RequestBuild — occupants attendus=4, trouvés={capturedRootborns.Count} — annulé");
            isBuildLocked = false;
            return;
        }

        // Position d'instanciation = ancre bottom-left (convention prefab)
        Vector3 spawnPosition = GridManager.Instance.GridToWorld(GetAnchorCell());

        // Instanciation du bâtiment
        GameObject newBuilding = Instantiate(building.buildingPrefab, spawnPosition, Quaternion.identity);

        Debug.Log($"🏗️ FoundationComponent [{gameObject.name}]: Bâtiment '{building.buildingName}' instancié en {spawnPosition}");

        // Assignation de l'équipe du joueur local
        TeamComponent teamComponent = newBuilding.GetComponent<TeamComponent>();
        if (teamComponent != null && PlayerManager.Instance != null)
        {
            teamComponent.SetTeamID(PlayerManager.Instance.LocalPlayer.teamID);
        }

        // Notification de remplacement UNE SEULE FOIS (premier Rootborn → nouveau bâtiment)
        // Permet au SelectionManager de basculer automatiquement la sélection
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.NotifyEntityReplaced(capturedRootborns[0], newBuilding);
        }

        // Destruction des 4 Rootborn consommés
        foreach (GameObject rootborn in capturedRootborns)
        {
            if (rootborn != null)
            {
                Destroy(rootborn);
            }
        }

        Debug.Log($"🏗️ FoundationComponent [{gameObject.name}]: 4 Rootborn consommés — Foundation détruite");

        // Auto-destruction de la Foundation
        Destroy(gameObject);
    }

    // === UTILITAIRES INTERNES ===

    // Vérifie si une case grille appartient aux 4 cases de la Foundation
    private bool IsCellOccupied(Vector2Int cell)
    {
        if (occupiedCells == null) return false;

        foreach (Vector2Int occupiedCell in occupiedCells)
        {
            if (occupiedCell == cell) return true;
        }

        return false;
    }

    // === DEBUG ===

    // Affiche l'état courant de la Foundation dans la console
    [ContextMenu("Debug: Afficher état Foundation")]
    private void DebugFoundationState()
    {
        Debug.Log($"=== FoundationComponent Debug — {gameObject.name} ===");
        Debug.Log($"Cases : {(occupiedCells != null ? string.Join(", ", occupiedCells) : "non initialisées")}");
        Debug.Log($"Occupants trackés : {trackedRootborns.Count}/4");
        Debug.Log($"IsReadyToBuild : {IsReadyToBuild()}");
        Debug.Log($"isBuildLocked : {isBuildLocked}");

        int index = 0;
        foreach (var entry in trackedRootborns)
        {
            MovementController movement = entry.Value;
            string state = movement != null ? movement.GetCurrentState().ToString() : "MovementController null";
            Debug.Log($"  [{index}] {(entry.Key != null ? entry.Key.name : "null")} — état: {state}");
            index++;
        }
    }

    // Force un rescan des occupants depuis l'inspecteur
    [ContextMenu("Debug: Forcer RefreshOccupants")]
    private void DebugForceRefresh()
    {
        RefreshOccupants();
        Debug.Log($"🌱 FoundationComponent [{gameObject.name}]: RefreshOccupants forcé depuis l'inspecteur");
    }
}
