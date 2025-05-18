using UnityEngine;

public class GameManager : MonoBehaviour
{
    // test
    public static GameManager Instance;
    private UnitSelector selectedUnit;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        
    }

    public void SelectUnit(UnitSelector unit)
    {
        if (selectedUnit != null)
        {
            selectedUnit.GetComponent<UnitHaloEffect>()?.DeactivateHalo();
            selectedUnit.Deselect();
        }
        selectedUnit = unit;
        selectedUnit.GetComponent<UnitHaloEffect>()?.ActivateHalo();
    }

    public UnitSelector GetSelectedUnit()
    {
        return selectedUnit;
    }
}
