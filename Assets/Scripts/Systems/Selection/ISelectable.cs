using UnityEngine;

// Interface pour exposer les informations d'affichage d'une entité sélectionnable.
public interface ISelectable
{
    Sprite GetPortrait();
    string GetDisplayName();
}
