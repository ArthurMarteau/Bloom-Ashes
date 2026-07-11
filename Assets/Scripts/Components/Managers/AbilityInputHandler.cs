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
                GameLogger.Warn(GameLogger.Category.UI, "⌨️ AbilityInputHandler: InputManager introuvable, abonnement ignoré.", this);
            }
            return;
        }

        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null && debugAbilityInput)
        {
            GameLogger.Warn(GameLogger.Category.UI, "⌨️ AbilityInputHandler: SelectionManager introuvable, ability clavier inactive.", this);
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
                GameLogger.Warn(GameLogger.Category.UI, "⌨️ AbilityInputHandler: SelectionManager absent, ability ignorée.", this);
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
                GameLogger.Log(GameLogger.Category.UI, "⌨️ AbilityInputHandler: ability ignorée, aucune unité unique sélectionnée.", this);
            }
            return;
        }

        if (CommandExecutor.Instance == null)
        {
            if (debugAbilityInput)
            {
                GameLogger.Warn(GameLogger.Category.UI, "⌨️ AbilityInputHandler: CommandExecutor absent, ability ignorée.", this);
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
                GameLogger.Log(GameLogger.Category.UI, "⌨️ AbilityInputHandler: ability ignorée, bâtiment sélectionné.", this);
            }
            return;
        }

        if (isNexus)
        {
            SubmitAbilityCommand(unit);

            if (debugAbilityInput)
            {
                GameLogger.Log(GameLogger.Category.UI, $"⌨️ AbilityInputHandler: ability Nexus lancée pour {unit.name}.", this);
            }
            return;
        }

        UnitStatsComponent stats = unit.GetComponent<UnitStatsComponent>();
        if (stats == null)
        {
            if (debugAbilityInput)
            {
                GameLogger.Log(GameLogger.Category.UI, "⌨️ AbilityInputHandler: ability ignorée, UnitStatsComponent manquant.", this);
            }
            return;
        }

        UnitStatsData baseStats = stats.GetBaseStats();
        if (baseStats == null || baseStats.abilityData == null)
        {
            if (debugAbilityInput)
            {
                GameLogger.Log(GameLogger.Category.UI, "⌨️ AbilityInputHandler: ability ignorée, aucune AbilityData assignée.", this);
            }
            return;
        }

        SubmitAbilityCommand(unit);

        if (debugAbilityInput)
        {
            GameLogger.Log(GameLogger.Category.UI, $"⌨️ AbilityInputHandler: ability lancée pour {unit.name}.", this);
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
                GameLogger.Log(GameLogger.Category.UI, "⌨️ AbilityInputHandler: ability ignorée, bâtiment sélectionné.", this);
            }
            return true;
        }

        if (!isNexus)
        {
            return false;
        }

        SubmitAbilityCommand(selectedBuilding);

        if (debugAbilityInput)
        {
            GameLogger.Log(GameLogger.Category.UI, $"⌨️ AbilityInputHandler: ability Nexus lancée pour {selectedBuilding.name}.", this);
        }
        return true;
    }

    // Sérialise l'ability principale de l'entité pour une exécution retardée au tick lockstep.
    private bool SubmitAbilityCommand(GameObject caster)
    {
        if (caster == null || CommandExecutor.Instance == null || EntityRegistry.Instance == null)
            return false;

        long casterEntityID = EntityRegistry.Instance.GetEntityID(caster);
        ITeamMember teamMember = caster.GetComponent<ITeamMember>();
        if (casterEntityID < 0 || teamMember == null)
            return false;

        CommandExecutor.Instance.Submit(new GameCommand
        {
            playerID = teamMember.GetTeamID(),
            type = CommandType.CastAbility,
            unitEntityIDs = new[] { casterEntityID },
            abilityIndex = AbilityManager.ENTITY_ABILITY_INDEX
        });
        return true;
    }
}
