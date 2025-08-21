using System.Collections;
using Core;
using UnityEngine;

public class CollisionHandler : MonoBehaviour
{
    // Composants liés
    private MovementCore movementCore;
    private UnitAnimatorController animatorController;
    
    // Configuration du mouvement
    private float movementSpeed;
    private float pushbackSpeed;
    
    // État du mouvement
    private bool isMovingToCell = false;
    private bool isBeingPushedBack = false;
    private Vector2Int lastTraversedPosition;
    private Coroutine currentMovementCoroutine = null;
    
    // Protection contre les boucles infinies
    private const int MAX_COLLISION_RETRIES = 3;
    private const float COLLISION_RETRY_DELAY = 0.1f;

    // Initialise les références et la configuration
    public void Initialize(MovementCore core, UnitAnimatorController animator, float baseSpeed)
    {
        movementCore = core;
        animatorController = animator;
        movementSpeed = baseSpeed;
        pushbackSpeed = baseSpeed * 2f; // Pushback plus rapide
        
        // Initialiser la dernière position parcourue
        lastTraversedPosition = movementCore.GetCurrentGridPosition();
    }

    // Tente un déplacement vers une case avec gestion des collisions avancée
    public IEnumerator TryMoveToCell(Vector2Int targetCell)
    {
        Vector2Int currentPos = movementCore.GetCurrentGridPosition();
        
        // Vérifier si la case cible est occupée
        GameObject occupant = GridManager.Instance.GetOccupant(targetCell);
        Debug.Log($"🔍 Case {targetCell} occupée par: {(occupant != null ? occupant.name : "LIBRE")}");
        
        if (occupant != null && occupant != this.gameObject)
        {
            Debug.Log($"🔍 Occupant est différent de {gameObject.name}");
            
            // Déclencher l'événement de collision
            MovementEventSystem.TriggerUnitCollision(gameObject, occupant);
            
            CollisionHandler otherHandler = occupant.GetComponent<CollisionHandler>();
            if (otherHandler != null && !otherHandler.isBeingPushedBack && otherHandler.isMovingToCell)
            {
                // Système de priorité basé sur l'avancement sur la case
                bool shouldIPushOther = ShouldPushOtherUnit(otherHandler, targetCell);
                
                if (shouldIPushOther)
                {
                    Debug.Log($"🚀 {gameObject.name} pousse {otherHandler.gameObject.name}");
                    otherHandler.ForceReturnToPrevious();
                    
                    // Attendre un peu et réessayer avec protection contre boucle infinie
                    yield return new WaitForSeconds(COLLISION_RETRY_DELAY);
                    
                    // Réessayer avec un compteur pour éviter les boucles infinies
                    int retryCount = 0;
                    while (retryCount < MAX_COLLISION_RETRIES)
                    {
                        yield return new WaitForSeconds(COLLISION_RETRY_DELAY);
                        GameObject newOccupant = GridManager.Instance.GetOccupant(targetCell);
                        
                        if (newOccupant == null)
                        {
                            // La case est maintenant libre, procéder au mouvement
                            yield return StartCoroutine(AnimateMovementToCell(targetCell));
                            yield break;
                        }
                        
                        retryCount++;
                        Debug.Log($"⏳ Tentative {retryCount}/{MAX_COLLISION_RETRIES} de mouvement vers {targetCell}");
                    }
                    
                    // Échec après plusieurs tentatives
                    if (retryCount >= MAX_COLLISION_RETRIES)
                    {
                        Debug.LogWarning($"❌ Échec après {MAX_COLLISION_RETRIES} tentatives vers {targetCell}");
                        MovementEventSystem.TriggerUnitBlocked(gameObject, targetCell);
                        yield return StartCoroutine(AnimateReturnToPrevious());
                    }
                }
                else
                {
                    Debug.Log($"🔄 {gameObject.name} recule face à {otherHandler.gameObject.name}");
                    yield return StartCoroutine(AnimateReturnToPrevious());
                }
            }
            else
            {
                Debug.Log("❌ Case bloquée par un obstacle ou une unité statique !");
                MovementEventSystem.TriggerUnitBlocked(gameObject, targetCell);
                yield return StartCoroutine(AnimateReturnToPrevious());
            }
        }
        else
        {
            // Case libre, procéder au mouvement fluide
            yield return StartCoroutine(AnimateMovementToCell(targetCell));
        }
    }

