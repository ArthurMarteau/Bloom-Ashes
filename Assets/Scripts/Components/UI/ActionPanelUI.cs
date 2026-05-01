using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Panneau d'actions affiché selon la sélection courante — trois contextes mutuellement exclusifs :
// - PRODUCTION      : Rootborn adjacent à un bâtiment → slots de production d'unités
// - UPGRADE         : Bâtiment sélectionné → slots d'upgrade T2/T3/T4
// - FOUNDATION_BUILD: Rootborn local sur une Foundation → slots de construction de bâtiments
// Branché sur les UnityEvents de SelectionManager — pas de polling dans Update
//
// SETUP PREFAB — Hiérarchie attendue :
//   ActionPanel (ce MonoBehaviour + RectTransform — est lui-même le panelRoot)
//   ├── Slot0 (ProductionSlotUI)
//   ├── Slot1 (ProductionSlotUI)
//   ├── Slot2 (ProductionSlotUI)
//   └── Slot3 (ProductionSlotUI)
public class ActionPanelUI : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("4 slots assignés dans l'Inspector, index 0 à 3")]
    [SerializeField] private List<ProductionSlotUI> slots;

    [Header("Animation")]
    [Tooltip("Durée de l'animation d'apparition en secondes")]
    [SerializeField] private float showAnimationDuration = 0.15f;

    [Header("Coûts upgrade")]
    [SerializeField] private int upgradeT2Cost = 50;
    [SerializeField] private int upgradeT3Cost = 80;
    [SerializeField] private int upgradeT4Cost = 100;

    private SelectionManager selectionManager;
    private Coroutine showAnimationCoroutine;
    private MovementController trackedMovementController;
    private BuildingStatsComponent trackedBuilding;
    private FoundationComponent trackedFoundation;
    private GameObject currentRootborn;
    private int trackedTeamID;

    // Enum interne pour savoir quel contexte est actif
    private enum PanelContext { None, Production, Upgrade, FoundationBuild }
    private PanelContext currentContext = PanelContext.None;

    // Trouve le SelectionManager et s'abonne à ses events
    private void Awake()
    {
        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogError("🌿 ActionPanelUI : SelectionManager introuvable dans la scène !");
            return;
        }

        selectionManager.OnUnitsSelected.AddListener(HandleUnitsSelected);
        selectionManager.OnBuildingSelected.AddListener(HandleBuildingSelected);
        selectionManager.OnSelectionCleared.AddListener(HandleSelectionCleared);
        selectionManager.OnGroundSelected.AddListener(OnGroundSelectedAdapter);
        selectionManager.OnObstacleSelected.AddListener(OnObstacleSelectedAdapter);

        HidePanel();
    }

    // Désabonne tous les events lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        if (selectionManager != null)
        {
            selectionManager.OnUnitsSelected.RemoveListener(HandleUnitsSelected);
            selectionManager.OnBuildingSelected.RemoveListener(HandleBuildingSelected);
            selectionManager.OnSelectionCleared.RemoveListener(HandleSelectionCleared);
            selectionManager.OnGroundSelected.RemoveListener(OnGroundSelectedAdapter);
            selectionManager.OnObstacleSelected.RemoveListener(OnObstacleSelectedAdapter);
        }

        UnsubscribeMovementEvents();
        UnsubscribeUpgradeEvents();
        UnsubscribeFoundationEvents();
    }

    // === CONTEXTE PRODUCTION / FOUNDATION BUILD ===

    // Reçoit la liste des unités sélectionnées
    // Priorité : Rootborn local sur Foundation → FoundationBuild ; sinon Production ; sinon HidePanel
    private void HandleUnitsSelected(List<GameObject> units)
    {
        // Nettoie tous les contextes précédents
        UnsubscribeMovementEvents();
        UnsubscribeUpgradeEvents();
        UnsubscribeFoundationEvents();
        currentRootborn = null;
        currentContext  = PanelContext.None;

        if (units == null || units.Count != 1) { HidePanel(); return; }

        GameObject unit = units[0];

        // Vérifie si c'est un Rootborn de l'équipe locale
        UnitStatsComponent unitStats = unit.GetComponent<UnitStatsComponent>();
        TeamComponent teamComp       = unit.GetComponent<TeamComponent>();
        bool isLocalRootborn = unitStats != null && unitStats.IsRootborn()
            && teamComp != null
            && PlayerManager.Instance != null
            && teamComp.GetTeamID() == PlayerManager.Instance.LocalPlayer.teamID;

        if (!isLocalRootborn) { HidePanel(); return; }

        // Priorité 1 : Rootborn sur une Foundation → contexte FoundationBuild
        MovementController movement = unit.GetComponent<MovementController>();
        if (movement != null)
        {
            Vector2Int unitCell = movement.GetCurrentGridPosition();
            FoundationComponent[] foundations = Object.FindObjectsByType<FoundationComponent>(FindObjectsSortMode.None);

            foreach (FoundationComponent foundation in foundations)
            {
                Vector2Int[] cells = foundation.GetOccupiedCells();
                if (cells == null) continue;

                foreach (Vector2Int cell in cells)
                {
                    if (cell != unitCell) continue;

                    trackedFoundation = foundation;
                    trackedFoundation.OnStateChanged.AddListener(RefreshPanel);
                    trackedFoundation.OnDestroyed.AddListener(HandleFoundationDestroyed);
                    currentContext = PanelContext.FoundationBuild;

                    Debug.Log($"🌱 ActionPanelUI : Rootborn local sur Foundation {foundation.gameObject.name} — FoundationBuild");
                    RefreshPanel();
                    return;
                }
            }
        }

        // Priorité 2 : Rootborn adjacent à un bâtiment → contexte Production
        RootbornProductionController pc = unit.GetComponent<RootbornProductionController>();
        if (pc == null) { HidePanel(); return; }

        currentRootborn = unit;
        currentContext  = PanelContext.Production;

        MovementController movementController = currentRootborn.GetComponent<MovementController>();
        if (movementController != null)
        {
            trackedMovementController = movementController;
            trackedMovementController.OnMovementStarted.AddListener(HidePanel);
            trackedMovementController.OnArrived.AddListener(RefreshPanel);
        }

        RefreshPanel();
    }

    // Met à jour les slots de production selon le bâtiment adjacent au Rootborn
    private void RefreshProduction()
    {
        RootbornProductionController pc = currentRootborn.GetComponent<RootbornProductionController>();
        if (pc == null) { HidePanel(); return; }

        BuildingStatsComponent building = pc.FindAdjacentFactionBuilding();
        if (building == null) { HidePanel(); return; }

        List<UnitStatsData> allUnits = building.GetBaseStats().producibleUnits;
        int teamID = currentRootborn.GetComponent<TeamComponent>()?.GetTeamID() ?? 0;

        ShowPanel();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i >= allUnits.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            UnitStatsData unitData = allUnits[i];
            ProductionSlotUI.SlotState state;

            if (unitData.tierRequired > building.GetCurrentTier())
                state = ProductionSlotUI.SlotState.Locked;
            else if (WatersapManager.Instance != null && !WatersapManager.Instance.CanSpend(teamID, unitData.watersapCost))
                state = ProductionSlotUI.SlotState.NotEnoughWatersap;
            else
                state = ProductionSlotUI.SlotState.Available;

            slots[i].Setup(unitData, state, (ud) => { pc.ExecuteTransform(ud); HidePanel(); });
        }

        Debug.Log($"🌿 ActionPanelUI : production — {Mathf.Min(allUnits.Count, slots.Count)} slot(s) pour {building.GetBuildingName()}");
    }

    // Met à jour les slots de construction disponibles sur la Foundation trackée
    private void RefreshFoundationBuild()
    {
        if (trackedFoundation == null) { HidePanel(); return; }

        FoundationData foundationData = trackedFoundation.GetData();
        if (foundationData == null || foundationData.availableBuildings == null) { HidePanel(); return; }

        List<BuildingStatsData> availableBuildings = foundationData.availableBuildings;
        bool canBuild = trackedFoundation.IsReadyToBuild();

        ShowPanel();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i >= availableBuildings.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            BuildingStatsData building = availableBuildings[i];
            ProductionSlotUI.SlotState state = canBuild
                ? ProductionSlotUI.SlotState.Available
                : ProductionSlotUI.SlotState.Locked;

            BuildingStatsData capturedBuilding = building;
            slots[i].Setup(building, state, (_) => trackedFoundation?.RequestBuild(capturedBuilding));
        }

        Debug.Log($"🌱 ActionPanelUI : foundationBuild — canBuild={canBuild}, {Mathf.Min(availableBuildings.Count, slots.Count)} slot(s)");
    }

    // === CONTEXTE UPGRADE ===

    // Reçoit le bâtiment sélectionné — affiche les slots d'upgrade si ce n'est pas un Nexus
    private void HandleBuildingSelected(GameObject buildingObject)
    {
        UnsubscribeMovementEvents();
        UnsubscribeUpgradeEvents();
        UnsubscribeFoundationEvents();
        currentRootborn = null;
        currentContext  = PanelContext.None;

        if (buildingObject == null) { HidePanel(); return; }

        BuildingStatsComponent stats = buildingObject.GetComponent<BuildingStatsComponent>();

        bool isNexus = stats != null && stats.GetBaseStats().isNexus;
        bool isBud   = buildingObject.GetComponent<BudComponent>() != null;

        if (stats == null || isNexus || isBud) { HidePanel(); return; }

        trackedBuilding = stats;
        trackedTeamID   = buildingObject.GetComponent<TeamComponent>()?.GetTeamID() ?? 0;
        currentContext  = PanelContext.Upgrade;

        trackedBuilding.OnTierUpgraded.AddListener(RefreshPanel);

        RefreshPanel();
    }

    // Met à jour les slots d'upgrade T2/T3/T4 selon le tier actuel du bâtiment
    private void RefreshUpgrade()
    {
        int[] costs = { upgradeT2Cost, upgradeT3Cost, upgradeT4Cost };
        List<UnitStatsData> allUnits = trackedBuilding.GetBaseStats().producibleUnits;

        ShowPanel();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i >= 3)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            if (i + 1 >= allUnits.Count)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            UnitStatsData unitData = allUnits[i + 1];
            int tier = i + 2;
            ProductionSlotUI.SlotState state;

            if (tier <= trackedBuilding.GetCurrentTier())
                state = ProductionSlotUI.SlotState.Locked;
            else if (WatersapManager.Instance != null && !WatersapManager.Instance.CanSpend(trackedTeamID, costs[i]))
                state = ProductionSlotUI.SlotState.NotEnoughWatersap;
            else
                state = ProductionSlotUI.SlotState.Available;

            int capturedCost = costs[i];
            slots[i].Setup(unitData, state, (_) => trackedBuilding.UpgradeTier(trackedTeamID, capturedCost));
        }

        Debug.Log($"🌿 ActionPanelUI : upgrade — tier {trackedBuilding.GetCurrentTier()}/{trackedBuilding.GetMaxTier()} pour {trackedBuilding.GetBuildingName()}");
    }

    // === DISPATCH ===

    // Dispatche le refresh selon le contexte actif
    private void RefreshPanel()
    {
        switch (currentContext)
        {
            case PanelContext.Production:
                if (currentRootborn != null) RefreshProduction();
                else HidePanel();
                break;

            case PanelContext.Upgrade:
                if (trackedBuilding != null) RefreshUpgrade();
                else HidePanel();
                break;

            case PanelContext.FoundationBuild:
                if (trackedFoundation != null) RefreshFoundationBuild();
                else HidePanel();
                break;

            default:
                HidePanel();
                break;
        }
    }

    // === FOUNDATION TRACKING ===

    // Appelé quand la Foundation trackée est détruite — masque le panneau sans RemoveListener
    // (La Foundation est en cours de destruction, ses events se nettoient automatiquement)
    private void HandleFoundationDestroyed()
    {
        trackedFoundation = null;
        currentContext    = PanelContext.None;
        HidePanel();
        Debug.Log("🌱 ActionPanelUI : Foundation détruite — panneau masqué");
    }

    // Désabonne proprement les events de la Foundation trackée et oublie la référence
    private void UnsubscribeFoundationEvents()
    {
        if (trackedFoundation == null) return;
        trackedFoundation.OnStateChanged.RemoveListener(RefreshPanel);
        trackedFoundation.OnDestroyed.RemoveListener(HandleFoundationDestroyed);
        trackedFoundation = null;
    }

    // === CLEAR ===

    // Sélection effacée — masque le panneau et réinitialise tout
    private void HandleSelectionCleared()
    {
        UnsubscribeMovementEvents();
        UnsubscribeUpgradeEvents();
        UnsubscribeFoundationEvents();
        currentRootborn = null;
        trackedBuilding = null;
        currentContext  = PanelContext.None;
        HidePanel();
    }

    // Adapte la signature OnGroundSelected (Vector2Int) vers HandleSelectionCleared
    private void OnGroundSelectedAdapter(Vector2Int _) => HandleSelectionCleared();

    // Adapte la signature OnObstacleSelected (GameObject) vers HandleSelectionCleared
    private void OnObstacleSelectedAdapter(GameObject _) => HandleSelectionCleared();

    // Désabonne les events du MovementController tracké
    private void UnsubscribeMovementEvents()
    {
        if (trackedMovementController == null) return;
        try
        {
            trackedMovementController.OnMovementStarted.RemoveListener(HidePanel);
            trackedMovementController.OnArrived.RemoveListener(RefreshPanel);
        }
        catch { }
        trackedMovementController = null;
    }

    // Désabonne OnTierUpgraded du bâtiment tracké et efface la référence
    private void UnsubscribeUpgradeEvents()
    {
        if (trackedBuilding == null) return;
        trackedBuilding.OnTierUpgraded.RemoveListener(RefreshPanel);
        trackedBuilding = null;
    }

    // === SHOW / HIDE / ANIMATION ===

    // Active ce GameObject et lance l'animation de scale X 0 → 1
    private void ShowPanel()
    {
        gameObject.SetActive(true);

        if (showAnimationCoroutine != null)
            StopCoroutine(showAnimationCoroutine);

        showAnimationCoroutine = selectionManager.StartCoroutine(AnimateShow());
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
        Vector3 startScale  = new Vector3(0f, transform.localScale.y, transform.localScale.z);
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
