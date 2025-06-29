using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitMover : MonoBehaviour
{
    private Vector2Int currentGridPosition;
    private Vector2Int previousGridPosition;
    private bool isMoving = false;
    private bool isBeingPushedBack = false;
    private UnitAnimatorController animatorController;

    private GameObject targetToAttack = null;
    private Vector2Int? lastTargetPos = null;
    private Coroutine chaseCoroutine = null;

    private Vector2Int? desiredTargetGridPos = null;
    private Coroutine persistentMoveCoroutine = null;

    private int moveFrame = 0;

    private void Start()
    {
        currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
        previousGridPosition = currentGridPosition;
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
        isMoving = true;
        int retries = 0;
        int maxRetries = 20;

        while (desiredTargetGridPos.HasValue && retries < maxRetries)
        {
            Vector2Int bestDestination = Pathfinding.Instance.FindClosestFreeCell(currentGridPosition, desiredTargetGridPos.Value);

            if (bestDestination == currentGridPosition)
            {
                yield return new WaitForSeconds(0.2f);
                retries++;
                continue;
            }

            yield return StartCoroutine(FollowPathToDynamic(bestDestination));

            if (currentGridPosition == desiredTargetGridPos.Value)
                break;

            retries++;
        }

        if (retries >= maxRetries)
            Debug.LogWarning($"🔁 Trop de tentatives de déplacement vers {desiredTargetGridPos.Value}, annulation.");

        isMoving = false;
        desiredTargetGridPos = null;
        persistentMoveCoroutine = null;
    }

    private IEnumerator FollowPathToDynamic(Vector2Int finalTarget)
    {
        if (GetComponent<Rootborn>() != null)
        {
            animatorController.PlayIdleReversed();
            yield return new WaitForSeconds(1f);
        }

        isMoving = true;
        moveFrame = 0;

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

            GameObject occupant = GridManager.Instance.GetOccupant(step);
            if (occupant != null && occupant != this.gameObject)
            {
                UnitMover otherMover = occupant.GetComponent<UnitMover>();
                if (otherMover != null && !otherMover.isBeingPushedBack)
                {
                    float myDist = Vector3.Distance(transform.position, targetPos);
                    float otherDist = Vector3.Distance(otherMover.transform.position, targetPos);

                    if (myDist < otherDist)
                    {
                        otherMover.ForceReturnToPreviousPosition();
                    }
                    else
                    {
                        yield return StartCoroutine(AnimateReturnToPrevious());
                        yield break;
                    }
                }
                else
                {
                    Debug.Log("❌ Case bloquée par un obstacle !");
                    yield return StartCoroutine(AnimateReturnToPrevious());
                    yield break;
                }
            }

            GridManager.Instance.RegisterOccupiedCell(step, gameObject);

            float totalStepDistance = Vector3.Distance(startPos, targetPos);
            float moved = 0f;

            while (Vector3.Distance(transform.position, targetPos) > 0.01f)
            {
                Vector3 direction = targetPos - transform.position;
                animatorController.PlayWalkAnimation(direction);
                float stepMove = 1f * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, targetPos, stepMove);
                moved += stepMove;

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
                        Debug.Log("💀 Chute ! Trop engagé");
                        GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
                        Destroy(gameObject);
                        yield break;
                    }
                }

                yield return null;
            }

            transform.position = targetPos;
            GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);
            previousGridPosition = currentGridPosition;
            currentGridPosition = step;
            moveFrame++;
        }

        animatorController.PlayWalkAnimation(Vector2.zero);
        SnapToCurrentCell();
        isMoving = false;
    }

    private IEnumerator AnimateReturnToPrevious()
    {
        isBeingPushedBack = true;
        Vector3 returnPos = GridManager.Instance.GridToWorld(previousGridPosition.x, previousGridPosition.y);
        float speed = 3f;

        GridManager.Instance.RegisterOccupiedCell(previousGridPosition, gameObject);
        GridManager.Instance.UnregisterOccupiedCell(currentGridPosition);

        while (Vector3.Distance(transform.position, returnPos) > 0.01f)
        {
            Vector3 direction = returnPos - transform.position;
            animatorController.PlayWalkAnimation(direction);
            transform.position = Vector3.MoveTowards(transform.position, returnPos, speed * Time.deltaTime);
            yield return null;
        }

        transform.position = returnPos;
        currentGridPosition = previousGridPosition;
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
}