    // Anime le mouvement fluide vers une case SANS téléportation
    private IEnumerator AnimateMovementToCell(Vector2Int targetCell)
    {
        isMovingToCell = true;
        Vector2Int startCell = movementCore.GetCurrentGridPosition();
        
        // Calculer les positions monde
        Vector3 startPos = transform.position;
        Vector3 targetPos = GridManager.Instance.GridToWorld(targetCell.x, targetCell.y);
        
        // Vérifier que le mouvement est cohérent (pas de téléportation)
        float distance = Vector3.Distance(startPos, targetPos);
        if (distance > 2f) // Plus d'une case de distance = problème
        {
            Debug.LogError($"⚠️ Tentative de téléportation détectée ! Distance: {distance}");
            // Corriger en téléportant à la position de la grille actuelle
            transform.position = GridManager.Instance.GridToWorld(startCell.x, startCell.y);
            startPos = transform.position;
            
            // Recalculer la distance après correction
            distance = Vector3.Distance(startPos, targetPos);
            if (distance > 2f)
            {
                Debug.LogError($"⚠️ Téléportation impossible même après correction. Annulation du mouvement.");
                isMovingToCell = false;
                yield break;
            }
        }
        
        // Enregistrer la nouvelle position sur la grille AVANT le mouvement pour réserver
        GridManager.Instance.UnregisterOccupiedCell(startCell);
        GridManager.Instance.RegisterOccupiedCell(targetCell, gameObject);
        
        float totalDistance = Vector3.Distance(startPos, targetPos);
        float movedDistance = 0f;
        
        // Direction pour l'animation
        Vector2 moveDirection = (targetPos - startPos).normalized;
        if (animatorController != null && moveDirection != Vector2.zero)
        {
            animatorController.PlayWalkAnimation(moveDirection);
        }
        
        // Animation du mouvement fluide
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            float stepMove = movementSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, stepMove);
            movedDistance += stepMove;
            
            // Vérifier si un obstacle apparaît pendant le mouvement (pont qui se ferme)
            if (GridManager.Instance.IsCellBlocked(targetCell))
            {
                float progress = movedDistance / totalDistance;
                if (progress < 0.5f)
                {
                    Debug.Log("⛔ Obstacle apparu, retour en arrière");
                    // Remettre la grille dans l'état précédent
                    GridManager.Instance.UnregisterOccupiedCell(targetCell);
                    GridManager.Instance.RegisterOccupiedCell(startCell, gameObject);
                    
                    // Déclencher l'événement de blocage
                    MovementEventSystem.TriggerUnitBlocked(gameObject, targetCell);
                    
                    yield return StartCoroutine(AnimateReturnToPrevious());
                    yield break;
                }
                else
                {
                    Debug.Log("💀 Unité détruite par un obstacle qui se ferme");
                    GridManager.Instance.UnregisterOccupiedCell(targetCell);
                    Destroy(gameObject);
                    yield break;
                }
            }
            
