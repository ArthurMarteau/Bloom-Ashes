using UnityEngine;

// Classe Node pour l'algorithme A*
[System.Serializable]
public class Node
{
    public Vector2Int position;
    public int gCost; // Coût depuis le début
    public int hCost; // Heuristique vers la fin
    public int fCost => gCost + hCost; // Coût total
    public Node parent;
    
    public Node(Vector2Int pos, int g, int h, Node parentNode)
    {
        position = pos;
        gCost = g;
        hCost = h;
        parent = parentNode;
    }
}