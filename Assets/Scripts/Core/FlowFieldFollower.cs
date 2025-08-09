using System.Collections;
using UnityEngine;

public class FlowFieldFollower : MonoBehaviour
{
    // Composants liés
    private MovementCore movementCore;
    private CollisionHandler collisionHandler;
    
    // État du suivi
    private Coroutine followCoroutine = null;
    private Coroutine chaseCoroutine = null;
    private GameObject chaseTarget = null;
    private Vector2Int? currentDestination = null;

    // Initialise les références vers les autres composants
    public void Initialize(MovementCore core, CollisionHandler collision)
    {
        movementCore = core;
        collisionHandler = collision;
    }

    // Démarre le suivi d'un flow field vers une destination fixe
    public void StartFollowingField(Vector2Int destination)
    {
        // Arrêter tout suivi en cours
        Stop();
        
        currentDestination = destination;
        followCoroutine = StartCoroutine(FollowFieldCoroutine(destination));
    }

    // Démarre la poursuite dynamique d'une cible mobile
    public void StartChasingTarget(GameObject target)
    {
        // Arrêter tout suivi en cours
        Stop();
        
        chaseTarget = target;
        chaseCoroutine = StartCoroutine(ChaseCoroutineDynamic());
    }

    // Coroutine principale de suivi d'un flow field
    private IEnumerator FollowFieldCoroutine(Vector2Int destination)
    {
        // Gestion du délai Rootborn avant chaque tentative
        if (!movementCore.HasPendingOrder())
        {
            yield return StartCoroutine(movementCore.HandleRootbornDelay());
        }
        
        while (currentDestination.HasValue)
        {
            Vector2Int currentPos = movementCore.GetCurrentGridPosition();
            
            // Vérifier si on a atteint la destination
            if (currentPos == destination)
            {
                Debug.Log($"✅ Destination {destination} atteinte !");
                break;
            }
            
            // Obtenir le flow field vers la destination
            FlowField flowField = FlowFieldManager.Instance.GetFlowField(destination);
            if (flowField == null)
            {
                Debug.LogError("Impossible de générer le flow field !");
                break;
            }
            
            // Obtenir la prochaine direction depuis le flow field
            Vector2Int nextStep = GetNextStepFromFlowField(currentPos, flowField);
            
            // Si aucune direction valide, attendre et réessayer
            if (nextStep == currentPos)
            {
                Debug.Log("⏳ Aucun mouvement possible, nouvelle tentative...");
                yield return new WaitForSeconds(0.2f);
                continue;
            }
            
            // Tenter le déplacement vers la prochaine case
            yield return StartCoroutine(collisionHandler.TryMoveToCell(nextStep));
        }
        
        // Nettoyage
        currentDestination = null;
        followCoroutine = null;
    }

    // Coroutine de poursuite dynamique avec recalcul automatique
    private IEnumerator ChaseCoroutineDynamic()
    {
        Vector2Int? lastTargetPosition = null;
        
        while (chaseTarget != null)
        {
            Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(chaseTarget.transform.position);
            // Vérifier si on est déjà à proximité (dans les 8 cases autour)
            if (IsInAttackRange(movementCore.GetCurrentGridPosition(), targetGridPos))
            {
                Debug.Log("💥 Déjà à proximité de la cible !");
                yield return new WaitForSeconds(0.5f);
                continue; // Pas besoin de bouger, on surveille juste si la cible bouge
            }
            
            // Recalculer le flow field si la cible a bougé
            if (lastTargetPosition != targetGridPos)
            {
                lastTargetPosition = targetGridPos;
                
                // Utiliser FindClosestFreeCell pour trouver une position accessible
                Vector2Int destination = FlowFieldManager.Instance.FindClosestFreeCell(
                    movementCore.GetCurrentGridPosition(), 
                    targetGridPos
                );
                
                // Démarrer le suivi vers cette position
                StartFollowingField(destination);
                
                // Attendre que le suivi se termine ou que la cible bouge
                while (followCoroutine != null && chaseTarget != null)
                {
                    Vector2Int newTargetPos = GridManager.Instance.WorldToGrid(chaseTarget.transform.position);
                    
                    // Si la cible a bougé, sortir de la boucle pour recalculer
                    if (newTargetPos != targetGridPos)
                    {
                        Stop(); // Arrêter le mouvement actuel
                        break;
                    }
                    
                    // Vérifier si on est à portée d'attaque (cases adjacentes)
                    if (IsInAttackRange(movementCore.GetCurrentGridPosition(), newTargetPos))
                    {
                        Debug.Log("💥 Cible à portée d'attaque !");
                        yield return new WaitForSeconds(0.5f);
                    }
                    
                    yield return new WaitForSeconds(0.1f);
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // Nettoyage
        chaseTarget = null;
        chaseCoroutine = null;
    }

    // Calcule la prochaine case à partir du flow field
    private Vector2Int GetNextStepFromFlowField(Vector2Int currentPos, FlowField flowField)
    {
        Vector2Int direction = flowField.GetDirection(currentPos);
        
        // Si aucune direction (obstacle ou destination atteinte)
        if (direction == Vector2Int.zero)
            return currentPos;
        
        Vector2Int nextStep = currentPos + direction;
        
        // Vérifier que la prochaine case est dans les limites
        if (!GridManager.Instance.IsWithinBounds(nextStep))
            return currentPos;
        
        return nextStep;
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
        if (followCoroutine != null)
        {
            StopCoroutine(followCoroutine);
            followCoroutine = null;
        }
        
        if (chaseCoroutine != null)
        {
            StopCoroutine(chaseCoroutine);
            chaseCoroutine = null;
        }
        
        currentDestination = null;
        chaseTarget = null;
    }

    // Indique si l'unité suit actuellement un flow field
    public bool IsFollowingField()
    {
        return followCoroutine != null;
    }

    // Indique si l'unité poursuit une cible
    public bool IsChasingTarget()
    {
        return chaseCoroutine != null;
    }
}