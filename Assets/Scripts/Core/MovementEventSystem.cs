using UnityEngine;
using System;

namespace Core
{
// Système centralisé d'événements pour les mouvements d'unités
public class MovementEventSystem : MonoBehaviour
{
    // Instance singleton pour accès global
    public static MovementEventSystem Instance;
    
    // Événements pour notifier les changements d'état de mouvement
    public static event Action<GameObject, Vector2Int> OnUnitStartMoving;
    public static event Action<GameObject, Vector2Int> OnUnitReachDestination;
    public static event Action<GameObject, GameObject> OnUnitStartChasing;
    public static event Action<GameObject> OnUnitStopMoving;
    public static event Action<GameObject, Vector2Int> OnUnitBlocked;
    public static event Action<GameObject, GameObject> OnUnitCollision;
    public static event Action<GameObject> OnUnitPushedBack;
    
    // Initialisation du singleton
    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else 
        {
            Destroy(gameObject);
        }
    }
    
    // Méthode pour déclencher l'événement de début de mouvement
    public static void TriggerStartMoving(GameObject unit, Vector2Int destination)
    {
        OnUnitStartMoving?.Invoke(unit, destination);
        Debug.Log($"📢 Événement: {unit.name} commence à bouger vers {destination}");
    }
    
    // Méthode pour déclencher l'événement d'arrivée à destination
    public static void TriggerReachDestination(GameObject unit, Vector2Int position)
    {
        OnUnitReachDestination?.Invoke(unit, position);
        Debug.Log($"📢 Événement: {unit.name} a atteint {position}");
    }
    
    // Méthode pour déclencher l'événement de début de poursuite
    public static void TriggerStartChasing(GameObject unit, GameObject target)
    {
        OnUnitStartChasing?.Invoke(unit, target);
        Debug.Log($"📢 Événement: {unit.name} commence à poursuivre {target.name}");
    }
    
    // Méthode pour déclencher l'événement d'arrêt de mouvement
    public static void TriggerStopMoving(GameObject unit)
    {
        OnUnitStopMoving?.Invoke(unit);
        Debug.Log($"📢 Événement: {unit.name} s'arrête");
    }
    
    // Méthode pour déclencher l'événement de blocage
    public static void TriggerUnitBlocked(GameObject unit, Vector2Int blockedPosition)
    {
        OnUnitBlocked?.Invoke(unit, blockedPosition);
        Debug.Log($"📢 Événement: {unit.name} est bloqué à {blockedPosition}");
    }
    
    // Méthode pour déclencher l'événement de collision
    public static void TriggerUnitCollision(GameObject unit, GameObject other)
    {
        OnUnitCollision?.Invoke(unit, other);
        Debug.Log($"📢 Événement: Collision entre {unit.name} et {other.name}");
    }
    
    // Méthode pour déclencher l'événement de repoussement
    public static void TriggerUnitPushedBack(GameObject unit)
    {
        OnUnitPushedBack?.Invoke(unit);
        Debug.Log($"📢 Événement: {unit.name} est repoussé");
    }
    
    // Méthode pour nettoyer tous les événements (utile pour les tests ou le reset)
    public static void ClearAllListeners()
    {
        OnUnitStartMoving = null;
        OnUnitReachDestination = null;
        OnUnitStartChasing = null;
        OnUnitStopMoving = null;
        OnUnitBlocked = null;
        OnUnitCollision = null;
        OnUnitPushedBack = null;
    }
    
    // Nettoyage à la destruction
    private void OnDestroy()
    {
        if (Instance == this)
        {
            ClearAllListeners();
        }
    }
}
}