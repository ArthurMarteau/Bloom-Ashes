using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Panneau de production affiché quand un Rootborn allié adjacent à un bâtiment est sélectionné
// Affiche jusqu'à 4 slots correspondant aux unités produisibles du bâtiment adjacent
// Branché sur les UnityEvents de SelectionManager — pas de polling dans Update
//
// SETUP PREFAB — Hiérarchie attendue :
//   ProductionPanel (ce MonoBehaviour + RectTransform — est lui-même le panelRoot)
//   ├── Slot0 (ProductionSlotUI)
//   ├── Slot1 (ProductionSlotUI)
//   ├── Slot2 (ProductionSlotUI)
//   └── Slot3 (ProductionSlotUI)
public class ProductionPanelUI : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("4 slots assignés dans l'Inspector, index 0 à 3")]
    [SerializeField] private List<ProductionSlotUI> slots;

    [Header("Animation")]
    [Tooltip("Durée de l'animation d'apparition en secondes")]
    [SerializeField] private float showAnimationDuration = 0.15f;

    // Nombre maximum de slots affichés
    private const int MaxSlots = 4;

    // Rootborn actuellement sélectionné avec un bâtiment adjacent
    private GameObject currentRootborn;

    // Référence au SelectionManager (trouvée dans Start)
    private SelectionManager selectionManager;

    // Coroutine d'animation en cours — stockée pour pouvoir l'annuler
    private Coroutine showAnimationCoroutine;

    // MovementController du Rootborn tracké — désabonné à chaque changement de sélection
    private MovementController trackedMovementController;

    // Trouve le SelectionManager et s'abonne à ses events
    private void Start()
    {
        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogError("🌿 ProductionPanelUI : SelectionManager introuvable dans la scène !");
            return;
        }

        selectionManager.OnUnitsSelected.AddListener(HandleUnitsSelected);
        selectionManager.OnSelectionCleared.AddListener(HandleSelectionCleared);
        selectionManager.OnGroundSelected.AddListener(OnNonRootbornSelected);
        selectionManager.OnBuildingSelected.AddListener(OnNonRootbornSelected);
        selectionManager.OnObstacleSelected.AddListener(OnNonRootbornSelected);

        HidePanel();
    }

    // Désabonne les events lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        if (selectionManager != null)
        {
            selectionManager.OnUnitsSelected.RemoveListener(HandleUnitsSelected);
            selectionManager.OnSelectionCleared.RemoveListener(HandleSelectionCleared);
            selectionManager.OnGroundSelected.RemoveListener(OnNonRootbornSelected);
            selectionManager.OnBuildingSelected.RemoveListener(OnNonRootbornSelected);
            selectionManager.OnObstacleSelected.RemoveListener(OnNonRootbornSelected);
        }

        UnsubscribeMovementEvents();
    }

    // === HANDLERS SÉLECTION ===

    // Reçoit la liste des unités sélectionnées depuis SelectionManager
    // Affiche le panneau uniquement si 1 Rootborn avec bâtiment adjacent est sélectionné
    private void HandleUnitsSelected(List<GameObject> units)
    {
        if (units == null || units.Count != 1)
        {
            HidePanel();
            return;
        }

        GameObject unit = units[0];
        RootbornProductionController productionController = unit.GetComponent<RootbornProductionController>();

        if (productionController == null)
        {
            HidePanel();
            return;
        }

        currentRootborn = unit;

        // Désabonne l'ancien Rootborn puis s'abonne au nouveau
        UnsubscribeMovementEvents();
        MovementController movementController = currentRootborn.GetComponent<MovementController>();
        if (movementController != null)
        {
            trackedMovementController = movementController;
            trackedMovementController.OnMovementStarted.AddListener(HidePanel);
            trackedMovementController.OnArrived.AddListener(RefreshPanelNextFrame);
        }

        RefreshPanelNextFrame();
    }

    // Sélection effacée — masque le panneau et réinitialise le Rootborn courant
    private void HandleSelectionCleared()
    {
        UnsubscribeMovementEvents();
        HidePanel();
        currentRootborn = null;
    }

    // Adapte la signature OnGroundSelected (Vector2Int) vers HandleSelectionCleared
    private void OnNonRootbornSelected(Vector2Int _) { HandleSelectionCleared(); }

    // Adapte la signature OnBuildingSelected / OnObstacleSelected (GameObject) vers HandleSelectionCleared
    private void OnNonRootbornSelected(GameObject _) { HandleSelectionCleared(); }

    // Désabonne les events du MovementController tracké et efface la référence
    private void UnsubscribeMovementEvents()
    {
        if (trackedMovementController == null) return;

        trackedMovementController.OnMovementStarted.RemoveListener(HidePanel);
        trackedMovementController.OnArrived.RemoveListener(RefreshPanelNextFrame);
        trackedMovementController = null;
    }

    // === LOGIQUE D'AFFICHAGE ===

    // Met à jour les slots selon le bâtiment adjacent au Rootborn sélectionné
    // Masque les slots excédentaires, détermine l'état de chaque unité produisible
    private void RefreshPanel()
    {
        if (currentRootborn == null)
        {
            HidePanel();
            return;
        }

        RootbornProductionController productionController = currentRootborn.GetComponent<RootbornProductionController>();
        if (productionController == null)
        {
            HidePanel();
            return;
        }

        // Vérifie qu'un bâtiment de faction est adjacent
        BuildingStatsComponent adjacentBuilding = productionController.FindAdjacentFactionBuilding();
        if (adjacentBuilding == null)
        {
            HidePanel();
            return;
        }

        // Récupère les unités produisibles (max MaxSlots)
        List<UnitStatsData> producibleUnits = adjacentBuilding.GetBaseStats().producibleUnits;

        // Récupère l'équipe du Rootborn pour vérifier le Watersap
        int teamID = 0;
        TeamComponent teamComponent = currentRootborn.GetComponent<TeamComponent>();
        if (teamComponent != null)
            teamID = teamComponent.GetTeamID();

        ShowPanel();

        int slotCount = Mathf.Min(slots.Count, MaxSlots);

        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i] == null) continue;

            // Slot sans unité correspondante : masqué
            if (i >= producibleUnits.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            UnitStatsData unitData = producibleUnits[i];
            ProductionSlotUI.SlotState state = DetermineSlotState(unitData, teamID);

            slots[i].Setup(unitData, state, OnSlotClicked);
        }

        Debug.Log($"🌿 ProductionPanelUI : panneau actualisé — {Mathf.Min(producibleUnits.Count, slotCount)} slot(s) pour {adjacentBuilding.GetBuildingName()}");
    }

    // Lance un rafraîchissement au frame suivant pour laisser le temps au Rootborn de se repositionner
    private void RefreshPanelNextFrame()
    {
        // Le ProductionPanel peut être inactif — on passe par un GO toujours actif
        if (selectionManager != null)
            selectionManager.StartCoroutine(RefreshPanelDelayed());
    }

    // Attend un frame puis appelle RefreshPanel
    private IEnumerator RefreshPanelDelayed()
    {
        yield return null;
        RefreshPanel();
    }

    // Détermine l'état d'un slot selon le tier requis et le Watersap disponible
    private ProductionSlotUI.SlotState DetermineSlotState(UnitStatsData unitData, int teamID)
    {
        // Tier 2, 3 ou 4 : verrouillé tant que la recherche n'est pas implémentée
        if (unitData.tierRequired > 1)
            return ProductionSlotUI.SlotState.Locked;

        // Tier 1 : vérifie le Watersap
        if (WatersapManager.Instance != null && !WatersapManager.Instance.CanSpend(teamID, unitData.watersapCost))
            return ProductionSlotUI.SlotState.NotEnoughWatersap;

        return ProductionSlotUI.SlotState.Available;
    }

    // Callback déclenché par le clic sur un slot disponible
    // Demande la transformation au RootbornProductionController puis masque le panneau
    private void OnSlotClicked(UnitStatsData unitData)
    {
        if (currentRootborn == null)
        {
            Debug.LogWarning("🌿 ProductionPanelUI : OnSlotClicked — currentRootborn null");
            return;
        }

        RootbornProductionController productionController = currentRootborn.GetComponent<RootbornProductionController>();
        if (productionController == null)
        {
            Debug.LogWarning($"🌿 ProductionPanelUI : OnSlotClicked — RootbornProductionController introuvable sur {currentRootborn.name}");
            return;
        }

        Debug.Log($"🌿 ProductionPanelUI : clic sur {unitData.unitName} — lancement de la transformation");

        productionController.ExecuteTransform(unitData);
        HidePanel();
    }

    // === SHOW / HIDE ===

    // Active ce GameObject et lance l'animation de scale X 0 → 1
    private void ShowPanel()
    {
        gameObject.SetActive(true);

        // Annule l'animation précédente si elle était en cours
        if (showAnimationCoroutine != null)
            StopCoroutine(showAnimationCoroutine);

        showAnimationCoroutine = StartCoroutine(AnimateShow());
    }

    // Masque ce GameObject et remet le scale X à 0 instantanément
    private void HidePanel()
    {
        if (showAnimationCoroutine != null)
        {
            StopCoroutine(showAnimationCoroutine);
            showAnimationCoroutine = null;
        }

        Vector3 scale = transform.localScale;
        transform.localScale = new Vector3(0f, scale.y, scale.z);

        gameObject.SetActive(false);
    }

    // Anime le scale X de ce GameObject de 0 à 1 sur showAnimationDuration secondes
    private IEnumerator AnimateShow()
    {
        Vector3 startScale = new Vector3(0f, transform.localScale.y, transform.localScale.z);
        Vector3 targetScale = new Vector3(1f, transform.localScale.y, transform.localScale.z);
        transform.localScale = startScale;

        float elapsed = 0f;

        while (elapsed < showAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / showAnimationDuration);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
        showAnimationCoroutine = null;
    }
}
