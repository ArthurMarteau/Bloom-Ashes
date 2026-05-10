using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    private GameObject currentRootborn;
    private int trackedTeamID;

    // Enum interne pour savoir quel contexte est actif
    private enum PanelContext { None, Production, Revert, Upgrade }
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
    }

    // === CONTEXTE PRODUCTION ===

    // Reçoit la liste des unités sélectionnées
    // Affiche le panneau uniquement si 1 Rootborn avec RootbornProductionController est sélectionné
    private void HandleUnitsSelected(List<GameObject> units)
    {
        if (units == null || units.Count != 1)
        {
            HidePanel();
            return;
        }

        GameObject unit = units[0];
        RootbornProductionController pc = unit.GetComponent<RootbornProductionController>();

        if (pc != null)
        {
            currentRootborn = unit;
            currentContext = PanelContext.Production;

            SubscribeMovementEvents(currentRootborn);

            RefreshPanel();
            return;
        }

        RevertToRootbornController rc = unit.GetComponent<RevertToRootbornController>();
        if (rc == null || rc.FindAdjacentNexus() == null)
        {
            HidePanel();
            return;
        }

        currentRootborn = unit;
        currentContext = PanelContext.Revert;

        SubscribeMovementEvents(currentRootborn);

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

    // === CONTEXTE REVERT ===

    // Met à jour l'unique slot de re-transformation vers Rootborn
    private void RefreshRevert()
    {
        RevertToRootbornController rc = currentRootborn.GetComponent<RevertToRootbornController>();
        if (rc == null) { HidePanel(); return; }
        if (rc.FindAdjacentNexus() == null) { HidePanel(); return; }

        UnitStatsData rootbornData = GetRootbornData(rc);
        if (rootbornData == null) { HidePanel(); return; }

        int revertWatersapCost = GetRevertWatersapCost(rc);
        ProductionSlotUI.SlotState state = rc.CanRevert()
            ? ProductionSlotUI.SlotState.Available
            : ProductionSlotUI.SlotState.NotEnoughWatersap;

        ShowPanel();

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;

            if (i > 0)
            {
                slots[i].gameObject.SetActive(false);
                continue;
            }

            slots[i].Setup(rootbornData, state, (_) => { rc.ExecuteRevert(); HidePanel(); }, revertWatersapCost);
        }

        Debug.Log($"🌿 ActionPanelUI : revert — slot Rootborn ({state}, coût {revertWatersapCost})");
    }

    // === CONTEXTE UPGRADE ===

    // Reçoit le bâtiment sélectionné — affiche les slots d'upgrade si ce n'est pas un Nexus
    private void HandleBuildingSelected(GameObject buildingObject)
    {
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
            case PanelContext.Revert:
                if (currentRootborn != null) RefreshRevert();
                else HidePanel();
                break;
            case PanelContext.Upgrade:
                if (trackedBuilding != null) RefreshUpgrade();
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
        currentRootborn = null;
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
        }
    }

    // Désabonne OnTierUpgraded du bâtiment tracké et efface la référence
    private void UnsubscribeUpgradeEvents()
    {
        if (trackedBuilding == null) return;
        trackedBuilding.OnTierUpgraded.RemoveListener(RefreshPanel);
        trackedBuilding = null;
    }

    private UnitStatsData GetRootbornData(RevertToRootbornController rc)
    {
        PropertyInfo rootbornDataProperty = typeof(RevertToRootbornController).GetProperty("RootbornData", BindingFlags.Instance | BindingFlags.Public);
        if (rootbornDataProperty != null)
            return rootbornDataProperty.GetValue(rc) as UnitStatsData;

        FieldInfo rootbornPrefabField = typeof(RevertToRootbornController).GetField("rootbornPrefab", BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject rootbornPrefab = rootbornPrefabField?.GetValue(rc) as GameObject;
        return rootbornPrefab?.GetComponent<UnitStatsComponent>()?.GetBaseStats();
    }

    private int GetRevertWatersapCost(RevertToRootbornController rc)
    {
        PropertyInfo costProperty = typeof(RevertToRootbornController).GetProperty("RevertWatersapCost", BindingFlags.Instance | BindingFlags.Public);
        if (costProperty != null && costProperty.GetValue(rc) is int cost)
            return cost;

        FieldInfo costField = typeof(RevertToRootbornController).GetField("revertWatersapCost", BindingFlags.Instance | BindingFlags.NonPublic);
        if (costField != null && costField.GetValue(rc) is int fieldCost)
            return fieldCost;

        return 10;
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
