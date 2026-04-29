using System.Collections.Generic;
using UnityEngine;

// ScriptableObject global définissant les bâtiments constructibles sur une Foundation
// Réutilisable : une même FoundationData peut être partagée par plusieurs Foundation identiques
// Pattern prévu pour les futures veines de Watersap et autres environnements activables
[CreateAssetMenu(fileName = "New Foundation Data", menuName = "Bloom and Ashes/Data/Foundation Data")]
public class FoundationData : ScriptableObject
{
    [Header("Bâtiments constructibles")]
    [Tooltip("Liste des bâtiments 2x2 pouvant être construits sur cette fondation")]
    public List<BuildingStatsData> availableBuildings = new List<BuildingStatsData>();

    // Avertit dans l'inspecteur si un bâtiment de la liste n'est pas de taille 2x2
    private void OnValidate()
    {
        if (availableBuildings == null) return;

        foreach (BuildingStatsData building in availableBuildings)
        {
            if (building == null) continue;

            if (building.sizeX != 2 || building.sizeY != 2)
            {
                Debug.LogWarning($"🌱 FoundationData [{name}]: '{building.buildingName}' n'est pas un bâtiment 2x2 " +
                                 $"(sizeX={building.sizeX}, sizeY={building.sizeY}) — il sera ignoré lors du build");
            }
        }
    }
}
