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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Notifie qu'une entité a été spawnée (SANS détruire le spawner)
    /// Utilisé par : NexusController (Nexus reste, Rootborn spawn)
    /// </summary>
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

    /// <summary>
    /// Notifie qu'une entité a été REMPLACÉE par une autre
    /// Utilisé par : RootbornProductionController, BudComponent
    /// </summary>
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
}
