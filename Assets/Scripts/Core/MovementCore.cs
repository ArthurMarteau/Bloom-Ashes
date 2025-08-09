using System.Collections;
using UnityEngine;

public class MovementCore : MonoBehaviour
{
    [SerializeField] private float baseMovementSpeed = 1f;
    
    // Composants internes
    private FlowFieldFollower flowFieldFollower;
    private CollisionHandler collisionHandler;
    private UnitAnimatorController animatorController;
    
    // État de l'unité
    private Vector2Int currentGridPosition;
    private Vector2Int? pendingTarget = null;

    // Initialie l'unité et ses composants
    private void Start()
    {
        // Récupérer les composants
        flowFieldFollower = GetComponent<FlowFieldFollower>();
        collisionHandler = GetComponent<CollisionHandler>();
        animatorController = GetComponent<UnitAnimatorController>();
        
        // Initialiser la position sur la grille
        currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
        
        // Enregistrer l'unité sur la grille
        GridManager.Instance.RegisterOccupiedCell(currentGridPosition, gameObject);
        
        // Lier les composants entre eux
        flowFieldFollower.Initialize(this, collisionHandler);
        collisionHandler.Initialize(this, animatorController, baseMovementSpeed);
    }

    // Point d'entrée principal pour tous les déplacements
    public void TryMoveTo(Vector2Int newTarget)
    {
        // Si l'unité est en cours de mouvement vers une case
        if (collisionHandler.IsMovingToCell())
        {
            // Aller à la case la plus proche avant d'exécuter le nouvel ordre
            Vector2Int closestCell = collisionHandler.GetClosestGridPosition();
            UpdateGridPosition(closestCell);
    
            // Téléporter à cette case
            Vector3 targetPos = GridManager.Instance.GridToWorld(closestCell.x, closestCell.y);
            transform.position = targetPos;
    
            pendingTarget = newTarget;
            flowFieldFollower.Stop();
        }
        else
        {
            // L'unité est statique sur un nœud, démarrer immédiatement
            flowFieldFollower.StartFollowingField(newTarget);
        }
    }

    // Lance la poursuite d'une unité mobile
    public void ChaseTarget(GameObject target)
    {
        // Annuler tout mouvement en cours
        CancelAllMovement();
        
        // Démarrer la poursuite
        flowFieldFollower.StartChasingTarget(target);
    }

    // Arrête tous les mouvements en cours
    public void CancelAllMovement()
    {
        pendingTarget = null;
        flowFieldFollower.Stop();
        collisionHandler.CancelPushback();
    }

    // Exécute un ordre en attente une fois arrivé sur un nœud
    public void ExecutePendingOrder()
    {
        if (pendingTarget.HasValue)
        {
            Vector2Int target = pendingTarget.Value;
            pendingTarget = null;
            
            // Maintenant qu'on est sur un nœud, exécuter le nouvel ordre
            flowFieldFollower.StartFollowingField(target);
        }
    }

    // Gère le délai spécifique aux unités Rootborn
    public IEnumerator HandleRootbornDelay()
    {
        Rootborn rootbornComponent = GetComponent<Rootborn>();
        if (rootbornComponent != null && !collisionHandler.IsBeingPushedBack())
        {
            animatorController.PlayIdleReversed();
            yield return new WaitForSeconds(1f);
            animatorController.PlayWalkAnimation(Vector2.zero);
        }
    }

    // Met à jour la position actuelle sur la grille
    public void UpdateGridPosition(Vector2Int newPosition)
    {
        // Désengeger l'ancienne position
        GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
        
        // Enregistrer la nouvelle position
        currentGridPosition = newPosition;
        GridManager.Instance.RegisterOccupiedCell(currentGridPosition, gameObject);
    }

    // Accesseurs pour les autres composants
    public Vector2Int GetCurrentGridPosition()
    {
        return currentGridPosition;
    }

    public float GetMovementSpeed()
    {
        return baseMovementSpeed;
    }

    public bool HasPendingOrder()
    {
        return pendingTarget.HasValue;
    }
}