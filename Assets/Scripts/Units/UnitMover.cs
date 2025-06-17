using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitMover : MonoBehaviour
{
    private Vector2Int currentGridPosition;
    private bool isMoving = false;
    private UnitAnimatorController animatorController;

    private GameObject targetToAttack = null;
    private Vector2Int? lastTargetPos = null;
    private Coroutine chaseCoroutine = null;

    // Ajout persistent move
    private Vector2Int? desiredTargetGridPos = null; // La case demandée par le joueur
    private Coroutine persistentMoveCoroutine = null; // Pour pouvoir annuler le déplacement courant

    // Pour la gestion des croisements
    private int moveFrame = 0;

    private void Start()
    {
        currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
        animatorController = GetComponent<UnitAnimatorController>();
        GridManager.Instance.RegisterOccupiedCell(currentGridPosition, gameObject);
    }

    // === ORDRE DE DEPLACEMENT PERSISTANT (clic souris) ===
    public void TryMoveTo(Vector2Int targetGridPos)
    {
        CancelChase();
        CancelPersistentMove();
        SnapToCurrentCell();

        // Option : commente cette ligne pour pouvoir redonner un ordre même en mouvement
        // if (isMoving) return;

        desiredTargetGridPos = targetGridPos;
        SnapToCurrentCell();
        persistentMoveCoroutine = StartCoroutine(PersistentMoveToTarget());
    }

    private IEnumerator PersistentMoveToTarget()
    {
        isMoving = true;

        while (desiredTargetGridPos.HasValue)
        {
            // BFS pour aller au plus près de la case demandée
            Vector2Int bestDestination = FindClosestReachableCellBFS(currentGridPosition, desiredTargetGridPos.Value);

            if (bestDestination == currentGridPosition)
            {
                // On est bloqué au plus près, donc on attend un peu et on reteste tant qu’on a le même ordre
                yield return new WaitForSeconds(0.2f);
                continue;
            }

            // Bouge jusqu’à la bestDestination (logique dynamique)
            yield return StartCoroutine(FollowPathToDynamic(bestDestination));

            // On vérifie si on est arrivé à la vraie cible
            if (currentGridPosition == desiredTargetGridPos.Value)
            {
                break; // Arrêt du déplacement : objectif atteint !
            }
            // Sinon, boucle : peut-être que la case demandée est enfin libre
        }

        isMoving = false;
        desiredTargetGridPos = null;
        persistentMoveCoroutine = null;
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

    // === DEPLACEMENT CASE PAR CASE ===
    private IEnumerator FollowPathToDynamic(Vector2Int finalTarget)
    {
        if (GetComponent<Rootborn>() != null)
        {
            animatorController.PlayIdleReversed();
            yield return new WaitForSeconds(1f);
        }

        isMoving = true;
        moveFrame = 0; // reset à chaque déplacement

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

            // Gestion du croisement/collision ICI, par ex :
            GameObject occupant = GridManager.Instance.GetOccupant(step);
            if (occupant != null)
            {
                UnitMover otherMover = occupant.GetComponent<UnitMover>();
                if (otherMover != null)
                {
                    if (otherMover.moveFrame <= this.moveFrame)
                    {
                        yield return null;
                        i--; // ATTENTION : recommence ce step
                        continue;
                    }
                    else
                    {
                        otherMover.ForceReturnToPreviousPosition();
                    }
                }
                else
                {
                    Debug.Log("❌ Case bloquée par un obstacle !");
                    SnapToCurrentCell();
                    isMoving = false;
                    yield break;
                }
            }

            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                Vector3 direction = targetPos - transform.position;
                animatorController.PlayWalkAnimation(direction);
                transform.position = Vector3.MoveTowards(transform.position, targetPos, 1f * Time.deltaTime);
                yield return null;
            }

            // Quand l’unité est arrivée sur la case cible, SNAPPER et MAJ :
            transform.position = targetPos;
            GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
            GridManager.Instance.RegisterOccupiedCell(step, gameObject);
            currentGridPosition = step;
            moveFrame++;
        }

        animatorController.PlayWalkAnimation(Vector2.zero); // Idle
        SnapToCurrentCell();
        isMoving = false;
    }


    // Gestion du croisement : à affiner selon ta logique (optionnel)
    public void ForceReturnToPreviousPosition()
    {
        // A faire : gérer le retour arrière en cas de conflit, à ta façon
    }

    // === AGGRO/CHASE LOGIC (identique mais dynamique) ===
    public void ChaseTarget(GameObject target)
    {
        CancelPersistentMove(); // Toujours annuler le move classique si aggro !
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
            if (targetToAttack == null)
                yield break;

            Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(targetToAttack.transform.position);

            Vector2Int destination = FindClosestReachableCellBFS(currentGridPosition, targetGridPos);

            if (destination == currentGridPosition)
            {
                Debug.Log("🚫 Poursuite stoppée, unité déjà au plus près possible !");
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            Vector2Int myPos = currentGridPosition;
            bool inRange = false;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (myPos == new Vector2Int(targetGridPos.x + dx, targetGridPos.y + dy))
                    {
                        inRange = true;
                        break;
                    }
                }
                if (inRange) break;
            }

            if (!inRange)
            {
                lastTargetPos = destination;
                yield return StartCoroutine(FollowPathToDynamic(destination));
            }
            else
            {
                Debug.Log("💥 Cible à portée ! Prêt à attaquer.");
                // TODO : Animation/dégâts ici
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

    // --- BFS pour "plus proche possible" ---
    public Vector2Int FindClosestReachableCellBFS(Vector2Int start, Vector2Int target, int radiusMax = 10)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int bestCell = start;
        float bestDist = (start - target).sqrMagnitude;

        int[] dirX = { -1, 0, 1, 0, -1, -1, 1, 1 };
        int[] dirY = { 0, 1, 0, -1, 1, -1, 1, -1 };

        int iter = 0;
        int maxIter = 500;

        while (queue.Count > 0 && iter < maxIter)
        {
            Vector2Int current = queue.Dequeue();
            float dist = (current - target).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = current;
                if (bestDist <= 1f)
                    return bestCell;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2Int neighbor = new Vector2Int(current.x + dirX[i], current.y + dirY[i]);
                if (visited.Contains(neighbor)) continue;
                if (!GridManager.Instance.IsWithinBounds(neighbor)) continue;
                if (Mathf.Abs(neighbor.x - target.x) > radiusMax || Mathf.Abs(neighbor.y - target.y) > radiusMax)
                    continue;
                if (GridManager.Instance.IsCellUnavailable(neighbor, null)) continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }

            iter++;
        }
        return bestCell;
    }

    private void SnapToCurrentCell()
    {
        Vector3 cellWorldPos = GridManager.Instance.GridToWorld(currentGridPosition.x, currentGridPosition.y);
        transform.position = cellWorldPos;
        animatorController.PlayWalkAnimation(Vector2.zero); // Idle
    }

}




