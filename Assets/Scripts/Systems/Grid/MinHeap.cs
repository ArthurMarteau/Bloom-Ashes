using System;
using System.Collections.Generic;

// Tas min générique pour extraction O(log n) de l'élément minimal
// Remplace List<T> + recherche linéaire O(n²) dans PathFinding pour grilles 80x80+
// Complexité : Insert O(log n), ExtractMin O(log n), Peek O(1), UpdateItem O(log n), Contains O(1)
public class MinHeap<T> where T : IComparable<T>
{
    private readonly List<T> items = new List<T>();

    // Associe chaque item à son index courant dans le tableau pour UpdateItem/Contains O(1)
    // Utilise l'identité de référence (GetHashCode par défaut) — correct pour des classes
    private readonly Dictionary<T, int> indexMap = new Dictionary<T, int>();

    // Nombre d'éléments actuellement dans le tas
    public int Count => items.Count;

    // Insère un élément à la fin du tas et remonte pour rétablir la propriété de tas
    public void Insert(T item)
    {
        items.Add(item);
        int index = items.Count - 1;
        indexMap[item] = index;
        BubbleUp(index);
    }

    // Retourne l'élément minimal (racine) sans l'extraire, en O(1)
    public T Peek()
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("MinHeap vide : impossible de lire l'élément minimal");
        }
        return items[0];
    }

    // Extrait et retourne l'élément minimal (racine), remplace par le dernier et redescend
    public T ExtractMin()
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("MinHeap vide : impossible d'extraire un élément");
        }

        T min = items[0];
        int lastIndex = items.Count - 1;

        // Déplace le dernier élément à la racine puis le redescend
        Swap(0, lastIndex);
        items.RemoveAt(lastIndex);
        indexMap.Remove(min);

        if (items.Count > 0)
        {
            BubbleDown(0);
        }

        return min;
    }

    // Notifie le tas qu'un item a diminué de valeur (ex : gCost réduit → fCost réduit)
    // À appeler après avoir modifié l'item pour rétablir la propriété en O(log n)
    // Remonte uniquement : une diminution de valeur ne peut qu'améliorer la position
    public void UpdateItem(T item)
    {
        if (indexMap.TryGetValue(item, out int index))
        {
            BubbleUp(index);
        }
    }

    // Vérifie si un item est présent dans le tas en O(1) via la map d'index
    public bool Contains(T item) => indexMap.ContainsKey(item);

    // Remonte un élément vers la racine tant qu'il est inférieur à son parent
    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parentIndex = (index - 1) / 2;

            if (items[index].CompareTo(items[parentIndex]) < 0)
            {
                Swap(index, parentIndex);
                index = parentIndex;
            }
            else
            {
                break; // Propriété de tas respectée
            }
        }
    }

    // Descend un élément vers les feuilles tant qu'il est supérieur à l'un de ses enfants
    private void BubbleDown(int index)
    {
        int count = items.Count;

        while (true)
        {
            int smallest = index;
            int leftChild  = 2 * index + 1;
            int rightChild = 2 * index + 2;

            if (leftChild < count && items[leftChild].CompareTo(items[smallest]) < 0)
            {
                smallest = leftChild;
            }

            if (rightChild < count && items[rightChild].CompareTo(items[smallest]) < 0)
            {
                smallest = rightChild;
            }

            if (smallest != index)
            {
                Swap(index, smallest);
                index = smallest;
            }
            else
            {
                break; // Propriété de tas respectée
            }
        }
    }

    // Échange deux éléments dans le tableau et maintient la map d'index synchronisée
    private void Swap(int indexA, int indexB)
    {
        indexMap[items[indexA]] = indexB;
        indexMap[items[indexB]] = indexA;
        (items[indexA], items[indexB]) = (items[indexB], items[indexA]);
    }
}
