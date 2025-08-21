using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

public class PathfinderFollower : MonoBehaviour
{
    // Composants liés
    private MovementCore movementCore;
    private CollisionHandler collisionHandler;
    
    // État du suivi
    private Coroutine followCoroutine = null;
    private Coroutine chaseCoroutine = null;
    private GameObject chaseTarget = null;
    private Vector2Int? currentDestination = null;
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;
    
    // Système de retry pour pathfinding bloqué
    private Coroutine retryCoroutine = null;
    private int currentRetryCount = 0;
    private const int MAX_RETRY_COUNT = 20; // 1 minute à 3 secondes par tentative
    private const float RETRY_INTERVAL = 3f; // 3 secondes entre chaque tentative
    
    // Optimisation de la poursuite
    private Vector2Int? lastTargetPosition = null;
    private float lastTargetCheckTime = 0f;
    private const float TARGET_CHECK_INTERVAL = 0.2f; // Vérifier la position cible toutes les 0.2 secondes

    // Initialise les références vers les autres composants
    public void Initialize(MovementCore core, CollisionHandler collision)
    {
        movementCore = core;
        collisionHandler = collision;
        
        // S'abonner aux événements si nécessaire
        if (MovementEventSystem.Instance != null)
        {
            // Peut s'abonner à des événements globaux ici si besoin
        }
    }

    // Démarre le suivi d'un chemin vers une destination fixe
    public void StartFollowingPath(Vector2Int destination)
    {
        // Si un retry est en cours pour la même destination, ne pas l'interrompre
        if (retryCoroutine != null && currentDestination.HasValue && currentDestination.Value == destination)
        {
            Debug.Log($"🔄 Retry déjà en cours pour {destination}, ne pas interrompre");
            return;
        }
        // Arrêter seulement le mouvement, garder la chase si active
        StopMovementOnly();
    
        currentDestination = destination;
        Debug.Log($"📍 Démarrage pathfinding vers destination: {destination}");
        
        // Déclencher l'événement de début de mouvement
        MovementEventSystem.TriggerStartMoving(gameObject, destination);
        
        followCoroutine = StartCoroutine(FollowPathCoroutine(destination));
    }

    // Démarre la poursuite dynamique d'une cible mobile
    public void StartChasingTarget(GameObject target)
    {
        // Arrêter tout suivi en cours
        Stop();
        
        chaseTarget = target;
        lastTargetPosition = null;
        lastTargetCheckTime = 0f;
        
        // Déclencher l'événement de début de poursuite
        MovementEventSystem.TriggerStartChasing(gameObject, target);
        
        chaseCoroutine = StartCoroutine(ChaseCoroutineDynamic());
    }

    // Coroutine principale de suivi d'un chemin A*
    private IEnumerator FollowPathCoroutine(Vector2Int destination)
    {
        // Réinitialiser le compteur de retry pour un nouveau pathfinding
        currentRetryCount = 0;
        
        // Gestion du délai Rootborn avant chaque tentative
        if (!movementCore.HasPendingOrder())
        {
            yield return StartCoroutine(movementCore.HandleRootbornDelay());
        }
        
        while (currentDestination.HasValue && currentDestination.Value == destination &&
               this != null && gameObject != null)
        {
            Vector2Int currentPos = movementCore.GetCurrentGridPosition();
            
            // Vérifier si on a atteint la destination
            if (currentPos == destination)
            {
                Debug.Log($"✅ Destination {destination} atteinte !");
                
                // Déclencher l'événement d'arrivée
                MovementEventSystem.TriggerReachDestination(gameObject, destination);
                
                break;
            }
            
            // Calculer le chemin avec A* vers la destination
            List<Vector2Int> path = Pathfinder.Instance.FindPath(currentPos, destination);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"🗺️ PATHFINDING ÉCHEC: {currentPos} → {destination}");
                Debug.LogWarning($"🗺️ Position actuelle bloquée? {GridManager.Instance.IsCellBlocked(currentPos)}");
                Debug.LogWarning($"🗺️ Destination bloquée? {GridManager.Instance.IsCellBlocked(destination)}");
                
                // Démarrer le système de retry au lieu de tout arrêter
                retryCoroutine = StartCoroutine(HandlePathfindingRetry(destination));
                yield return retryCoroutine;
                break;
            }
            
            Debug.Log($"🗺️ Chemin trouvé: {path.Count} cases");
            // Suivre le chemin case par case
            currentPath = path;
            pathIndex = 0;
            
