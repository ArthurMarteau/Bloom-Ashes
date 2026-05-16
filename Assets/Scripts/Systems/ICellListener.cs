using UnityEngine;

// Interface pour recevoir les entrées/sorties d'unités sur des cellules logiques.
public interface ICellListener
{
    void OnUnitEntered(Vector2Int cell, GameObject unit);
    void OnUnitLeft(Vector2Int cell, GameObject unit);
}
