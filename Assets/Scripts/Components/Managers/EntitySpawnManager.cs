using UnityEngine;
using UnityEngine.Events;

// Coordinateur centralisé pour tous les spawns/replacements d'entités
// Permet un découplage total entre spawners et SelectionManager
// Les spawners émettent des events, le SelectionManager réagit
public class EntitySpawnManager : MonoBehaviour
{
    public static EntitySpawnManager Instance { get; private set; }

    // Event déclenché quand une entité est spawnée SANS remplacement
    // Exemple : Nexus spawn un Rootborn (le Nexus reste)
    // Paramètres : (spawner, spawnedEntity)
    public UnityEvent<GameObject, GameObject> OnEntitySpawned = new UnityEvent<GameObject, GameObject>();

    // Event déclenché quand une entité est REMPLACÉE par une autre
    // Exemple : Rootborn → Bud, Bud → Unité finale
    // Paramètres : (oldEntity, newEntity)
    public UnityEvent<GameObject, GameObject> OnEntityReplaced = new UnityEvent<GameObject, GameObject>();

    // Event déclenché quand une entité est détruite (mort, sacrifice, destruction bâtiment)
    // Paramètre : entité détruite (encore valide au moment de l'appel, démontage visuel déjà planifié)
    public UnityEvent<GameObject> OnEntityDestroyed = new UnityEvent<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Instancie une entité gameplay, assigne son équipe et déclenche son VFX de spawn.
    public GameObject SpawnEntity(GameObject prefab, Vector3 position, int teamID)
    {
        if (prefab == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, "⚠️ EntitySpawnManager : SpawnEntity appelé avec prefab null", this);
            return null;
        }

        GameObject spawnedEntity = Instantiate(prefab, position, Quaternion.identity);

        TeamComponent teamComponent = spawnedEntity.GetComponent<TeamComponent>();
        if (teamComponent != null)
            teamComponent.SetTeamID(teamID);
        else
            GameLogger.Warn(GameLogger.Category.Spawn, $"⚠️ EntitySpawnManager : TeamComponent manquant sur {spawnedEntity.name} — équipe non assignée", this);

        spawnedEntity.GetComponent<UnitStatsComponent>()?.NotifySpawned();
        spawnedEntity.GetComponent<BuildingStatsComponent>()?.NotifySpawned();

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 EntitySpawnManager : {spawnedEntity.name} instancié en {position} (équipe {teamID})", this);
        return spawnedEntity;
    }

    // Retire une entité de la simulation immédiatement puis laisse son visuel disparaître après délai.
    public void DespawnEntity(GameObject entity, float visualDelay)
    {
        DespawnEntity(entity, visualDelay, true);
    }

    // Retire une entité de la simulation avec contrôle de l'event destroyed pour les remplacements.
    public void DespawnEntity(GameObject entity, float visualDelay, bool notifyDestroyed)
    {
        if (entity == null)
            return;

        EntityRegistry.Instance?.MarkDead(entity);

        if (notifyDestroyed)
            NotifyEntityDestroyed(entity);

        GameLogger.Log(GameLogger.Category.Spawn, $" EntitySpawnManager : {entity.name} démontage visuel dans {visualDelay}s", this);
        Destroy(entity, Mathf.Max(0f, visualDelay));
    }

    // Notifie qu'une entité a été spawnée (SANS détruire le spawner)
    // Utilisé par : NexusController (Nexus reste, Rootborn spawn)
    public void NotifyEntitySpawned(GameObject spawner, GameObject spawnedEntity)
    {
        if (spawner == null || spawnedEntity == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, "⚠️ EntitySpawnManager : NotifyEntitySpawned appelé avec spawner ou spawnedEntity null", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 EntitySpawnManager : {spawner.name} a spawné {spawnedEntity.name}", this);
        OnEntitySpawned.Invoke(spawner, spawnedEntity);
    }

    // Notifie qu'une entité a été REMPLACÉE par une autre
    // Utilisé par : RootbornProductionController, BudComponent
    public void NotifyEntityReplaced(GameObject oldEntity, GameObject newEntity)
    {
        if (oldEntity == null || newEntity == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, "⚠️ EntitySpawnManager : NotifyEntityReplaced appelé avec oldEntity ou newEntity null", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Spawn, $"🔄 EntitySpawnManager : {oldEntity.name} remplacé par {newEntity.name}", this);
        OnEntityReplaced.Invoke(oldEntity, newEntity);
    }

    public void NotifyEntityDestroyed(GameObject destroyedEntity)
    {
        if (destroyedEntity == null) return;
        GameLogger.Log(GameLogger.Category.Spawn, $" EntitySpawnManager : {destroyedEntity.name} détruit", this);
        OnEntityDestroyed.Invoke(destroyedEntity);
    }
}
