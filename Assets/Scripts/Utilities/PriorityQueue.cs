using System.Collections.Generic;

public class PriorityQueue<T> where T : System.IComparable<T>
{
    private List<T> items = new List<T>();
    // Dictionary pour traquer les éléments présents et leur meilleur coût
    private Dictionary<T, T> itemTracker = new Dictionary<T, T>();

    public int Count => items.Count;

    // Ajoute un élément à la file de priorité en maintenant la propriété du tas binaire minimum
    public void Enqueue(T item)
    {
        // Ajouter le nouvel élément à la fin de la liste
        items.Add(item);
        
        // Enregistrer cet élément dans le tracker
        itemTracker[item] = item;
        
        // Index de l'élément qu'on vient d'ajouter (child index)
        int ci = items.Count - 1;

        // Remonter l'élément vers le haut du tas jusqu'à sa position correcte (bubble up)
        while (ci > 0)
        {
            // Calculer l'index du parent : (enfant - 1) / 2
            int pi = (ci - 1) / 2;
            
            // Si l'enfant est >= parent, le tas est correct, on s'arrête
            if (items[ci].CompareTo(items[pi]) >= 0) break;

            // Échanger enfant et parent (tuple swap en C#)
            (items[ci], items[pi]) = (items[pi], items[ci]);
            
            // Continuer avec le parent comme nouvel enfant
            ci = pi;
        }
    }

    // Retire et retourne l'élément de plus haute priorité (le plus petit F) de la file
    public T Dequeue()
    {
        // Vérification de sécurité : file vide
        if (items.Count == 0)
            throw new System.InvalidOperationException("Cannot dequeue from empty priority queue");
            
        // Index du dernier élément
        int li = items.Count - 1;
        
        // Sauvegarder l'élément de tête (plus petite valeur F) à retourner
        T frontItem = items[0];
        
        // Retirer cet élément du tracker
        itemTracker.Remove(frontItem);
        
        // Déplacer le dernier élément à la racine
        items[0] = items[li];
        
        // Supprimer le dernier élément
        items.RemoveAt(li);

        // Si la file n'est pas vide après suppression, réorganiser le tas
        if (items.Count > 0)
        {
            // Mettre à jour l'index du dernier élément
            --li;
            
            // Index du parent actuel
            int pi = 0;
            
            // Redescendre l'élément vers le bas du tas jusqu'à sa position correcte (bubble down)
            while (true)
            {
                // Calculer l'index de l'enfant gauche : parent * 2 + 1
                int ci = pi * 2 + 1;
                
                // Si pas d'enfant gauche, on a fini
                if (ci > li) break;

                // Index de l'enfant droit
                int rc = ci + 1;
                
                // Si l'enfant droit existe ET qu'il est plus petit que le gauche, utiliser le droit
                if (rc <= li && items[rc].CompareTo(items[ci]) < 0)
                    ci = rc;

                // Si le parent est <= au plus petit enfant, le tas est correct
                if (items[pi].CompareTo(items[ci]) <= 0) break;

                // Échanger parent et enfant
                (items[pi], items[ci]) = (items[ci], items[pi]);
                
                // Continuer avec l'enfant comme nouveau parent
                pi = ci;
            }
        }

        return frontItem;
    }

    // Vérifie s'il existe déjà un élément équivalent avec un coût égal ou meilleur dans la file (version optimisée)
    public bool ContainsBetter(T item)
    {
        // Recherche O(1) dans le Dictionary au lieu de O(n) dans la List
        if (itemTracker.TryGetValue(item, out T existingItem))
        {
            // Si l'élément existe ET qu'il a un coût égal ou meilleur
            return existingItem.CompareTo(item) <= 0;
        }
        
        // L'élément n'existe pas dans la file
        return false;
    }

    // Indique si la file est vide
    public bool IsEmpty()
    {
        return items.Count == 0;
    }

    // Vide complètement la file
    public void Clear()
    {
        items.Clear();
        itemTracker.Clear(); // Ne pas oublier de vider le tracker aussi
    }
}

