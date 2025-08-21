using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private UnitSelector selectedUnit;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void SelectUnit(UnitSelector unit)
    {
        if (selectedUnit != null)
        {
            selectedUnit.Deselect();
        }

        selectedUnit = unit;
        selectedUnit.Select();
    }

    public UnitSelector GetSelectedUnit()
    {
        return selectedUnit;
    }
    public void ClearSelection()
    {
        if (selectedUnit != null)
        {
            selectedUnit.Deselect();
            selectedUnit = null;
        }
    }

}

