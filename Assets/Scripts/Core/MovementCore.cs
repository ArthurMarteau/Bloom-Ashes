using System.Collections;
using UnityEngine;

public class MovementCore : MonoBehaviour
{
    [SerializeField] private float baseMovementSpeed = 1f;
    
    // Composants internes
    private PathfinderFollower pathfinderFollower;
    private CollisionHandler collisionHandler;
    private UnitAnimatorController animatorController;
    
    // État de l'unité
    private Vector2Int currentGridPosition;
    private Vector2Int? pendingTarget = null;
    
    // Protection contre les ordres dupliqués
    private Vector2Int? lastOrderedTarget = null;

    // Initialise l'unité et ses composants
    private void Start()
    {
        // Récupérer les composants
        pathfinderFollower = GetComponent<PathfinderFollower>();
        collisionHandler = GetComponent<CollisionHandler>();
        animatorController = GetComponent<UnitAnimatorController>();
        
        // Initialiser la position sur la grille
        currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
        
        // Enregistrer l'unité sur la grille
        GridManager.Instance.RegisterOccupiedCell(currentGridPosition, gameObject);
        
        // Lier les composants entre eux
        pathfinderFollower.Initialize(this, collisionHandler);
        collisionHandler.Initialize(this, animatorController, baseMovementSpeed);
        
        Debug.Log($"🚀 {gameObject.name} initialisé à la position {currentGridPosition}");
    }

    // Point d'entrée principal pour tous les déplacements
    public void TryMoveTo(Vector2Int newTarget)
    {
        // Vérifier si on est déjà en train de traiter cette destination
        if (pendingTarget.HasValue && pendingTarget.Value == newTarget)
        {
            Debug.Log($"⚠️ Ordre déjà en attente pour la destination {newTarget}");
            return;
        }
        
        // Vérifier si c'est le même ordre que le précédent
        if (lastOrderedTarget.HasValue && lastOrderedTarget.Value == newTarget && 
            pathfinderFollower.GetCurrentDestination().HasValue &&
            pathfinderFollower.GetCurrentDestination().Value == newTarget)
        {
            Debug.Log($"⚠️ Déjà en route vers {newTarget}");
            return;
        }
        
        lastOrderedTarget = newTarget;
        
        // Si l'unité est en cours de mouvement vers une case
        if (collisionHandler.IsMovingToCell())
        {
            // Aller à la case la plus proche avant d'exécuter le nouvel ordre
            Vector2Int closestCell = collisionHandler.GetClosestGridPosition();
            
            // Vérifier la cohérence du mouvement (anti-téléportation)
            float distanceToClosest = Vector3.Distance(
                transform.position, 
                GridManager.Instance.GridToWorld(closestCell.x, closestCell.y)
            );
            
            if (distanceToClosest > 1.5f) // Plus d'une case et demie = incohérent
            {
                Debug.LogWarning($"⚠️ Téléportation évitée ! Distance: {distanceToClosest}");
                // Utiliser la position de grille actuelle au lieu de téléporter
                closestCell = currentGridPosition;
            }
            
            // Si la case la plus proche est différente de notre position actuelle enregistrée
            if (closestCell != currentGridPosition)
            {
                UpdateGridPosition(closestCell);
                
                // Déplacer fluide vers cette case sans téléportation
                Vector3 targetPos = GridManager.Instance.GridToWorld(closestCell.x, closestCell.y);
                StartCoroutine(MoveToPositionSmoothly(targetPos, closestCell));
            }
    
            pendingTarget = newTarget;
            pathfinderFollower.Stop();
            
            Debug.Log($"📋 Nouvel ordre en attente: {newTarget}");
        }
        else
        {
            // L'unité est statique sur un nœud, démarrer immédiatement
            pathfinderFollower.StartFollowingPath(newTarget);
        }
    }

    // Lance la poursuite d'une unité mobile
    public void ChaseTarget(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("⚠️ Tentative de poursuite d'une cible nulle");
            return;
        }
        
        if (target == gameObject)
        {
            Debug.LogWarning("⚠️ Une unité ne peut pas se poursuivre elle-même");
            return;
        }
        
        // Vérifier si on poursuit déjà cette cible
        if (pathfinderFollower.IsChasingTarget() && pathfinderFollower.GetChaseTarget() == target)
        {
            Debug.Log($"⚠️ Déjà en train de poursuivre {target.name}");
            return;
        }
        
        // Annuler tout mouvement en cours
        CancelAllMovement();
        
        // Démarrer la poursuite
        pathfinderFollower.StartChasingTarget(target);
        
