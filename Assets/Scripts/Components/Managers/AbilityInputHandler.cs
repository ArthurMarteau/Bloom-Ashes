using System.Collections.Generic;
using UnityEngine;

// Relie la touche d'ability du InputManager au système d'abilities pour l'unité sélectionnée.
public class AbilityInputHandler : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugAbilityInput = true;

    private InputManager inputManager;
    private SelectionManager selectionManager;
    private GameObject selectedBuilding;

    // Trouve les managers nécessaires et s'abonne à l'event clavier d'ability.
    private void Start()
    {
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            if (debugAbilityInput)
            {
                Debug.LogWarning("⌨️ AbilityInputHandler: InputManager introuvable, abonnement ignoré.");
            }
            return;
        }

        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null && debugAbilityInput)
        {
            Debug.LogWarning("⌨️ AbilityInputHandler: SelectionManager introuvable, ability clavier inactive.");
        }
        else if (selectionManager != null)
        {
            selectionManager.OnBuildingSelected.AddListener(HandleBuildingSelected);
            selectionManager.OnUnitsSelected.AddListener(HandleUnitsSelected);
            selectionManager.OnSelectionCleared.AddListener(HandleSelectionCleared);
            selectionManager.OnGroundSelected.AddListener(HandleGroundSelected);
            selectionManager.OnObstacleSelected.AddListener(HandleObstacleSelected);
        }

        inputManager.OnAbilityKeyPressed.AddListener(HandleAbilityKeyPressed);
    }

    // Désabonne l'event clavier à la destruction pour éviter les listeners orphelins.
    private void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnAbilityKeyPressed.RemoveListener(HandleAbilityKeyPressed);
        }

        if (selectionManager != null)
        {
            selectionManager.OnBuildingSelected.RemoveListener(HandleBuildingSelected);
            selectionManager.OnUnitsSelected.RemoveListener(HandleUnitsSelected);
            selectionManager.OnSelectionCleared.RemoveListener(HandleSelectionCleared);
            selectionManager.OnGroundSelected.RemoveListener(HandleGroundSelected);
            selectionManager.OnObstacleSelected.RemoveListener(HandleObstacleSelected);
        }
    }

    // Tente de lancer l'ability de l'unique unité sélectionnée.
    private void HandleAbilityKeyPressed()
    {
        if (selectionManager == null)
        {
            if (debugAbilityInput)
            {
                Debug.LogWarning("⌨️ AbilityInputHandler: SelectionManager absent, ability ignorée.");
            }
            return;
        }

        if (selectionManager.GetSelectedUnitCount() != 1)
        {
            if (TryExecuteSelectedNexusAbility())
            {
                return;
            }

            if (debugAbilityInput)
            {
                Debug.Log("⌨️ AbilityInputHandler: ability ignorée, aucune unité unique sélectionnée.");
            }
            return;
        }

        if (AbilityManager.Instance == null)
        {
            if (debugAbilityInput)
            {
                Debug.LogWarning("⌨️ AbilityInputHandler: AbilityManager absent, ability ignorée.");
            }
            return;
        }

        List<GameObject> selectedUnits = selectionManager.GetSelectedUnits();
        if (selectedUnits == null || selectedUnits.Count == 0)
        {
            return;
        }

        GameObject unit = selectedUnits[0];
        if (unit == null)
        {
            return;
        }

        bool isBuilding = unit.GetComponent<BuildingStatsComponent>() != null;
        bool isNexus = unit.GetComponent<NexusController>() != null;
        if (isBuilding && !isNexus)
        {
            if (debugAbilityInput)
            {
                Debug.Log("⌨️ AbilityInputHandler: ability ignorée, bâtiment sélectionné.");
            }
            return;
        }

        if (isNexus)
        {
            NexusController nexus = unit.GetComponent<NexusController>();
            nexus.TrySpawnRootborn();

            if (debugAbilityInput)
            {
                Debug.Log($"⌨️ AbilityInputHandler: ability Nexus lancée pour {unit.name}.");
            }
            return;
        }

        UnitStatsComponent stats = unit.GetComponent<UnitStatsComponent>();
        if (stats == null)
        {
            if (debugAbilityInput)
            {
                Debug.Log("⌨️ AbilityInputHandler: ability ignorée, UnitStatsComponent manquant.");
            }
            return;
        }

        UnitStatsData baseStats = stats.GetBaseStats();
        if (baseStats == null || baseStats.abilityData == null)
        {
            if (debugAbilityInput)
            {
                Debug.Log("⌨️ AbilityInputHandler: ability ignorée, aucune AbilityData assignée.");
            }
            return;
        }

        AbilityManager.Instance.TryExecuteAbility(unit, baseStats.abilityData);

        if (debugAbilityInput)
        {
            Debug.Log($"⌨️ AbilityInputHandler: ability lancée pour {unit.name}.");
        }
    }

    // Mémorise le bâtiment sélectionné pour permettre les abilities de bâtiments spéciaux.
    private void HandleBuildingSelected(GameObject building)
    {
        selectedBuilding = building;
    }

    // Efface le bâtiment mémorisé quand une sélection d'unités devient active.
    private void HandleUnitsSelected(List<GameObject> _)
    {
        selectedBuilding = null;
    }

    // Efface le bâtiment mémorisé quand la sélection est vidée.
    private void HandleSelectionCleared()
    {
        selectedBuilding = null;
    }

    // Efface le bâtiment mémorisé quand le terrain est sélectionné.
    private void HandleGroundSelected(Vector2Int _)
    {
        selectedBuilding = null;
    }

    // Efface le bâtiment mémorisé quand un obstacle est sélectionné.
    private void HandleObstacleSelected(GameObject _)
    {
        selectedBuilding = null;
    }

    // Tente de lancer l'ability du Nexus actuellement sélectionné.
    private bool TryExecuteSelectedNexusAbility()
    {
        if (selectedBuilding == null)
        {
            return false;
        }

        bool isBuilding = selectedBuilding.GetComponent<BuildingStatsComponent>() != null;
        NexusController nexus = selectedBuilding.GetComponent<NexusController>();
        bool isNexus = nexus != null;

        if (isBuilding && !isNexus)
        {
            if (debugAbilityInput)
            {
                Debug.Log("⌨️ AbilityInputHandler: ability ignorée, bâtiment sélectionné.");
            }
            return true;
        }

        if (!isNexus)
        {
            return false;
        }

        nexus.TrySpawnRootborn();

        if (debugAbilityInput)
        {
            Debug.Log($"⌨️ AbilityInputHandler: ability Nexus lancée pour {selectedBuilding.name}.");
        }
        return true;
    }
}
