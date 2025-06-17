using UnityEngine;

public class PathNode : System.IComparable<PathNode>
{
    public Vector2Int Position;
    public int G; // coût depuis le départ
    public int F; // G + H

    public PathNode(Vector2Int pos, int g, int h)
    {
        Position = pos;
        G = g;
        F = g + h;
    }

    public int CompareTo(PathNode other)
    {
        return F.CompareTo(other.F);
    }

    public override bool Equals(object obj)
    {
        return obj is PathNode other && Position == other.Position;
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode();
    }
}

