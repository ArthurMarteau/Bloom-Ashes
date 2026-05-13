using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Tests EditMode de classification des objets sélectionnables
public class SelectionResolverTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    // Nettoie les GameObjects créés par chaque test
    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
    }

    // Vérifie qu'un GameObject avec UnitStatsComponent seul est une unité
    [Test]
    public void Classify_UnitStatsOnly_ReturnsUnits()
    {
        GameObject unit = CreateGameObject("unit");
        unit.AddComponent<UnitStatsComponent>();

        SelectionType type = SelectionResolver.Classify(unit, out GameObject entity);

        Assert.AreEqual(SelectionType.Units, type);
        Assert.AreEqual(unit, entity);
    }

    // Vérifie qu'un Bud avec UnitStatsComponent et BuildingStatsComponent est un bâtiment
    [Test]
    public void Classify_UnitAndBuildingStats_ReturnsBuilding()
    {
        GameObject bud = CreateGameObject("bud");
        bud.AddComponent<UnitStatsComponent>();
        bud.AddComponent<BuildingStatsComponent>();

        SelectionType type = SelectionResolver.Classify(bud, out GameObject entity);

        Assert.AreEqual(SelectionType.Building, type);
        Assert.AreEqual(bud, entity);
    }

    // Vérifie qu'un GameObject avec BuildingStatsComponent seul est un bâtiment
    [Test]
    public void Classify_BuildingStatsOnly_ReturnsBuilding()
    {
        GameObject building = CreateGameObject("building");
        building.AddComponent<BuildingStatsComponent>();

        SelectionType type = SelectionResolver.Classify(building, out GameObject entity);

        Assert.AreEqual(SelectionType.Building, type);
        Assert.AreEqual(building, entity);
    }

    // Vérifie qu'un GameObject sans composant spécifique est un obstacle
    [Test]
    public void Classify_NoSpecificComponent_ReturnsObstacle()
    {
        GameObject obstacle = CreateGameObject("obstacle");

        SelectionType type = SelectionResolver.Classify(obstacle, out GameObject entity);

        Assert.AreEqual(SelectionType.Obstacle, type);
        Assert.AreEqual(obstacle, entity);
    }

    // Vérifie qu'un hit null retourne None
    [Test]
    public void Classify_Null_ReturnsNone()
    {
        SelectionType type = SelectionResolver.Classify(null, out GameObject entity);

        Assert.AreEqual(SelectionType.None, type);
        Assert.IsNull(entity);
    }

    // Vérifie que ResolveBestHit priorise Unit devant Building et Obstacle
    [Test]
    public void ResolveBestHit_MixUnitBuildingObstacle_ReturnsUnit()
    {
        GameObject unit = CreateGameObjectWithCollider("unit");
        GameObject building = CreateGameObjectWithCollider("building");
        GameObject obstacle = CreateGameObjectWithCollider("obstacle");
        unit.AddComponent<UnitStatsComponent>();
        building.AddComponent<BuildingStatsComponent>();

        Collider2D[] hits = new[]
        {
            obstacle.GetComponent<Collider2D>(),
            building.GetComponent<Collider2D>(),
            unit.GetComponent<Collider2D>()
        };

        GameObject bestHit = SelectionResolver.ResolveBestHit(hits);

        Assert.AreEqual(unit, bestHit);
    }

    // Vérifie que ResolveBestHit priorise Building devant Obstacle
    [Test]
    public void ResolveBestHit_BuildingAndObstacle_ReturnsBuilding()
    {
        GameObject building = CreateGameObjectWithCollider("building");
        GameObject obstacle = CreateGameObjectWithCollider("obstacle");
        building.AddComponent<BuildingStatsComponent>();

        Collider2D[] hits = new[]
        {
            obstacle.GetComponent<Collider2D>(),
            building.GetComponent<Collider2D>()
        };

        GameObject bestHit = SelectionResolver.ResolveBestHit(hits);

        Assert.AreEqual(building, bestHit);
    }

    // Vérifie que IsSelectableUnit exclut Bud et bâtiments purs
    [Test]
    public void IsSelectableUnit_FiltersBudAndPureBuilding()
    {
        GameObject unit = CreateGameObject("unit");
        GameObject bud = CreateGameObject("bud");
        GameObject building = CreateGameObject("building");
        unit.AddComponent<UnitStatsComponent>();
        bud.AddComponent<UnitStatsComponent>();
        bud.AddComponent<BuildingStatsComponent>();
        building.AddComponent<BuildingStatsComponent>();

        Assert.IsTrue(SelectionResolver.IsSelectableUnit(unit));
        Assert.IsFalse(SelectionResolver.IsSelectableUnit(bud));
        Assert.IsFalse(SelectionResolver.IsSelectableUnit(building));
    }

    // Crée un GameObject de test et le tracke pour cleanup
    private GameObject CreateGameObject(string objectName)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObjects.Add(createdObject);
        return createdObject;
    }

    // Crée un GameObject de test avec collider et le tracke pour cleanup
    private GameObject CreateGameObjectWithCollider(string objectName)
    {
        GameObject createdObject = CreateGameObject(objectName);
        createdObject.AddComponent<BoxCollider2D>();
        return createdObject;
    }

}
