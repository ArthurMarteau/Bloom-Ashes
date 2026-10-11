using NUnit.Framework;

// Tests EditMode des primitives de grille pures utilisées par PathFinding.
// PathFinding lui-même dépend encore de GridManager et de GameObject et nécessite un test d'intégration Unity.
public class PathFindingTests
{
    // Vérifie les limites entières sans scène ni MonoBehaviour.
    [TestCase(0, 0, true)]
    [TestCase(7, 5, true)]
    [TestCase(-1, 0, false)]
    [TestCase(8, 0, false)]
    [TestCase(0, 6, false)]
    public void GridSystem_IsInBounds_UsesExclusiveUpperBounds(int x, int y, bool expected)
    {
        Assert.AreEqual(expected, GridSystem.IsInBounds(x, y, 8, 6));
    }

    // Vérifie que les conversions grille-monde sont réversibles pour des valeurs fixes.
    [TestCase(0, 0)]
    [TestCase(3, 4)]
    [TestCase(79, 79)]
    public void GridSystem_GridToWorldThenWorldToGrid_RoundTrips(int x, int y)
    {
        const float cellSize = 1.25f;
        const float originX = -10f;
        const float originY = 3f;
        var world = GridSystem.GridToWorld(x, y, cellSize, originX, originY);

        var cell = GridSystem.WorldToGrid(world.x, world.y, cellSize, originX, originY);

        Assert.AreEqual(x, cell.x);
        Assert.AreEqual(y, cell.y);
    }
}
