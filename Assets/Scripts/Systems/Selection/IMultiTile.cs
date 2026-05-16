using System.Collections.Generic;
using UnityEngine;

// Interface pour les GameObjects occupant plusieurs cellules logiques.
public interface IMultiTile
{
    List<Vector2Int> GetOccupiedTiles();
}
