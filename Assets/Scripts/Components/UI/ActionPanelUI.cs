using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Panneau d'actions affiché selon la sélection courante — deux contextes mutuellement exclusifs :
// - PRODUCTION : Rootborn adjacent à un bâtiment → slots de production d'unités
// - UPGRADE    : Bâtiment sélectionné → slots d'upgrade T2/T3/T4
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
    private GameObject currentTransformUnit;
    private int trackedTeamID;

    // Enum interne pour savoir quel contexte est actif
    private enum PanelContext { None, Production, Upgrade, Foundation }
    private PanelContext currentContext = PanelContext.None;

    // Trouve le SelectionManager et s'abonne à ses events
    private void Awake()
    {
        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null)
        {
            GameLogger.Error("🌿 ActionPanelUI : SelectionManager introuvable dans la scène !", this);
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

    // === CONTEXTE PRODUCTION ===

    // Reçoit la liste des unités sélectionnées
    // Affiche le panneau uniquement si 1 unité avec TransformationController et un bâtiment compatible est sélectionnée
    private void HandleUnitsSelected(List<GameObject> units)
    {
        UnsubscribeMovementEvents();
        UnsubscribeUpgradeEvents();
        UnsubscribeFoundationEvents();
        currentTransformUnit = null;

        if (units == null || units.Count != 1)
        {
            HidePanel();
            return;
        }

        GameObject unit = units[0];
        TransformationController tc = unit.GetComponent<TransformationController>();
        if (tc == null)
        {
            HidePanel();
            return;
        }
        GameLogger.Log(GameLogger.Category.UI, $"️ HandleUnitsSelected : tc trouvé sur {unit.name}", this);

        currentTransformUnit = unit;
        SubscribeMovementEvents(unit);

        BuildingStatsComponent adjacentBuilding = tc.FindAdjacentCompatibleBuilding();
        GameLogger.Log(GameLogger.Category.UI, $"️ AdjacentBuilding={(adjacentBuilding != null ? "trouvé" : "null")}", this);
        if (adjacentBuilding != null)
        {
            currentContext = PanelContext.Production;
            RefreshPanel();
            return;
        }

        FoundationComponent foundation = tc.FindFoundationUnderRootborn();
        GameLogger.Log(GameLogger.Category.UI, $"️ Foundation={(foundation != null ? foundation.gameObject.name : "null")}, idleCount={(foundation != null ? foundation.GetIdleRootbornCount() : -1)}", this);
        if (foundation != null && foundation.GetIdleRootbornCount() > 0)
        {
            currentContext = PanelContext.Foundation;
            trackedFoundation = foundation;
            trackedFoundation.OnFoundationStateChanged.AddListener(RefreshPanel);
            RefreshPanel();
            return;
        }

        HidePanel();
    }

    // Met à jour les slots de transformation selon le bâtiment compatible adjacent
    private void RefreshProduction()
    {
        TransformationController tc = currentTransformUnit.GetComponent<TransformationController>();
        if (tc == null) { HidePanel(); return; }

        BuildingStatsComponent building = tc.FindAdjacentCompatibleBuilding();
        if (building == null) { HidePanel(); return; }

        List<UnitStatsData> allUnits = building.GetBaseStats().producibleUnits;

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
            else if (!tc.CanTransform(unitData))
                state = ProductionSlotUI.SlotState.NotEnoughWatersap;
            else
                state = ProductionSlotUI.SlotState.Available;

            slots[i].Setup(unitData, state, (ud) => { tc.ExecuteTransform(ud); HidePanel(); });
        }

        GameLogger.Log(GameLogger.Category.UI, $"🌿 ActionPanelUI : production — {Mathf.Min(allUnits.Count, slots.Count)} slot(s) pour {building.GetBuildingName()}", this);
    }

    // === CONTEXTE UPGRADE ===

    // Reçoit le bâtiment sélectionné — affiche les slots d'upgrade si ce n'est pas un Nexus
    private void HandleBuildingSelected(GameObject buildingObject)
    {
        UnsubscribeMovementEvents();
        UnsubscribeFoundationEvents();
        currentTransformUnit = null;

        if (buildingObject == null) { HidePanel(); return; }

        BuildingStatsComponent stats = buildingObject.GetComponent<BuildingStatsComponent>();

        // Masque le panneau pour le Nexus et le Bud (pas d'upgrades pour eux)
        bool isNexus = stats != null && stats.GetBaseStats().isNexus;
        bool isBud   = buildingObject.GetComponent<BudComponent>() != null;

        if (stats == null || isNexus || isBud) { HidePanel(); return; }

        UnsubscribeUpgradeEvents();

        trackedBuilding = stats;
        trackedTeamID = buildingObject.GetComponent<TeamComponent>()?.GetTeamID() ?? 0;
        currentContext = PanelContext.Upgrade;

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

            // Seulement 3 slots d'upgrade (T2/T3/T4) — le 4e est toujours masqué
            if (i >= 3)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            // Pas d'unité à afficher pour ce tier → slot masqué
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
            slots[i].Setup(unitData, state, (_) => { trackedBuilding.UpgradeTier(trackedTeamID, capturedCost); }, capturedCost);
        }

        GameLogger.Log(GameLogger.Category.UI, $"🌿 ActionPanelUI : upgrade — tier {trackedBuilding.GetCurrentTier()}/{trackedBuilding.GetMaxTier()} pour {trackedBuilding.GetBuildingName()}", this);
    }

    // === CONTEXTE FOUNDATION ===

    // Met à jour les slots de construction selon l'état des Rootborn sur la Foundation
    private void RefreshFoundation()
    {
        if (trackedFoundation == null || trackedFoundation.GetIdleRootbornCount() == 0)
        {
            HidePanel();
            return;
        }

        FoundationStatsData foundationStats = trackedFoundation.GetFoundationStats();
        if (foundationStats == null)
        {
            HidePanel();
            return;
        }

        List<BuildingStatsData> buildings = foundationStats.constructibleBuildings;
        bool ready = trackedFoundation.IsReadyToBuild();
        int teamID = trackedFoundation.GetTeamID();

        ShowPanel();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (buildings == null || i >= buildings.Count || buildings[i] == null)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            BuildingStatsData buildingData = buildings[i];
            ProductionSlotUI.SlotState state = ready
                ? ProductionSlotUI.SlotState.Available
                : ProductionSlotUI.SlotState.Locked;

            slots[i].Setup(buildingData, state, (bd) =>
            {
                trackedFoundation?.ExecuteBuild(bd, teamID);
                HidePanel();
            });
        }

        GameLogger.Log(GameLogger.Category.UI, $"🌿 ActionPanelUI : foundation — {trackedFoundation.GetIdleRootbornCount()}/4 Rootborn idle, ready={ready}", this);
    }

    // === DISPATCH ===

    // Dispatche le refresh selon le contexte actif
    private void RefreshPanel()
    {
        switch (currentContext)
        {
            case PanelContext.Production:
                if (currentTransformUnit != null) RefreshProduction();
                else HidePanel();
                break;
            case PanelContext.Upgrade:
                if (trackedBuilding != null) RefreshUpgrade();
                else HidePanel();
                break;
            case PanelContext.Foundation:
                if (trackedFoundation != null) RefreshFoundation();
                else HidePanel();
                break;
            default:
                HidePanel();
                break;
        }
    }

    // === CLEAR ===

    // Sélection effacée — masque le panneau et réinitialise tout
    private void HandleSelectionCleared()
    {
        UnsubscribeMovementEvents();
        UnsubscribeUpgradeEvents();
        UnsubscribeFoundationEvents();
        currentTransformUnit = null;
        trackedBuilding = null;
        currentContext = PanelContext.None;
        HidePanel();
    }

    // Adapte la signature OnGroundSelected (Vector2Int) vers HandleSelectionCleared
    private void OnGroundSelectedAdapter(Vector2Int _) => HandleSelectionCleared();

    // Adapte la signature OnObstacleSelected (GameObject) vers HandleSelectionCleared
    private void OnObstacleSelectedAdapter(GameObject _) => HandleSelectionCleared();

    // Désabonne les events du MovementController tracké (try/catch pour objets détruits)
    private void UnsubscribeMovementEvents()
    {
        if (trackedMovementController == null) return;
        try
        {
            trackedMovementController.OnMovementStarted.RemoveListener(HidePanel);
            trackedMovementController.OnArrived.RemoveListener(RefreshPanel);
            trackedMovementController.OnIdleStateChanged.RemoveListener(HandleRootbornIdleChanged);
        }
        catch { }
        trackedMovementController = null;
    }

    // Abonne le panneau au mouvement de l'unité courante pour masquer au départ et refresh à l'arrivée
    private void SubscribeMovementEvents(GameObject unit)
    {
        UnsubscribeMovementEvents();

        MovementController movementController = unit.GetComponent<MovementController>();
        if (movementController != null)
        {
            trackedMovementController = movementController;
            trackedMovementController.OnMovementStarted.AddListener(HidePanel);
            trackedMovementController.OnArrived.AddListener(RefreshPanel);
            trackedMovementController.OnIdleStateChanged.AddListener(HandleRootbornIdleChanged);
        }
    }

    // Réévalue le panneau quand le Rootborn sélectionné devient idle après un déplacement
    private void HandleRootbornIdleChanged(bool isIdle)
    {
        if (!isIdle || currentTransformUnit == null) return;
        if (currentContext == PanelContext.Production || currentContext == PanelContext.Foundation) return;

        HandleUnitsSelected(new List<GameObject> { currentTransformUnit });
    }

    // Désabonne OnTierUpgraded du bâtiment tracké et efface la référence
    private void UnsubscribeUpgradeEvents()
    {
        if (trackedBuilding == null) return;
        trackedBuilding.OnTierUpgraded.RemoveListener(RefreshPanel);
        trackedBuilding = null;
    }

    // Désabonne les events de la Foundation trackée
    private void UnsubscribeFoundationEvents()
    {
        if (trackedFoundation == null) return;
        trackedFoundation.OnFoundationStateChanged.RemoveListener(RefreshPanel);
        trackedFoundation = null;
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