            yield return null;
        }
        
        // Finaliser le déplacement - s'assurer de la position exacte
        transform.position = targetPos;
        
        // Mettre à jour les positions
        lastTraversedPosition = startCell;
        movementCore.UpdateGridPosition(targetCell);
        
        isMovingToCell = false;
        
        // Vérifier s'il y a un ordre en attente
        movementCore.ExecutePendingOrder();
    }

    // Anime le retour vers la position précédente (pushback) SANS téléportation
    private IEnumerator AnimateReturnToPrevious()
    {
        isBeingPushedBack = true;
        
        // Déclencher l'événement de pushback
        MovementEventSystem.TriggerUnitPushedBack(gameObject);
        
        Vector3 currentPos = transform.position;
        Vector3 returnPos = GridManager.Instance.GridToWorld(lastTraversedPosition.x, lastTraversedPosition.y);
        
        // Vérifier la cohérence du retour
        float distance = Vector3.Distance(currentPos, returnPos);
        if (distance > 2f) // Plus d'une case = problème
        {
            Debug.LogError($"⚠️ Pushback incohérent détecté ! Distance: {distance}");
            // Utiliser la position actuelle comme point de départ
            Vector2Int currentGrid = GridManager.Instance.WorldToGrid(currentPos);
            lastTraversedPosition = currentGrid;
            returnPos = GridManager.Instance.GridToWorld(currentGrid.x, currentGrid.y);
            
            // Si toujours incohérent, annuler le pushback
            distance = Vector3.Distance(currentPos, returnPos);
            if (distance > 2f)
            {
                Debug.LogError($"⚠️ Pushback impossible. Annulation.");
                isBeingPushedBack = false;
                yield break;
            }
        }
        
        // Réenregistrer l'ancienne position
        GridManager.Instance.UnregisterOccupiedCell(movementCore.GetCurrentGridPosition());
        GridManager.Instance.RegisterOccupiedCell(lastTraversedPosition, gameObject);
        
        Debug.Log($"🔙 Pushback fluide vers {lastTraversedPosition}");
        
        // Direction pour l'animation
        Vector2 moveDirection = (returnPos - currentPos).normalized;
        if (animatorController != null && moveDirection != Vector2.zero)
        {
            animatorController.PlayWalkAnimation(moveDirection);
        }
        
        // Animation du retour fluide
        while (Vector3.Distance(transform.position, returnPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, returnPos, pushbackSpeed * Time.deltaTime);
            yield return null;
        }
        
        // Finaliser le retour
        transform.position = returnPos;
        if (animatorController != null)
        {
            animatorController.PlayWalkAnimation(Vector2.zero); // Animation idle
        }
        movementCore.UpdateGridPosition(lastTraversedPosition);
        
        isBeingPushedBack = false;
    }

    // Détermine si cette unité doit pousser l'autre unité (priorité basée sur l'avancement)
    private bool ShouldPushOtherUnit(CollisionHandler otherHandler, Vector2Int targetCell)
    {
        // Calculer l'avancement de chaque unité vers la case cible
        Vector3 targetPos = GridManager.Instance.GridToWorld(targetCell.x, targetCell.y);
        float myDistanceToTarget = Vector3.Distance(transform.position, targetPos);
        float otherDistanceToTarget = Vector3.Distance(otherHandler.transform.position, targetPos);
        
        // Priorité 1 : L'unité la plus avancée (plus proche de la case cible) est prioritaire
        if (Mathf.Abs(myDistanceToTarget - otherDistanceToTarget) > 0.1f)
        {
            return myDistanceToTarget < otherDistanceToTarget;
        }
        
        // Priorité 2 : Fallback déterministe avec l'ID de l'objet
        return gameObject.GetInstanceID() < otherHandler.gameObject.GetInstanceID();
    }

    // Force le retour à la position précédente
    public void ForceReturnToPrevious()
    {
        if (!isBeingPushedBack && currentMovementCoroutine == null)
        {
            currentMovementCoroutine = StartCoroutine(AnimateReturnToPrevious());
        }
    }

    // Annule un pushback en cours
    public void CancelPushback()
    {
        if (currentMovementCoroutine != null)
        {
            StopCoroutine(currentMovementCoroutine);
            currentMovementCoroutine = null;
        }
        isBeingPushedBack = false;
        isMovingToCell = false;
    }

    // Accesseurs pour l'état
    public bool IsMovingToCell()
    {
        return isMovingToCell;
    }

    public bool IsBeingPushedBack()
    {
        return isBeingPushedBack;
    }
    
    // Détermine la cellule la plus proche quand l'unité est entre deux cases
    public Vector2Int GetClosestGridPosition()
    {
        Vector2Int currentGrid = GridManager.Instance.WorldToGrid(transform.position);
        Vector3 currentWorldPos = GridManager.Instance.GridToWorld(currentGrid.x, currentGrid.y);
    
        // Si très proche du centre de la case actuelle, on y est
        if (Vector3.Distance(transform.position, currentWorldPos) < 0.3f)
        {
            return currentGrid;
        }
    
        // Sinon, trouver la case la plus proche parmi les 8 voisins
        Vector2Int closestCell = currentGrid;
        float closestDistance = Vector3.Distance(transform.position, currentWorldPos);
    
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                Vector2Int neighborCell = new Vector2Int(currentGrid.x + dx, currentGrid.y + dy);
                Vector3 neighborWorldPos = GridManager.Instance.GridToWorld(neighborCell.x, neighborCell.y);
                float distance = Vector3.Distance(transform.position, neighborWorldPos);
            
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestCell = neighborCell;
                }
            }
        }
    
        return closestCell;
    }
}