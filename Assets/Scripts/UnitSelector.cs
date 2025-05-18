using UnityEngine;

public class UnitSelector : MonoBehaviour
{
    private bool isSelected = false;

    void OnMouseDown()
    {
       GameManager.Instance.SelectUnit(this);
       isSelected = true; 
    }

    public void Deselect()
    {
        isSelected = false;
    }

    public bool IsSelected()
    {
        return isSelected;
    }
}
