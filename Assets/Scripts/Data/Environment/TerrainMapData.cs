using UnityEngine;

// Données sérialisées d'une carte de terrain stockées dans un tableau unidimensionnel
[CreateAssetMenu(fileName = "TerrainMap", menuName = "Bloom and Ashes/Terrain Map")]
public class TerrainMapData : ScriptableObject
{
    [SerializeField, Min(1)] private int width = 80;
    [SerializeField, Min(1)] private int height = 80;
    [SerializeField] private GroundType[] cells;

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
    }

    // Garantit que le tableau plat correspond aux dimensions courantes
    private void EnsureStorage()
    {
        if (cells == null || cells.Length != width * height)
        {
            Resize(width, height);
        }
    }

    // Vérifie les limites d'une cellule dans la carte
    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
}
