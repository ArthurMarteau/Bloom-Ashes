using System.Collections.Generic;
using UnityEngine;

// Etat pur de sélection : unités multiples ou sélection unique exclusive
public class SelectionState
{
    private readonly List<GameObject> selectedUnits = new List<GameObject>();
    private GameObject singleEntity;
    private Vector2Int groundPosition;
    private SelectionType currentType = SelectionType.None;

    // Retourne le type courant de sélection
    public SelectionType Type => currentType;

    // Retourne les unités sélectionnées en lecture seule
    public IReadOnlyList<GameObject> Units => selectedUnits;

    // Retourne l'entité unique sélectionnée
    public GameObject Single => singleEntity;

    // Retourne la position de terrain sélectionnée
    public Vector2Int Ground => groundPosition;

    // Indique si l'état ne contient aucune sélection
    public bool IsEmpty => currentType == SelectionType.None;

    // Retourne le nombre d'éléments sélectionnés
    public int Count => currentType == SelectionType.Units ? selectedUnits.Count : (currentType == SelectionType.None ? 0 : 1);

    // Remplace la sélection courante par une liste d'unités
    public void SetUnits(IEnumerable<GameObject> units)
    {
        selectedUnits.Clear();
        singleEntity = null;
        groundPosition = Vector2Int.zero;

        if (units != null)
        {
            foreach (GameObject unit in units)
            {
                if (unit != null && !selectedUnits.Contains(unit))
                {
                    selectedUnits.Add(unit);
                }
            }
        }

        currentType = selectedUnits.Count > 0 ? SelectionType.Units : SelectionType.None;
        VerifyInvariants();
    }

    // Ajoute une unité à la sélection multiple
    public bool AddUnit(GameObject unit, int maxUnits)
    {
        if (unit == null)
        {
            return false;
        }

        if (currentType != SelectionType.Units)
        {
            selectedUnits.Clear();
            singleEntity = null;
            groundPosition = Vector2Int.zero;
            currentType = SelectionType.Units;
        }

        if (selectedUnits.Contains(unit))
        {
            VerifyInvariants();
            return true;
        }

        if (selectedUnits.Count >= maxUnits)
        {
            VerifyInvariants();
            return false;
        }

        selectedUnits.Add(unit);
        VerifyInvariants();
        return true;
    }

    // Retire une unité de la sélection multiple
    public bool RemoveUnit(GameObject unit)
    {
        bool removed = selectedUnits.Remove(unit);
        if (selectedUnits.Count == 0 && currentType == SelectionType.Units)
        {
            currentType = SelectionType.None;
        }

        VerifyInvariants();
        return removed;
    }

    // Remplace la sélection courante par une entité unique
    public void SetSingle(SelectionType type, GameObject entity)
    {
        if (type != SelectionType.Building && type != SelectionType.Environment && type != SelectionType.Obstacle)
        {
            throw new System.ArgumentException("SetSingle accepte uniquement Building, Environment ou Obstacle.", nameof(type));
        }

        selectedUnits.Clear();
        singleEntity = entity;
        groundPosition = Vector2Int.zero;
        currentType = entity != null ? type : SelectionType.None;
        VerifyInvariants();
    }

    // Remplace la sélection courante par une cellule de terrain
    public void SetGround(Vector2Int position)
    {
        selectedUnits.Clear();
        singleEntity = null;
        groundPosition = position;
        currentType = SelectionType.Ground;
        VerifyInvariants();
    }

    // Efface toute sélection
    public void Clear()
    {
        selectedUnits.Clear();
        singleEntity = null;
        groundPosition = Vector2Int.zero;
        currentType = SelectionType.None;
        VerifyInvariants();
    }

    // Indique si l'objet est présent dans la sélection courante
    public bool Contains(GameObject entity)
    {
        if (entity == null)
        {
            return false;
        }

        return selectedUnits.Contains(entity) || singleEntity == entity;
    }

    // Vérifie les invariants exclusifs de sélection
    private void VerifyInvariants()
    {
        if (selectedUnits.Count > 0 && (singleEntity != null || currentType != SelectionType.Units))
        {
            throw new System.InvalidOperationException("Invariant violé : unités et sélection unique simultanées.");
        }

        if (singleEntity != null && (selectedUnits.Count > 0 || (currentType != SelectionType.Building && currentType != SelectionType.Environment && currentType != SelectionType.Obstacle)))
        {
            throw new System.InvalidOperationException("Invariant violé : sélection unique incohérente.");
        }

        if (currentType == SelectionType.None && (selectedUnits.Count > 0 || singleEntity != null))
        {
            throw new System.InvalidOperationException("Invariant violé : état None non vide.");
        }
    }
}
