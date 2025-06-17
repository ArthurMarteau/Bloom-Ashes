using UnityEngine;

public class UnitSelector : MonoBehaviour
{
    private bool isSelected = false;

    void OnMouseDown()
    {
        GameManager.Instance.SelectUnit(this);
    }

    public void Select()
    {
        isSelected = true;
        GetComponent<UnitHaloEffect>()?.ActivateHalo();
    }

    public void Deselect()
    {
        isSelected = false;
        GetComponent<UnitHaloEffect>()?.DeactivateHalo();
    }

    public bool IsSelected()
    {
        return isSelected;
    }
}

