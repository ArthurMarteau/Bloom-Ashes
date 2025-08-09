using System.Collections;
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

    // Tente un déplacement vers une case avec gestion des collisions
    public IEnumerator TryMoveToCell(Vector2Int targetCell)
    {
        Vector2Int currentPos = movementCore.GetCurrentGridPosition();
        
        // Vérifier si la case cible est occupée
        GameObject occupant = GridManager.Instance.GetOccupant(targetCell);
        if (occupant != null && occupant != this.gameObject)
        {
            CollisionHandler otherHandler = occupant.GetComponent<CollisionHandler>();
            if (otherHandler != null && !otherHandler.isBeingPushedBack && otherHandler.isMovingToCell)
            {
                // Système de priorité basé sur la proximité
                Vector3 targetWorldPos = GridManager.Instance.GridToWorld(targetCell.x, targetCell.y);
                bool shouldIPushOther = ShouldPushOtherUnit(otherHandler, targetWorldPos);
                
                if (shouldIPushOther)
                {
                    Debug.Log($"🚀 {gameObject.name} pousse {otherHandler.gameObject.name}");
                    otherHandler.ForceReturnToPrevious();
                    yield return new WaitForSeconds(0.1f);
                    
                    // Réessayer après avoir poussé l'autre unité
                    yield return StartCoroutine(TryMoveToCell(targetCell));
                    yield break;
                }
                else
                {
                    Debug.Log($"🔄 {gameObject.name} recule face à {otherHandler.gameObject.name}");
                    yield return StartCoroutine(AnimateReturnToPrevious());
                    yield break;
                }
            }
            else
            {
                Debug.Log("❌ Case bloquée par un obstacle ou une unité statique !");
                yield return StartCoroutine(AnimateReturnToPrevious());
                yield break;
            }
        }
        
        // Case libre, procéder au mouvement
        yield return StartCoroutine(AnimateMovementToCell(targetCell));
    }

    // Anime le mouvement fluide vers une case
    private IEnumerator AnimateMovementToCell(Vector2Int targetCell)
    {
        isMovingToCell = true;
        Vector2Int startCell = movementCore.GetCurrentGridPosition();
        
        // Enregistrer la nouvelle position avant de commencer le mouvement
        GridManager.Instance.RegisterOccupiedCell(targetCell, gameObject);
        
        // Calculer les positions monde
        Vector3 startPos = GridManager.Instance.GridToWorld(startCell.x, startCell.y);
        Vector3 targetPos = GridManager.Instance.GridToWorld(targetCell.x, targetCell.y);
        
        float totalDistance = Vector3.Distance(startPos, targetPos);
        float movedDistance = 0f;
        
        // Animation du mouvement
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            Vector3 direction = targetPos - transform.position;
            animatorController.PlayWalkAnimation(direction);
            
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
                    yield return StartCoroutine(AnimateReturnToPrevious());
                    yield break;
                }
                else
                {
                    Debug.Log("💀 Unité détruite par un obstacle qui se ferme");
                    GridManager.Instance.UnregisterOccupiedCell(movementCore.GetCurrentGridPosition());
                    Destroy(gameObject);
                    yield break;
                }
            }
            
            yield return null;
        }
        
        // Finaliser le déplacement
        transform.position = targetPos;
        animatorController.PlayWalkAnimation(Vector2.zero);
        
        // Mettre à jour les positions
        lastTraversedPosition = movementCore.GetCurrentGridPosition();
        movementCore.UpdateGridPosition(targetCell);
        
        isMovingToCell = false;
        
        // Vérifier s'il y a un ordre en attente
        movementCore.ExecutePendingOrder();
    }

    // Anime le retour vers la position précédente (pushback)
    private IEnumerator AnimateReturnToPrevious()
    {
        isBeingPushedBack = true;
        
        Vector3 returnPos = GridManager.Instance.GridToWorld(lastTraversedPosition.x, lastTraversedPosition.y);
        
        // Réenregistrer l'ancienne position
        GridManager.Instance.RegisterOccupiedCell(lastTraversedPosition, gameObject);
        GridManager.Instance.UnregisterOccupiedCell(movementCore.GetCurrentGridPosition());
        
        Debug.Log($"🔙 Pushback vers {lastTraversedPosition}");
        
        // Animation du retour
        while (Vector3.Distance(transform.position, returnPos) > 0.01f)
        {
            Vector3 direction = returnPos - transform.position;
            animatorController.PlayWalkAnimation(direction);
            transform.position = Vector3.MoveTowards(transform.position, returnPos, pushbackSpeed * Time.deltaTime);
            yield return null;
        }
        
        // Finaliser le retour
        transform.position = returnPos;
        animatorController.PlayWalkAnimation(Vector2.zero);
        movementCore.UpdateGridPosition(lastTraversedPosition);
        
        isBeingPushedBack = false;
    }

    // Détermine si cette unité doit pousser l'autre unité
    private bool ShouldPushOtherUnit(CollisionHandler otherHandler, Vector3 targetPos)
    {
        // Calculer les distances vers la case cible
        float myDistance = Vector3.Distance(transform.position, targetPos);
        float otherDistance = Vector3.Distance(otherHandler.transform.position, targetPos);
        
        // Priorité 1 : L'unité la plus proche de la case cible
        if (Mathf.Abs(myDistance - otherDistance) > 0.01f)
        {
            return myDistance < otherDistance;
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