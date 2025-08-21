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

            // Convertir la position de la souris en coordonnées monde puis grille
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridTarget = GridManager.Instance.WorldToGrid(mouseWorldPos);

            // DEBUG : Vérifier la cohérence du clic (tu peux garder ou supprimer)
            Vector3 cellCenterWorld = GridManager.Instance.GridToWorld(gridTarget.x, gridTarget.y);
            float distanceFromCenter = Vector3.Distance(new Vector3(mouseWorldPos.x, mouseWorldPos.y, 0), cellCenterWorld);
            Debug.Log($"🖱️ Clic: {mouseWorldPos} → Grille: {gridTarget}");
            Debug.Log($"📏 Distance du centre: {distanceFromCenter}");

            // Vérifier qui occupe cette case
            GameObject occupant = GridManager.Instance.GetOccupant(gridTarget);
            if (occupant != null && occupant != this.gameObject)
            {
                var unit = occupant.GetComponent<UnitSelector>();
                if (unit != null)
                {
                    Debug.Log("🎯 CHASE déclenché via grille !");
                    movementCore.ChaseTarget(unit.gameObject);
                    return;
                }
            }

            Debug.Log("📍 Mouvement normal déclenché");
            movementCore.TryMoveTo(gridTarget);
        }
    }
}


