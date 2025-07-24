using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitMover : MonoBehaviour
{
    [SerializeField] private float baseMovementSpeed = 1f; // Vitesse de base configurable
    
    private Vector2Int currentGridPosition;
    private Vector2Int previousGridPosition;
    private Vector2Int lastTraversedPosition; // Dernière case parcourue pendant le mouvement
    private bool isMoving = false;
    private bool isBeingPushedBack = false;
    private UnitAnimatorController animatorController;

    private GameObject targetToAttack = null;
    private Vector2Int? lastTargetPos = null;
    private Coroutine chaseCoroutine = null;

    private Vector2Int? desiredTargetGridPos = null;
    private Coroutine persistentMoveCoroutine = null;

    private void Start()
    {
        currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
        previousGridPosition = currentGridPosition;
        lastTraversedPosition = currentGridPosition; // Initialiser la dernière case parcourue
        animatorController = GetComponent<UnitAnimatorController>();
        GridManager.Instance.RegisterOccupiedCell(currentGridPosition, gameObject);
    }

    public void TryMoveTo(Vector2Int targetGridPos)
    {
        CancelChase();
        CancelPersistentMove();
        SnapToCurrentCell();

        desiredTargetGridPos = targetGridPos;
        persistentMoveCoroutine = StartCoroutine(PersistentMoveToTarget());
    }

    private IEnumerator PersistentMoveToTarget()
    { 
        int attempts = 0;
        int maxAttempts = 5; // Limité à 5 essais maximum
        float retryDelay = 3f; // Attente de 3 seconde entre les tentatives

        while (desiredTargetGridPos.HasValue && attempts < maxAttempts)
        {
            // Délai Rootborn à chaque tentative
            yield return HandleRootbornDelay();
    
            // Marquer comme en mouvement seulement à la première tentative
            if (attempts == 0) isMoving = true;
            
            // Vérification de sécurité
            if (Pathfinding.Instance == null)
            {
                Debug.LogError("Pathfinding.Instance is null!");
                break;
            }

            Debug.Log($"🎯 Tentative {attempts + 1}/{maxAttempts} pour atteindre {desiredTargetGridPos.Value}");

            Vector2Int bestDestination;

            // Vérifier d'abord si la destination finale est maintenant accessible directement
            if (!GridManager.Instance.IsCellUnavailable(desiredTargetGridPos.Value, gameObject))
            {
                // La case cible est libre, on peut y aller directement !
                bestDestination = desiredTargetGridPos.Value;
                Debug.Log($"🎯 Case cible {desiredTargetGridPos.Value} maintenant libre, déplacement direct !");
            }
            else
            {
                // Sinon, chercher la cellule la plus proche comme avant
                bestDestination = Pathfinding.Instance.FindClosestFreeCell(currentGridPosition, desiredTargetGridPos.Value);
            }

            // Si la destination est inaccessible (unité bloquée)
            if (bestDestination == currentGridPosition)
            {
                attempts++;
                if (attempts < maxAttempts)
                {
                    Debug.Log($"⏳ Destination inaccessible, nouvel essai dans {retryDelay} secondes... ({attempts}/{maxAttempts})");
                    yield return new WaitForSeconds(retryDelay);
                    continue;
                }
                else
                {
                    Debug.LogWarning($"🚫 Impossible d'atteindre {desiredTargetGridPos.Value} après {maxAttempts} tentatives, abandon.");
                    break;
                }
            }

            // Tentative de déplacement vers la destination
            yield return StartCoroutine(FollowPathToDynamic(bestDestination));

            // Vérifier si on a atteint la destination finale
            if (currentGridPosition == desiredTargetGridPos.Value)
            {
                Debug.Log($"✅ Destination {desiredTargetGridPos.Value} atteinte avec succès!");
                break;
            }

            // Si le déplacement a échoué, compter comme un essai
            Debug.Log($"🔍 FollowPathToDynamic terminé. Position actuelle: {currentGridPosition}, Cible: {desiredTargetGridPos.Value}");
            attempts++;
            if (attempts < maxAttempts)
            {
                Debug.Log($"🔄 Déplacement interrompu, nouvel essai dans {retryDelay} secondes... ({attempts}/{maxAttempts})");
                yield return new WaitForSeconds(retryDelay);
            }
        }

        if (attempts >= maxAttempts)
            Debug.LogWarning($"🔁 Échec du déplacement vers {desiredTargetGridPos.Value} après {maxAttempts} tentatives, unité reste en position.");

        isMoving = false;
        desiredTargetGridPos = null;
        persistentMoveCoroutine = null;
    }

private IEnumerator FollowPathToDynamic(Vector2Int finalTarget)
{
    // Vérification de sécurité avant pathfinding
    if (Pathfinding.Instance == null)
    {
        Debug.LogError("Pathfinding.Instance is null!");
        isMoving = false;
        yield break;
    }

    List<Vector2Int> path = Pathfinding.Instance.FindPath(currentGridPosition, finalTarget);
    if (path == null || path.Count <= 1)
    {
        Debug.Log("❌ Chemin bloqué ou atteint.");
        SnapToCurrentCell();
        isMoving = false;
        yield break;
    }

    for (int i = 1; i < path.Count; i++)
    {
        Vector2Int step = path[i];
        Vector3 targetPos = GridManager.Instance.GridToWorld(step.x, step.y);
        Vector3 startPos = transform.position;

        // Système de sécurité anti-boucle infinie
        int collisionRetries = 0;
        int maxCollisionRetries = 3;

        while (collisionRetries < maxCollisionRetries)
        {
            collisionRetries++;
            
            // log ajouté pr verif
            Debug.Log($"🔍 {gameObject.name} vérifie step {step} (tentative {collisionRetries})");
            
            // Vérifier si la case cible est occupée
            GameObject occupant = GridManager.Instance.GetOccupant(step);
            if (occupant != null && occupant != this.gameObject)
            {
                UnitMover otherMover = occupant.GetComponent<UnitMover>();
                if (otherMover != null && !otherMover.isBeingPushedBack && otherMover.isMoving)
                {
                    // Système de priorité basé sur la distance vers la cellule cible
                    bool shouldIPushOther = ShouldPushOtherUnit(otherMover, targetPos);
                    
                    if (shouldIPushOther)
                    {
                        Debug.Log($"🚀 {gameObject.name} pousse {otherMover.gameObject.name} (plus proche de la cible) - Tentative {collisionRetries}");
                        otherMover.ForceReturnToPreviousPosition();
                        // Attendre un court délai pour éviter les conflits simultanés
                        yield return new WaitForSeconds(0.1f);
                        // Continuer la boucle while pour revérifier
                        continue;
                    }
                    else
                    {
                        Debug.Log($"🔄 {gameObject.name} recule face à {otherMover.gameObject.name} (moins proche de la cible)");
                        yield return StartCoroutine(AnimateReturnToPrevious());
                        Debug.Log($"🔍 Distance moi: {Vector3.Distance(transform.position, targetPos)}, Distance autre: {Vector3.Distance(otherMover.transform.position, targetPos)}");
                        yield break;
                    }
                }
                else
                {
                    Debug.Log("❌ Case bloquée par un obstacle ou une unité en pushback !");
                    yield return StartCoroutine(AnimateReturnToPrevious());
                    yield break;
                }
            }
            
            // Pas de collision détectée, on sort de la boucle while
            break;
        }

        // Si on a épuisé les tentatives, abandonner ce step
        if (collisionRetries >= maxCollisionRetries)
        {
            Debug.LogError($"🚨 SECURITE: Abandon du step {step} après {maxCollisionRetries} tentatives - {gameObject.name}");
            yield return StartCoroutine(AnimateReturnToPrevious());
            yield break;
        }

        // Enregistrer la nouvelle position avant de commencer le mouvement
        GridManager.Instance.RegisterOccupiedCell(step, gameObject);

        float totalStepDistance = Vector3.Distance(startPos, targetPos);
        float moved = 0f;

        // Animation du mouvement vers la case cible
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            Vector3 direction = targetPos - transform.position;
            animatorController.PlayWalkAnimation(direction);
            float stepMove = baseMovementSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, stepMove);
            moved += stepMove;

            // Vérifier si un pont/case se ferme pendant le mouvement
            if (GridManager.Instance.IsCellBlocked(step))
            {
                float progress = moved / totalStepDistance;
                if (progress < 0.5f)
                {
                    Debug.Log("⛔ Pont fermé, retour en arrière");
                    yield return StartCoroutine(AnimateReturnToPrevious());
                    yield break;
                }
                else
                {
                    Debug.Log("💀 Chute ! Trop engagé sur le pont");
                    GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
                    Destroy(gameObject);
                    yield break;
                }
            }

            yield return null;
        }

        // Finaliser le déplacement vers la nouvelle case
        transform.position = targetPos;
        GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
        lastTraversedPosition = currentGridPosition; // Sauvegarder la dernière case parcourue
        previousGridPosition = currentGridPosition;
        currentGridPosition = step;
    }

    // Arrêter l'animation de marche une fois arrivé
    animatorController.PlayWalkAnimation(Vector2.zero);
    SnapToCurrentCell();
    isMoving = false;
}

    /// <summary>
    /// Détermine si cette unité doit pousser l'autre unité basé sur l'avancement vers la cellule cible
    /// </summary>
    private bool ShouldPushOtherUnit(UnitMover otherMover, Vector3 targetPos)
    {
        // Calculer l'avancement de chaque unité vers la cellule cible
        float myDistance = Vector3.Distance(transform.position, targetPos);
        float otherDistance = Vector3.Distance(otherMover.transform.position, targetPos);
    
        // Priorité 1 : L'unité la plus proche de la cellule cible (plus avancée)
        if (Mathf.Abs(myDistance - otherDistance) > 0.01f)
        {
            return myDistance < otherDistance; // Je suis plus proche = je pousse
        }
    
        // Priorité 2 : Fallback déterministe avec l'ID de l'objet
        return gameObject.GetInstanceID() < otherMover.gameObject.GetInstanceID();
    }

    private IEnumerator AnimateReturnToPrevious()
    {
        isBeingPushedBack = true;
        // Retour vers la dernière case parcourue pendant le mouvement, pas la position initiale
        Vector3 returnPos = GridManager.Instance.GridToWorld(lastTraversedPosition.x, lastTraversedPosition.y);
        float pushbackSpeed = baseMovementSpeed * 2f; // Vitesse de pushback = 2x la vitesse de base

        GridManager.Instance.RegisterOccupiedCell(lastTraversedPosition, gameObject);
        GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);

        Debug.Log($"🔙 Pushback vers la dernière case parcourue: {lastTraversedPosition} (vitesse: {pushbackSpeed})");

        while (Vector3.Distance(transform.position, returnPos) > 0.01f)
        {
            Vector3 direction = returnPos - transform.position;
            animatorController.PlayWalkAnimation(direction);
            transform.position = Vector3.MoveTowards(transform.position, returnPos, pushbackSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = returnPos;
        currentGridPosition = lastTraversedPosition;
        animatorController.PlayWalkAnimation(Vector2.zero);
        isBeingPushedBack = false;
    }

    public void ForceReturnToPreviousPosition()
    {
        if (!isBeingPushedBack)
            StartCoroutine(AnimateReturnToPrevious());
    }

    public void ChaseTarget(GameObject target)
    {
        CancelPersistentMove();
        if (chaseCoroutine != null)
        {
            StopCoroutine(chaseCoroutine);
            chaseCoroutine = null;
        }
        targetToAttack = target;
        chaseCoroutine = StartCoroutine(ChaseCoroutineDynamic());
    }

    private IEnumerator ChaseCoroutineDynamic()
    {
        while (targetToAttack != null)
        {
            Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(targetToAttack.transform.position);
            Vector2Int destination = Pathfinding.Instance.FindClosestFreeCell(currentGridPosition, targetGridPos);

            if (destination == currentGridPosition)
            {
                Debug.Log("🚫 Poursuite stoppée, unité déjà au plus près possible !");
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            bool inRange = false;
            for (int dx = -1; dx <= 1 && !inRange; dx++)
            {
                for (int dy = -1; dy <= 1 && !inRange; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (currentGridPosition == new Vector2Int(targetGridPos.x + dx, targetGridPos.y + dy))
                        inRange = true;
                }
            }

            if (!inRange)
            {
                lastTargetPos = destination;
                yield return StartCoroutine(FollowPathToDynamic(destination));
            }
            else
            {
                Debug.Log("💥 Cible à portée ! Prêt à attaquer.");
                yield return new WaitForSeconds(0.5f);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    public void CancelChase()
    {
        if (chaseCoroutine != null)
        {
            StopCoroutine(chaseCoroutine);
            chaseCoroutine = null;
        }
        targetToAttack = null;
        lastTargetPos = null;
        SnapToCurrentCell();
    }

    public void CancelPersistentMove()
    {
        if (persistentMoveCoroutine != null)
        {
            StopCoroutine(persistentMoveCoroutine);
            persistentMoveCoroutine = null;
        }
        desiredTargetGridPos = null;
        isMoving = false;
        SnapToCurrentCell();
    }

    private void SnapToCurrentCell()
    {
        Vector3 cellWorldPos = GridManager.Instance.GridToWorld(currentGridPosition.x, currentGridPosition.y);
        transform.position = cellWorldPos;
        animatorController.PlayWalkAnimation(Vector2.zero);
    }
    
    private IEnumerator HandleRootbornDelay()
    {
        Rootborn rootbornComponent = GetComponent<Rootborn>();
        if (rootbornComponent != null && !isBeingPushedBack)
        {
            animatorController.PlayIdleReversed();
            yield return new WaitForSeconds(1f);
            animatorController.PlayWalkAnimation(Vector2.zero);
        }
    }
}