            while (pathIndex < currentPath.Count && currentDestination.HasValue && currentDestination.Value == destination &&
                   this != null && gameObject != null)
            {
                Vector2Int nextStep = currentPath[pathIndex];
                
                // Tenter le déplacement vers la prochaine case
                yield return StartCoroutine(collisionHandler.TryMoveToCell(nextStep));
                
                // Vérifier encore la validité après mouvement
                if (this == null || gameObject == null)
                {
                    Debug.Log("🔄 Arrêt du mouvement - GameObject détruit");
                    yield break;
                }
                
                // Vérifier si on a réussi à bouger (position actuelle = nextStep)
                if (movementCore.GetCurrentGridPosition() == nextStep)
                {
                    pathIndex++;
                    // Réinitialiser le compteur de retry après un mouvement réussi
                    currentRetryCount = 0;
                }
                else
                {
                    // Mouvement bloqué, recalculer le chemin
                    Debug.Log("⏳ Mouvement bloqué, recalcul du chemin...");
                    Pathfinder.Instance.InvalidatePath(currentPos, destination);
                    yield return new WaitForSeconds(0.2f);
                    break; // Sortir de la boucle de suivi pour recalculer
                }
            }
            
            // Si on n'a pas atteint la destination, attendre un peu avant de recalculer
            if (currentPos != destination)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Animation idle à la fin du mouvement
        movementCore.SetIdleAnimation();
        
