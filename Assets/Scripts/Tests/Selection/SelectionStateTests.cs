using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Tests EditMode des invariants de SelectionState
public class SelectionStateTests
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

    // Vérifie que SetUnits efface une sélection unique
    [Test]
    public void SetUnits_ClearSingle()
    {
        SelectionState state = new SelectionState();
        GameObject building = CreateGameObject("building");
        GameObject unit = CreateGameObject("unit");

        state.SetSingle(SelectionType.Building, building);
        state.SetUnits(new[] { unit });

        Assert.AreEqual(SelectionType.Units, state.Type);
        Assert.AreEqual(1, state.Units.Count);
        Assert.IsNull(state.Single);
    }

    // Vérifie que AddUnit efface une sélection unique existante
    [Test]
    public void AddUnit_ClearSingleIfPresent()
    {
        SelectionState state = new SelectionState();
        GameObject obstacle = CreateGameObject("obstacle");
        GameObject unit = CreateGameObject("unit");

        state.SetSingle(SelectionType.Obstacle, obstacle);
        bool added = state.AddUnit(unit, 10);

        Assert.IsTrue(added);
        Assert.AreEqual(SelectionType.Units, state.Type);
        Assert.AreEqual(1, state.Units.Count);
        Assert.IsNull(state.Single);
    }

    // Vérifie que AddUnit retourne false quand le cap est atteint
    [Test]
    public void AddUnit_ReturnsFalseAtCap()
    {
        SelectionState state = new SelectionState();
        GameObject firstUnit = CreateGameObject("first");
        GameObject secondUnit = CreateGameObject("second");

        Assert.IsTrue(state.AddUnit(firstUnit, 1));
        Assert.IsFalse(state.AddUnit(secondUnit, 1));
        Assert.AreEqual(1, state.Units.Count);
        Assert.IsFalse(state.Contains(secondUnit));
    }

    // Vérifie que SetSingle efface les unités
    [Test]
    public void SetSingle_ClearUnits()
    {
        SelectionState state = new SelectionState();
        GameObject unit = CreateGameObject("unit");
        GameObject building = CreateGameObject("building");

        state.SetUnits(new[] { unit });
        state.SetSingle(SelectionType.Building, building);

        Assert.AreEqual(SelectionType.Building, state.Type);
        Assert.AreEqual(building, state.Single);
        Assert.AreEqual(0, state.Units.Count);
    }

    // Vérifie que SetGround efface unités et single
    [Test]
    public void SetGround_ClearAllEntities()
    {
        SelectionState state = new SelectionState();
        GameObject unit = CreateGameObject("unit");

        state.SetUnits(new[] { unit });
        state.SetGround(new Vector2Int(2, 3));

        Assert.AreEqual(SelectionType.Ground, state.Type);
        Assert.AreEqual(new Vector2Int(2, 3), state.Ground);
        Assert.AreEqual(0, state.Units.Count);
        Assert.IsNull(state.Single);
    }

    // Vérifie que retirer la dernière unité repasse l'état à None
    [Test]
    public void RemoveUnit_LastUnit_SetTypeNone()
    {
        SelectionState state = new SelectionState();
        GameObject unit = CreateGameObject("unit");

        state.SetUnits(new[] { unit });
        bool removed = state.RemoveUnit(unit);

        Assert.IsTrue(removed);
        Assert.AreEqual(SelectionType.None, state.Type);
        Assert.IsTrue(state.IsEmpty);
    }

    // Vérifie qu'aucune transition publique ne garde units et single simultanément
    [Test]
    public void Invariant_PreventsUnitsAndSingleTogether()
    {
        SelectionState state = new SelectionState();
        GameObject unit = CreateGameObject("unit");
        GameObject building = CreateGameObject("building");

        state.SetSingle(SelectionType.Building, building);
        state.AddUnit(unit, 10);

        Assert.AreEqual(SelectionType.Units, state.Type);
        Assert.AreEqual(1, state.Units.Count);
        Assert.IsNull(state.Single);

        state.SetSingle(SelectionType.Building, building);

        Assert.AreEqual(SelectionType.Building, state.Type);
        Assert.AreEqual(0, state.Units.Count);
        Assert.AreEqual(building, state.Single);
    }

    // Crée un GameObject de test et le tracke pour cleanup
    private GameObject CreateGameObject(string objectName)
    {
        GameObject createdObject = new GameObject(objectName);
        createdObjects.Add(createdObject);
        return createdObject;
    }
}
