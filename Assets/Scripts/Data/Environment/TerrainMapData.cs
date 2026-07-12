using System;
using System.Collections.Generic;
using UnityEngine;

// Types de marqueurs disponibles pour l'authoring déterministe d'une carte.
public enum MapMarkerType
{
    NexusSpawn,
    FoundationAnchor
}

// Associe un rôle de gameplay à une cellule peinte de la carte.
[Serializable]
public struct MapMarker
{
    public MapMarkerType type;
    public Vector2Int cell;
}

// Données sérialisées d'une carte de terrain stockées dans un tableau unidimensionnel
[CreateAssetMenu(fileName = "TerrainMap", menuName = "Bloom and Ashes/Terrain Map")]
public class TerrainMapData : ScriptableObject
{
    [SerializeField, Min(1)] private int width = 80;
    [SerializeField, Min(1)] private int height = 80;
    [SerializeField] private GroundType[] cells;
    [SerializeField] private List<MapMarker> markers = new List<MapMarker>();

    public int Width => width;
    public int Height => height;

    // Initialise le stockage des nouveaux assets ou restaure une taille devenue incohérente
    private void OnEnable()
    {
        if (cells == null || cells.Length != width * height)
        {
            Resize(width, height);
        }
    }

    // Retourne le terrain d'une cellule ou Grass hors limites
    public GroundType GetCell(int x, int y)
    {
        if (!IsInBounds(x, y) || cells == null || cells.Length != width * height)
        {
            return GroundType.Grass;
        }

        return cells[x + y * width];
    }

    // Modifie une cellule valide dans le stockage sérialisé
    public void SetCell(int x, int y, GroundType groundType)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        EnsureStorage();
        cells[x + y * width] = groundType;
    }

    // Ajoute un marqueur ou remplace celui qui occupe déjà cette cellule.
    public void AddMarker(MapMarkerType type, Vector2Int cell)
    {
        if (!IsInBounds(cell.x, cell.y))
            return;

        EnsureMarkers();
        for (int index = 0; index < markers.Count; index++)
        {
            if (markers[index].cell != cell)
                continue;

            markers[index] = new MapMarker { type = type, cell = cell };
            return;
        }

        markers.Add(new MapMarker { type = type, cell = cell });
    }

    // Retire le marqueur présent sur une cellule et indique si un retrait a eu lieu.
    public bool RemoveMarkerAt(Vector2Int cell)
    {
        EnsureMarkers();
        for (int index = 0; index < markers.Count; index++)
        {
            if (markers[index].cell != cell)
                continue;

            markers.RemoveAt(index);
            return true;
        }

        return false;
    }

    // Retourne les marqueurs d'un type dans leur ordre stable de pose.
    public List<MapMarker> GetMarkers(MapMarkerType type)
    {
        EnsureMarkers();
        List<MapMarker> result = new List<MapMarker>();
        foreach (MapMarker marker in markers)
        {
            if (marker.type == type)
                result.Add(marker);
        }

        return result;
    }

    // Retire tous les marqueurs du type demandé sans modifier l'ordre des autres.
    public void ClearMarkers(MapMarkerType type)
    {
        EnsureMarkers();
        markers.RemoveAll(marker => marker.type == type);
    }

    // Redimensionne la carte en préservant l'intersection avec les anciennes dimensions
    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);

        int oldWidth = width;
        int oldHeight = height;
        GroundType[] oldCells = cells;
        GroundType[] resizedCells = new GroundType[newWidth * newHeight];

        for (int index = 0; index < resizedCells.Length; index++)
        {
            resizedCells[index] = GroundType.Grass;
        }

        if (oldCells != null && oldWidth > 0 && oldHeight > 0)
        {
            int copyWidth = Mathf.Min(oldWidth, newWidth);
            int copyHeight = Mathf.Min(oldHeight, newHeight);

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    int oldIndex = x + y * oldWidth;
                    if (oldIndex < oldCells.Length)
                    {
                        resizedCells[x + y * newWidth] = oldCells[oldIndex];
                    }
                }
            }
        }

        width = newWidth;
        height = newHeight;
        cells = resizedCells;
        EnsureMarkers();
        markers.RemoveAll(marker => !IsInBounds(marker.cell.x, marker.cell.y));
    }

    // Garantit que le tableau plat correspond aux dimensions courantes
    private void EnsureStorage()
    {
        if (cells == null || cells.Length != width * height)
        {
            Resize(width, height);
        }
    }

    // Garantit que les anciens assets disposent d'une liste de marqueurs valide.
    private void EnsureMarkers()
    {
        if (markers == null)
            markers = new List<MapMarker>();
    }

    // Vérifie les limites d'une cellule dans la carte
    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
}