        // Nettoyage
        currentDestination = null;
        followCoroutine = null;
    }

    // Coroutine de poursuite dynamique avec recalcul automatique optimisé
    private IEnumerator ChaseCoroutineDynamic()
    {
        Debug.Log("🎯 Début de la poursuite dynamique");
    
        while (chaseTarget != null)
        {
            // Optimisation : ne pas vérifier la position à chaque frame
            if (Time.time - lastTargetCheckTime < TARGET_CHECK_INTERVAL)
            {
                yield return new WaitForSeconds(TARGET_CHECK_INTERVAL - (Time.time - lastTargetCheckTime));
                continue;
            }
            
            lastTargetCheckTime = Time.time;
            
            Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(chaseTarget.transform.position);
            Debug.Log($"🎯 Position cible actuelle: {targetGridPos}");
        
            // Vérifier si on est déjà à proximité
            if (IsInAttackRange(movementCore.GetCurrentGridPosition(), targetGridPos))
            {
                Debug.Log("💥 Déjà à proximité de la cible !");
                
                // Arrêter le mouvement mais garder la poursuite active
                StopMovementOnly();
                
                yield return new WaitForSeconds(0.5f);
                continue;
            }
        
            // Recalculer le chemin seulement si la cible a bougé significativement
            if (!lastTargetPosition.HasValue || lastTargetPosition.Value != targetGridPos)
            {
                Debug.Log($"🎯 Cible a bougé de {lastTargetPosition} vers {targetGridPos}");
                lastTargetPosition = targetGridPos;
            
                // Trouver la meilleure destination pour intercepter la cible
                Vector2Int destination = Pathfinder.Instance.FindClosestFreeCell(
                    movementCore.GetCurrentGridPosition(), 
                    targetGridPos,
                    3 // Rayon réduit pour plus de réactivité
                );
            
                Debug.Log($"🎯 Nouvelle destination de poursuite: {destination}");
                
                // Utiliser le pathfinding avec évitement de foule pour la poursuite
                List<Vector2Int> chasePath = Pathfinder.Instance.FindPathWithCrowdAvoidance(
                    movementCore.GetCurrentGridPosition(),
                    destination
                );
                
                if (chasePath.Count > 0)
                {
                    StartFollowingPath(destination);
                }
                else
                {
                    Debug.LogWarning("🎯 Impossible de calculer un chemin vers la cible");
                }
            }
        
            yield return new WaitForSeconds(TARGET_CHECK_INTERVAL);
        }
    
        Debug.Log("🎯 Fin de la poursuite");
        chaseTarget = null;
        lastTargetPosition = null;
        chaseCoroutine = null;
    }

    // Coroutine de retry robuste avec temporisation pour pathfinding bloqué
    private IEnumerator HandlePathfindingRetry(Vector2Int destination)
    {
        Debug.LogWarning($"🔄 Démarrage du système de retry pour destination: {destination}");
        
        // Stopper le coroutine de suivi principal
        if (followCoroutine != null)
        {
            followCoroutine = null;
        }
        
        while (currentRetryCount < MAX_RETRY_COUNT && currentDestination.HasValue && currentDestination.Value == destination &&
               gameObject != null)
        {
            currentRetryCount++;
            float remainingTime = (MAX_RETRY_COUNT - currentRetryCount) * RETRY_INTERVAL;
            
            Debug.LogWarning($"🔄 Tentative {currentRetryCount}/{MAX_RETRY_COUNT} - Temps restant: {remainingTime:F0}s");
            
            // Attendre avant la prochaine tentative
            yield return new WaitForSeconds(RETRY_INTERVAL);
            
            // Vérification de validité de l'objet
            if (this == null || gameObject == null)
            {
                Debug.Log("🔄 Retry annulé - GameObject détruit");
                yield break;
            }
            
            // Vérifier si on doit encore essayer
            if (!currentDestination.HasValue || currentDestination.Value != destination)
            {
                Debug.Log("🔄 Retry annulé - destination changée");
                break;
            }
            
            Vector2Int currentPos = movementCore.GetCurrentGridPosition();
            
            // Vérifier si on a atteint la destination (quelqu'un d'autre nous a bougé)
            if (currentPos == destination)
            {
                Debug.Log($"✅ Destination {destination} atteinte pendant le retry !");
                MovementEventSystem.TriggerReachDestination(gameObject, destination);
                break;
            }
            
            // Essayer de recalculer le chemin
            Debug.Log($"🔄 Nouvelle tentative de pathfinding: {currentPos} → {destination}");
            
            // Nettoyer le cache pour forcer un recalcul frais
            Pathfinder.Instance.InvalidatePath(currentPos, destination);
            
            List<Vector2Int> retryPath = Pathfinder.Instance.FindPath(currentPos, destination);
            
            if (retryPath != null && retryPath.Count > 0)
            {
                Debug.Log($"🔄 ✅ Chemin trouvé après {currentRetryCount} tentatives ! Reprendre le mouvement.");
                // Relancer le pathfinding principal
                retryCoroutine = null;
                currentRetryCount = 0;
                // Relancer le pathfinding principal
                followCoroutine = StartCoroutine(FollowPathCoroutine(destination));
                yield break;
            }
            else
            {
                Debug.LogWarning($"🔄 ❌ Tentative {currentRetryCount} échouée - chemin toujours bloqué");
                
                // Essayer de trouver une destination alternative proche
                if (currentRetryCount % 5 == 0) // Tous les 5 essais
                {
                    Vector2Int? alternativeCell = GridManager.Instance.GetNearestFreeCell(destination, 5);
                    if (alternativeCell.HasValue && alternativeCell.Value != currentPos)
                    {
                        Debug.Log($"🔄 Tentative avec destination alternative: {alternativeCell.Value}");
                        List<Vector2Int> altPath = Pathfinder.Instance.FindPath(currentPos, alternativeCell.Value);
                        if (altPath != null && altPath.Count > 0)
                        {
                            Debug.Log($"🔄 ✅ Chemin alternatif trouvé vers {alternativeCell.Value}");
                            currentDestination = alternativeCell.Value;
                            followCoroutine = StartCoroutine(FollowPathCoroutine(alternativeCell.Value));
                            retryCoroutine = null;
                            yield break;
                        }
                    }
                }
            }
        }
        
        // Si on arrive ici, soit on a épuisé les tentatives, soit la destination a été annulée
        if (currentRetryCount >= MAX_RETRY_COUNT)
        {
            Debug.LogError($"🔄 ❌ Échec définitif après {MAX_RETRY_COUNT} tentatives (60 secondes)");
            Debug.LogError($"🔄 Impossible d'atteindre la destination {destination}");
            
            // Déclencher l'événement d'échec/blocage
            MovementEventSystem.TriggerUnitBlocked(gameObject, destination);
        }
        
        // Nettoyage final
        currentDestination = null;
        currentRetryCount = 0;
        retryCoroutine = null;
        
        // Animation idle
        movementCore.SetIdleAnimation();
        
        // Déclencher l'événement d'arrêt
        MovementEventSystem.TriggerStopMoving(gameObject);
    }

    // Vérifie si l'unité est à portée d'attaque de la cible (8 cases adjacentes)
    private bool IsInAttackRange(Vector2Int unitPos, Vector2Int targetPos)
    {
        int deltaX = Mathf.Abs(unitPos.x - targetPos.x);
        int deltaY = Mathf.Abs(unitPos.y - targetPos.y);
        
        // À portée si dans les 8 cases adjacentes
        return deltaX <= 1 && deltaY <= 1 && (deltaX + deltaY > 0);
    }

    // Arrête tout suivi en cours
    public void Stop()
    {
        StopMovementOnly();
        
        // Arrêter aussi la poursuite
        if (chaseTarget != null)
        {
            chaseTarget = null;
            lastTargetPosition = null;
        }
        
        if (chaseCoroutine != null)
        {
            StopCoroutine(chaseCoroutine);
            chaseCoroutine = null;
        }
        
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }
        
        Debug.Log("🛑 Arrêt complet du PathfinderFollower");
        
        // Déclencher l'événement d'arrêt
        MovementEventSystem.TriggerStopMoving(gameObject);
    }
    
    // Arrête seulement le mouvement sans arrêter la poursuite
    private void StopMovementOnly()
    {
        Debug.Log("🛑 Arrêt du mouvement seulement");
    
        if (followCoroutine != null)
        {
            StopCoroutine(followCoroutine);
            followCoroutine = null;
        }
    
        // Ne pas effacer currentDestination si on est en chase
        if (chaseTarget == null)
        {
            currentDestination = null;
        }
        
        currentPath.Clear();
        pathIndex = 0;
    }

    // Indique si l'unité suit actuellement un chemin
    public bool IsFollowingPath()
    {
        return followCoroutine != null;
    }

    // Indique si l'unité poursuit une cible
    public bool IsChasingTarget()
    {
        return chaseCoroutine != null && chaseTarget != null;
    }
    
    // Obtient la cible actuelle de poursuite
    public GameObject GetChaseTarget()
    {
        return chaseTarget;
    }
    
    // Obtient la destination actuelle
    public Vector2Int? GetCurrentDestination()
    {
        return currentDestination;
    }
}