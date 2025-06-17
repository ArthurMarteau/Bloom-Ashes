using System.Collections.Generic;

public class PriorityQueue<T> where T : System.IComparable<T>
{
    private List<T> items = new List<T>();

    public int Count => items.Count;

    public void Enqueue(T item)
    {
        items.Add(item);
        int ci = items.Count - 1;

        while (ci > 0)
        {
            int pi = (ci - 1) / 2;
            if (items[ci].CompareTo(items[pi]) >= 0) break;

            (items[ci], items[pi]) = (items[pi], items[ci]);
            ci = pi;
        }
    }

    public T Dequeue()
    {
        int li = items.Count - 1;
        T frontItem = items[0];
        items[0] = items[li];
        items.RemoveAt(li);

        --li;
        int pi = 0;
        while (true)
        {
            int ci = pi * 2 + 1;
            if (ci > li) break;

            int rc = ci + 1;
            if (rc <= li && items[rc].CompareTo(items[ci]) < 0)
                ci = rc;

            if (items[pi].CompareTo(items[ci]) <= 0) break;

            (items[pi], items[ci]) = (items[ci], items[pi]);
            pi = ci;
        }

        return frontItem;
    }

    public bool ContainsBetter(T item)
    {
        foreach (var i in items)
        {
            if (i.Equals(item) && i.CompareTo(item) <= 0)
                return true;
        }
        return false;
    }

    public bool IsEmpty()
    {
        return items.Count == 0;
    }

    public void Clear()
    {
        items.Clear();
    }
}

