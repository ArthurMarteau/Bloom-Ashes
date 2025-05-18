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
            if (!GameManager.Instance.GetSelectedUnit()?.gameObject == this.gameObject)
                return;

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridTarget = GridManager.Instance.WorldToGrid(mouseWorldPos);

            mover.TryMoveTo(gridTarget);
        }
    }
}
