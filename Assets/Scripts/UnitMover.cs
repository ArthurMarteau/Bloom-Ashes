using UnityEngine;

public class UnitMover : MonoBehaviour
{
    private Vector2Int currentGridPosition;
    private bool isMoving = false;

    private void Start()
    {
        currentGridPosition = GridManager.Instance.WorldToGrid(transform.position);
    }

    public void TryMoveTo(Vector2Int targetGridPos)
    {
        if (isMoving) return;

        Vector2Int delta = targetGridPos - currentGridPosition;

        // Ne permet que les déplacements sur les cases adjacentes (8 directions)
        if (Mathf.Abs(delta.x) > 1 || Mathf.Abs(delta.y) > 1)
        {
            Debug.Log("Cible hors de portée.");
            return;
        }

        // TODO : Vérifier si la case est occupée

        Vector3 targetWorldPos = GridManager.Instance.GridToWorld(targetGridPos.x, targetGridPos.y);
        StartCoroutine(MoveToPosition(targetWorldPos));
        currentGridPosition = targetGridPos;
    }

    private System.Collections.IEnumerator MoveToPosition(Vector3 targetPos)
    {
        isMoving = true;
        float speed = 4f;
        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPos;
        isMoving = false;
    }
}
