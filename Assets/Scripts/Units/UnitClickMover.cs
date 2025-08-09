using UnityEngine;

public class UnitClickMover : MonoBehaviour
{
    // Référence vers le nouveau système de mouvement
    private MovementCore movementCore;

    // Initialise la référence vers le composant MovementCore
    private void Start()
    {
        movementCore = GetComponent<MovementCore>();
    }

    // Gère les clics de souris pour donner des ordres de déplacement
    private void Update()
    {
        if (Input.GetMouseButtonDown(1)) // Clic droit
        {
            // Vérifier que cette unité est bien sélectionnée
            if (GameManager.Instance.GetSelectedUnit()?.gameObject != this.gameObject)
                return;

            // Convertir la position de la souris en coordonnées monde
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            // Vérifier s'il y a une unité sous le curseur pour la poursuite
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (hit.collider != null)
            {
                var unit = hit.collider.GetComponent<UnitSelector>();
                if (unit != null && unit.gameObject != this.gameObject)
                {
                    // Ordonner la poursuite de cette unité
                    movementCore.ChaseTarget(unit.gameObject);
                    return;
                }
            }

            // Sinon, convertir en coordonnées grille et ordonner un déplacement
            Vector2Int gridTarget = GridManager.Instance.WorldToGrid(mouseWorldPos);
            movementCore.TryMoveTo(gridTarget);
        }
    }
}


