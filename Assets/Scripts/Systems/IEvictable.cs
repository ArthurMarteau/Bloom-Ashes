using UnityEngine;

// Contrat appelé lorsqu'une entité mobile perd une cellule logique contestée
public interface IEvictable
{
    void OnEvicted(Vector2Int lostCell);
}