        Debug.Log($"🎯 Début de la poursuite de {target.name}");
    }

    // Arrête tous les mouvements en cours
    public void CancelAllMovement()
    {
        pendingTarget = null;
        lastOrderedTarget = null;
        pathfinderFollower.Stop();
        collisionHandler.CancelPushback();
        
        Debug.Log($"🛑 Tous les mouvements annulés pour {gameObject.name}");
    }

    // Exécute un ordre en attente une fois arrivé sur un nœud
    public void ExecutePendingOrder()
    {
        if (pendingTarget.HasValue)
        {
            Vector2Int target = pendingTarget.Value;
            pendingTarget = null;
            
            Debug.Log($"📋 Exécution de l'ordre en attente: {target}");
            
            // Maintenant qu'on est sur un nœud, exécuter le nouvel ordre
            pathfinderFollower.StartFollowingPath(target);
        }
    }

    // Gère le délai spécifique aux unités Rootborn
    public IEnumerator HandleRootbornDelay()
    {
        Rootborn rootbornComponent = GetComponent<Rootborn>();
        if (rootbornComponent != null && !collisionHandler.IsBeingPushedBack())
        {
            Debug.Log($"🌱 Délai Rootborn pour {gameObject.name}");
            
            if (animatorController != null)
            {
                animatorController.PlayIdleReversed();
            }
            yield return new WaitForSeconds(1f);
            if (animatorController != null)
            {
                animatorController.PlayWalkAnimation(Vector2.zero);
            }
        }
    }

    // Déplacement fluide vers une position sans téléportation
    private IEnumerator MoveToPositionSmoothly(Vector3 targetWorldPos, Vector2Int targetGridPos)
    {
        float moveSpeed = baseMovementSpeed * 3f; // Plus rapide pour ce type de mouvement
        
        // Vérifier la distance pour éviter la téléportation
        float distance = Vector3.Distance(transform.position, targetWorldPos);
        if (distance > 2f)
        {
            Debug.LogError($"⚠️ MoveToPositionSmoothly: Distance trop grande ({distance}), annulation");
            yield break;
        }
        
        // Direction pour l'animation
        Vector2 moveDirection = (targetWorldPos - transform.position).normalized;
        if (animatorController != null && moveDirection != Vector2.zero)
        {
            animatorController.PlayWalkAnimation(moveDirection);
        }
        
        // Animation fluide
        while (Vector3.Distance(transform.position, targetWorldPos) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        
        // Finaliser la position
        transform.position = targetWorldPos;
        
        // Mettre à jour la grille
        UpdateGridPosition(targetGridPos);
        
        Debug.Log($"✅ {gameObject.name} a atteint la position {targetGridPos}");
    }

    // Met à jour la position actuelle sur la grille
    public void UpdateGridPosition(Vector2Int newPosition)
    {
        // Désengager l'ancienne position seulement si elle est différente
        if (currentGridPosition != newPosition)
        {
            GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
            Debug.Log($"📍 {gameObject.name}: {currentGridPosition} → {newPosition}");
        }
        
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
    
    // Vérifie si l'unité est actuellement en mouvement
    public bool IsMoving()
    {
        return pathfinderFollower.IsFollowingPath() || 
               pathfinderFollower.IsChasingTarget() || 
               collisionHandler.IsMovingToCell() || 
               collisionHandler.IsBeingPushedBack();
    }
    
    // Obtient l'état actuel de l'unité pour le debug
    public string GetCurrentState()
    {
        if (collisionHandler.IsBeingPushedBack())
            return "Pushback";
        if (collisionHandler.IsMovingToCell())
            return "Moving to cell";
        if (pathfinderFollower.IsChasingTarget())
            return "Chasing target";
        if (pathfinderFollower.IsFollowingPath())
            return "Following path";
        if (HasPendingOrder())
            return "Pending order";
        
        return "Idle";
    }
    
    // Définit l'animation idle
    public void SetIdleAnimation()
    {
        if (animatorController != null)
        {
            animatorController.PlayWalkAnimation(Vector2.zero);
        }
    }
    
    // Obtient des informations de debug
    public void LogDebugInfo()
    {
        Debug.Log($"=== DEBUG {gameObject.name} ===");
        Debug.Log($"Position: {currentGridPosition}");
        Debug.Log($"État: {GetCurrentState()}");
        Debug.Log($"En mouvement: {IsMoving()}");
        Debug.Log($"Ordre en attente: {(pendingTarget.HasValue ? pendingTarget.Value.ToString() : "Aucun")}");
        Debug.Log($"Destination actuelle: {(pathfinderFollower.GetCurrentDestination().HasValue ? pathfinderFollower.GetCurrentDestination().Value.ToString() : "Aucune")}");
        Debug.Log($"Cible de poursuite: {(pathfinderFollower.GetChaseTarget() != null ? pathfinderFollower.GetChaseTarget().name : "Aucune")}");
        Debug.Log($"========================");
    }
    
    // Nettoyage lors de la destruction
    private void OnDestroy()
    {
        // Désengager la position sur la grille
        GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
        
        // Arrêter tous les mouvements
        CancelAllMovement();
        
        Debug.Log($"💀 {gameObject.name} détruit et retiré de la grille");
    }
}