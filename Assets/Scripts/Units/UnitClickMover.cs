using UnityEngine;

public class UnitClickMover : MonoBehaviour
{
    private UnitMover mover;

    private void Start()
    {
        mover = GetComponent<UnitMover>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) // Clic droit
        {
            if (GameManager.Instance.GetSelectedUnit()?.gameObject != this.gameObject)
                return;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (hit.collider != null)
            {
                var unit = hit.collider.GetComponent<UnitSelector>();
                if (unit != null && unit.gameObject != this.gameObject)
                {
                    mover.ChaseTarget(unit.gameObject);
                    return;
                }
            }

            Vector2Int gridTarget = GridManager.Instance.WorldToGrid(mouseWorldPos);
            mover.TryMoveTo(gridTarget);
        }
    }
}


