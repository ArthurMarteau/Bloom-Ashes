using UnityEngine;

// Contrôleur du Nexus — spawn les Rootborn sur la tile du bâtiment
// Nécessite BuildingStatsComponent (isNexus = true) et TeamComponent sur le même GameObject
// Le coût en Watersap est lu depuis producibleUnits[0] (premier Rootborn de la liste)
public class NexusController : MonoBehaviour
{
    [Header("Feedback")]
    [SerializeField] private AudioClip spawnSFX; // branché plus tard
    [SerializeField] private GameObject spawnVFX; // branché plus tard

    [Header("Debug")]
    [SerializeField] private bool debugNexus = true;
    [SerializeField] private bool debugNexusInput = true;

    // Cache des données de base du Nexus
    private BuildingStatsData baseStats;
    private Collider2D _collider;

    // Récupère et valide les données au démarrage
    private void Awake()
    {
        _collider = GetComponent<Collider2D>();

        BuildingStatsComponent buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats == null)
        {
            Debug.LogError($"🏛️ NexusController {gameObject.name} : BuildingStatsComponent manquant !");
            enabled = false;
            return;
        }

        baseStats = buildingStats.GetBaseStats();
        if (baseStats == null)
        {
            Debug.LogError($"🏛️ NexusController {gameObject.name} : BuildingStatsData non assigné !");
            enabled = false;
            return;
        }

        // Vérifie que ce bâtiment est bien configuré comme Nexus
        if (!baseStats.isNexus)
        {
            Debug.LogError($"🏛️ NexusController {gameObject.name} : isNexus = false dans {baseStats.buildingName} — ce contrôleur ne doit être ajouté qu'au Nexus !");
            enabled = false;
            return;
        }

        if (debugNexus)
            Debug.Log($"🏛️ NexusController {gameObject.name} : initialisé ({baseStats.buildingName})");
    }

    // S'abonne aux clics droits du gameplay pour déclencher le spawn sur le Nexus ciblé
    private void Start()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnRightClickDown.AddListener(OnRightClickHandler);
    }

    // Désabonne les clics droits pour éviter les callbacks après destruction du Nexus
    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnRightClickDown.RemoveListener(OnRightClickHandler);
    }

    // Déclenche le spawn Rootborn si le clic droit tombe sur le collider du Nexus
    private void OnRightClickHandler(Vector3 worldPosition)
    {
        if (_collider == null) return;

        if (_collider.OverlapPoint(worldPosition))
        {
            if (debugNexusInput)
                Debug.Log($"🏛️ NexusController {gameObject.name} : clic droit détecté sur Nexus");

            TrySpawnRootborn();
        }
    }

    // Tente de spawner un Rootborn sur la tile du Nexus
    // Retourne true si le spawn a réussi, false sinon (watersap insuffisant, config manquante, etc.)
    public bool TrySpawnRootborn()
    {
        // Récupère l'équipe du Nexus
        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            Debug.LogWarning($"🏛️ NexusController {gameObject.name} : TeamComponent manquant — spawn annulé");
            return false;
        }
        int teamID = teamComponent.GetTeamID();

        // Vérifie que la liste de production est configurée
        if (baseStats.producibleUnits == null || baseStats.producibleUnits.Count == 0)
        {
            Debug.LogWarning($"🏛️ NexusController {gameObject.name} : producibleUnits vide — aucun Rootborn à spawner");
            return false;
        }

        // Récupère le premier Rootborn de la liste
        UnitStatsData rootbornData = baseStats.producibleUnits[0];
        if (rootbornData == null)
        {
            Debug.LogWarning($"🏛️ NexusController {gameObject.name} : producibleUnits[0] est null");
            return false;
        }

        if (rootbornData.unitPrefab == null)
        {
            Debug.LogWarning($"🏛️ NexusController {gameObject.name} : unitPrefab manquant sur {rootbornData.unitName}");
            return false;
        }

        if (GridManager.Instance != null)
        {
            Vector2Int nexusTile = GridManager.Instance.WorldToGrid(transform.position);
            GameObject occupant = GridManager.Instance.GetUnitAt(nexusTile);
            if (occupant != null)
            {
                if (debugNexus)
                    Debug.LogWarning($"️ NexusController {gameObject.name} : case {nexusTile} occupée par {occupant.name} — spawn bloqué");
                return false;
            }
        }

        // Tente de dépenser le Watersap
        if (!WatersapManager.Instance.SpendWatersap(teamID, rootbornData.watersapCost))
        {
            if (debugNexus)
                Debug.LogWarning($"🏛️ NexusController {gameObject.name} : Watersap insuffisant pour spawner {rootbornData.unitName} (coût : {rootbornData.watersapCost})");
            return false;
        }

        // Spawne le Rootborn sur la tile du Nexus
        GameObject spawnedUnit = Instantiate(rootbornData.unitPrefab, transform.position, Quaternion.identity);

        // Assigne l'équipe au Rootborn spawné
        TeamComponent unitTeam = spawnedUnit.GetComponent<TeamComponent>();
        if (unitTeam != null)
            unitTeam.SetTeamID(teamID);
        else
            Debug.LogWarning($"🏛️ NexusController {gameObject.name} : TeamComponent manquant sur le Rootborn spawné — équipe non assignée");

        // 🔊 Hook SFX/VFX spawn Rootborn ici

        if (debugNexus)
            Debug.Log($"🏛️ NexusController {gameObject.name} : Rootborn spawné (équipe {teamID}, coût {rootbornData.watersapCost} Watersap)");

        // Notifie le système de spawn pour sélection automatique
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.NotifyEntitySpawned(gameObject, spawnedUnit);
        }

        return true;
    }

    // Teste le spawn depuis l'éditeur sans passer par l'input
    [ContextMenu("Test: Spawn Rootborn")]
    private void TestSpawnRootborn()
    {
        TrySpawnRootborn();
    }
}
