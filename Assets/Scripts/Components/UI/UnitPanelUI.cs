using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Panneau d'information bas gauche — affiche les infos de la sélection courante
// Branché sur les UnityEvents de SelectionManager (pas de polling dans Update)
// Gère 7 modes : unité simple, groupe, bâtiment, nexus, bud, terrain, obstacle — masqué si aucune sélection
//
// SETUP PREFAB — Hiérarchie attendue (à créer dans l'éditeur) :
//   Canvas (Screen Space Overlay)
//   └── PanelRoot (ancré BottomLeft, pivot 0,0, pos 16,16)
//       ├── Portrait (Image ronde, Mask circulaire)
//       ├── HPContainer (GameObject parent)
//       │   ├── HPBarFill (Image Filled Radial 360 — arc HP)
//       │   └── HPText (TextMeshProUGUI — "20 / 25", 11px)
//       ├── UnitName (TextMeshProUGUI — bold, 14px, blanc)
//       ├── StatSlotATK (GameObject parent avec Image background)
//       │   └── StatTextATK (TextMeshProUGUI — valeur ATK)
//       ├── StatSlotDEF (GameObject parent avec Image background)
//       │   └── StatTextDEF (TextMeshProUGUI — valeur DEF)
//       ├── StatSlotSPD (GameObject parent avec Image background)
//       │   └── StatTextSPD (TextMeshProUGUI — valeur SPD)
//       ├── AbilitySlot (Image — slot ability, enfant = icône)
//       ├── SacrificeSlot (Image — slot sacrifice, enfant = icône)
//       ├── CancelSlot (Image — slot cancel Bud, enfant = icône)
//       ├── BudProgressBarFill (Image Filled — barre de progression production)
//       └── AccessibilityText (TextMeshProUGUI — italique gris, terrain uniquement)
public class UnitPanelUI : MonoBehaviour
{
    // Types d'entités sélectionnables — détermine la configuration de visibilité
    private enum EntityType
    {
        None,        // Aucune sélection
        Unit,        // Unité standard (UnitStatsComponent)
        UnitGroup,   // Groupe de 2+ unités
        Building,    // Bâtiment standard (BuildingStatsComponent)
        Nexus,       // Bâtiment spécial avec ability de production (NexusController)
        Bud,         // Bâtiment temporaire de production (BudComponent)
        Obstacle,    // Obstacle (layer obstacle)
        Ground       // Terrain (tile vide)
    }

    [Header("Racine du panneau")]
    [SerializeField] private GameObject panelRoot; // SetActive(false) quand rien n'est sélectionné

    [Header("Portrait")]
    [SerializeField] private Image portraitImage; // Image enfant dans le Mask — reçoit le sprite du portrait
    [SerializeField] private Sprite groupPortraitSprite;

    [Header("HP")]
    [SerializeField] private GameObject hpContainer;          // GameObject parent contenant HPBarFill + HPText
    [SerializeField] private Image hpBarFill;                 // Image Filled Radial 360 — arc HP (enfant de hpContainer)
    [SerializeField] private TextMeshProUGUI hpText;          // Texte "19/20" sur l'arc HP (enfant de hpContainer)
    [SerializeField] private Image budProgressBarFill;        // Barre de progression du Bud (0→1)

    [Header("Infos unité")]
    [SerializeField] private TextMeshProUGUI unitNameText; // Nom unité (courbé côté gauche)

    [Header("Slots Stats (GameObjects parents)")]
    [SerializeField] private GameObject statSlotATK; // GameObject parent avec Image background
    [SerializeField] private GameObject statSlotDEF;
    [SerializeField] private GameObject statSlotSPD;

    [Header("Stats Textes (TextMeshProUGUI directs)")]
    [SerializeField] private TextMeshProUGUI statTextATK; // TextMeshPro enfant du slot
    [SerializeField] private TextMeshProUGUI statTextDEF;
    [SerializeField] private TextMeshProUGUI statTextSPD;

    [Header("Accessibilité terrain")]
    [SerializeField] private TextMeshProUGUI accessibilityText; // Texte italique gris — terrain uniquement

    [Header("Boutons d'action")]
    [SerializeField] private GameObject abilityButton;         // Ability_Button — affiché si ability disponible
    [SerializeField] private TooltipTrigger abilityTooltipTrigger;
    [SerializeField] private GameObject sacrificeCancelButton; // Sacrifice/Cancel_Button — affiché si unité ou bud
    [SerializeField] private GameObject sacrificeIcon;         // Sacrifice_Icon — enfant de sacrificeCancelButton
    [SerializeField] private GameObject cancelIcon;            // Cancel_Icon — enfant de sacrificeCancelButton
    [SerializeField] private SacrificeAbility sacrificeAbilityData;

    [Header("Watersap Stock (Rootborn)")]
    [SerializeField] private WatersapStockUI watersapStockUI;

    // Entité actuellement sélectionnée — utilisée par les callbacks boutons
    private GameObject currentSelectedEntity;
    private Button cachedAbilityButton;
    private CanvasGroup cachedAbilityCanvasGroup;

    // Couleurs HP selon le pourcentage de vie
    private static readonly Color ColorHPFull   = new Color(0.20f, 0.80f, 0.20f); // vert     > 75%
    private static readonly Color ColorHPGood   = new Color(0.90f, 0.90f, 0.10f); // jaune    > 50%
    private static readonly Color ColorHPLow    = new Color(0.90f, 0.50f, 0.10f); // orange   > 25%
    private static readonly Color ColorHPCrit   = new Color(0.90f, 0.10f, 0.10f); // rouge    ≤ 25%

    // Couleurs de stat selon valeur positive/négative (terrain)
    private static readonly Color ColorStatBuff   = new Color(0.20f, 0.90f, 0.20f); // vert
    private static readonly Color ColorStatDebuff = new Color(0.90f, 0.20f, 0.20f); // rouge

    // Référence à l'unité actuellement trackée pour les mises à jour HP temps réel
    private UnitStatsComponent trackedUnit;
    private MovementController trackedMovementController;
    private AttackController trackedAttackController;
    private RootbornStockComponent trackedStockComponent;
    private RootbornStockComponent trackedRootbornStock;

    // Référence au BudComponent tracké pour la mise à jour de la barre de progression
    private BudComponent trackedBud;

    // Référence au bâtiment actuellement tracké pour les mises à jour HP temps réel
    private BuildingStatsComponent trackedBuilding;

    // Référence au SelectionManager (trouvée une seule fois dans Start)
    private SelectionManager selectionManager;

    // Met à jour la barre de progression du Bud chaque frame
    private void Update()
    {
        if (trackedBud != null && budProgressBarFill != null)
            budProgressBarFill.fillAmount = trackedBud.Progress;

        if (currentSelectedEntity == null || cachedAbilityButton == null)
            return;

        if (trackedRootbornStock == null)
            return;

        MovementController.MovementState state = trackedMovementController != null
            ? trackedMovementController.GetCurrentState()
            : MovementController.MovementState.Idle;
        bool hasStock = trackedStockComponent == null || trackedStockComponent.CurrentStock >= 1;
        bool canUseAbility = trackedMovementController != null
            && state == MovementController.MovementState.Idle
            && (trackedAttackController == null || !trackedAttackController.IsAttacking())
            && hasStock;

        cachedAbilityButton.interactable = canUseAbility;
        if (cachedAbilityCanvasGroup != null)
            cachedAbilityCanvasGroup.alpha = canUseAbility ? 1f : 0.4f;
    }

    // Trouve le SelectionManager et s'abonne à ses events UI
    private void Start()
    {
        // Trouve le SelectionManager dans la scène (usage unique dans Start, acceptable)
        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null)
        {
            GameLogger.Error("🖥️ UnitPanelUI: SelectionManager introuvable dans la scène !", this);
            return;
        }

        // Abonnement aux events de sélection
        selectionManager.OnUnitsSelected.AddListener(HandleUnitsSelected);
        selectionManager.OnGroundSelected.AddListener(HandleGroundSelected);
        selectionManager.OnObstacleSelected.AddListener(HandleObstacleSelected);
        selectionManager.OnEnvironmentSelected.AddListener(HandleEnvironmentSelected);
        selectionManager.OnBuildingSelected.AddListener(HandleBuildingSelected);
        selectionManager.OnSelectionCleared.AddListener(HandleSelectionCleared);

        // Initialise les callbacks des boutons d'action
        Button abilityBtn = abilityButton?.GetComponent<Button>();
        cachedAbilityButton = abilityBtn;
        cachedAbilityCanvasGroup = abilityButton?.GetComponent<CanvasGroup>();
        if (abilityBtn != null) abilityBtn.onClick.AddListener(OnAbilityButtonClicked);
        Button btn = sacrificeCancelButton?.GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnSacrificeOrCancelButtonClicked);

        // Masqué par défaut au démarrage
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        GameLogger.Log(GameLogger.Category.UI, "🖥️ UnitPanelUI: Initialisé et abonné aux events SelectionManager", this);
    }

    // Désabonne les events et untrack l'unité lors de la destruction
    private void OnDestroy()
    {
        if (selectionManager != null)
        {
            selectionManager.OnUnitsSelected.RemoveListener(HandleUnitsSelected);
            selectionManager.OnGroundSelected.RemoveListener(HandleGroundSelected);
            selectionManager.OnObstacleSelected.RemoveListener(HandleObstacleSelected);
            selectionManager.OnEnvironmentSelected.RemoveListener(HandleEnvironmentSelected);
            selectionManager.OnBuildingSelected.RemoveListener(HandleBuildingSelected);
            selectionManager.OnSelectionCleared.RemoveListener(HandleSelectionCleared);
        }

        Button abilityBtn = abilityButton?.GetComponent<Button>();
        if (abilityBtn != null) abilityBtn.onClick.RemoveListener(OnAbilityButtonClicked);
        Button btn = sacrificeCancelButton?.GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveListener(OnSacrificeOrCancelButtonClicked);

        UntrackUnit();
        UntrackBuilding();
    }

    // === HANDLERS EVENTS SÉLECTION ===

    // Reçoit la liste des unités sélectionnées (1 unité = mode simple, 2+ = mode groupe)
    private void HandleUnitsSelected(List<GameObject> units)
    {
        if (units == null || units.Count == 0)
        {
            HandleSelectionCleared();
            return;
        }

        ShowPanel();

        if (units.Count == 1)
        {
            BudComponent budComponent = units[0].GetComponent<BudComponent>();
            if (budComponent != null) { RefreshBud(units[0]); return; }

            RefreshSingleUnit(units[0]);
        }
        else
        {
            RefreshGroup(units.Count);
        }

        GameLogger.Log(GameLogger.Category.UI, $"🖥️ UnitPanelUI: {units.Count} unité(s) sélectionnée(s)", this);
    }

    // Reçoit les coordonnées grille du terrain sélectionné
    private void HandleGroundSelected(Vector2Int gridPosition)
    {
        ShowPanel();
        RefreshGround(gridPosition);
        GameLogger.Log(GameLogger.Category.UI, $"🖥️ UnitPanelUI: Terrain sélectionné en {gridPosition}", this);
    }

    // Reçoit le bâtiment sélectionné
    private void HandleBuildingSelected(GameObject buildingObject)
    {
        if (buildingObject == null)
        {
            HandleSelectionCleared();
            return;
        }

        BudComponent budComponent = buildingObject.GetComponent<BudComponent>();
        if (budComponent != null)
        {
            ShowPanel();
            RefreshBud(buildingObject);
            return;
        }

        ShowPanel();
        RefreshBuilding(buildingObject);
        GameLogger.Log(GameLogger.Category.UI, $"🏗️ UnitPanelUI: Bâtiment sélectionné — {buildingObject.name}", this);
    }

    // Reçoit l'obstacle sélectionné
    private void HandleObstacleSelected(GameObject obstacle)
    {
        ShowPanel();
        RefreshObstacle(obstacle);
        GameLogger.Log(GameLogger.Category.UI, $"🖥️ UnitPanelUI: Obstacle sélectionné — {obstacle?.name}", this);
    }

    // Reçoit l'environnement sélectionné
    private void HandleEnvironmentSelected(GameObject environment)
    {
        ShowPanel();
        watersapStockUI?.Hide();
        UntrackUnit();
        UntrackBuilding();
        trackedBud = null;
        currentSelectedEntity = environment;
        ConfigureVisibility(EntityType.Obstacle);
        ApplyPortraitAndName(environment);
        GameLogger.Log(GameLogger.Category.UI, $"🖥️ UnitPanelUI: Environnement sélectionné — {environment?.name}", this);
    }

    // Aucune sélection — masque le panneau entier
    private void HandleSelectionCleared()
    {
        watersapStockUI?.Hide();
        currentSelectedEntity = null;
        trackedBud = null;
        UntrackUnit();
        UntrackBuilding();
        ConfigureVisibility(EntityType.None);
        HidePanel();
        GameLogger.Log(GameLogger.Category.UI, "🖥️ UnitPanelUI: Sélection effacée — panneau masqué", this);
    }

    // === CONFIGURATION DE VISIBILITÉ CENTRALISÉE ===

    /// <summary>
    /// Configure la visibilité de TOUS les éléments UI selon le type d'entité.
    /// Tout est désactivé par défaut, puis activé sélectivement.
    /// Les éléments conditionnels (abilityButton, statTextDEF/SPD pour Ground, HP pour Bud)
    /// sont gérés dans les méthodes Refresh*() après cet appel.
    /// </summary>
    private void ConfigureVisibility(EntityType type)
    {
        // 1. Désactiver TOUT par défaut (les enfants sont gérés par leurs parents hpContainer/statSlot)
        SetActive(hpContainer,                    false);
        SetActive(statSlotATK,                    false);
        SetActive(statSlotDEF,                    false);
        SetActive(statSlotSPD,                    false);
        SetActive(abilityButton,  false);
        SetActive(sacrificeCancelButton,   false);
        SetActive(budProgressBarFill?.gameObject, false);
        SetActive(accessibilityText?.gameObject,  false);

        // 2. Activer sélectivement selon le type d'entité
        switch (type)
        {
            case EntityType.Unit:
                SetActive(hpContainer,               true);
                SetActive(statSlotATK,               true);
                SetActive(statSlotDEF,               true);
                SetActive(statSlotSPD,               true);
                SetActive(sacrificeCancelButton, true);
                // abilityButton : conditionnel sur statsData.hasActiveAbility (géré dans RefreshSingleUnit)
                break;

            case EntityType.UnitGroup:
                // Seulement portrait et nom — tout le reste masqué
                break;

            case EntityType.Building:
                SetActive(hpContainer, true);
                SetActive(statSlotDEF, true);
                break;

            case EntityType.Nexus:
                SetActive(hpContainer,             true);
                SetActive(statSlotDEF,             true);
                SetActive(abilityButton, true);
                break;

            case EntityType.Bud:
                SetActive(hpContainer,                    true);
                SetActive(statSlotDEF,                    true);
                SetActive(budProgressBarFill?.gameObject, true);
                SetActive(sacrificeCancelButton, true);
                // hpContainer et statSlotDEF : conditionnels sur BuildingStatsComponent (géré dans RefreshBud)
                break;

            case EntityType.Obstacle:
                // Seulement portrait et nom — tout le reste masqué
                break;

            case EntityType.Ground:
                SetActive(accessibilityText?.gameObject, true);
                // statTextDEF : conditionnel sur defMod != 0 (géré dans RefreshGround)
                // statTextSPD : conditionnel sur spdMod != 1.0 (géré dans RefreshGround)
                break;

            case EntityType.None:
            default:
                break;
        }
    }

    // === MODES D'AFFICHAGE ===

    // Mode unité simple : portrait, nom, HP, stats ATK/DEF/SPD
    private void RefreshSingleUnit(GameObject unitObject)
    {
        trackedBud = null;
        UntrackBuilding();

        UnitStatsComponent stats = unitObject.GetComponent<UnitStatsComponent>();
        if (stats == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, $"🖥️ UnitPanelUI: Pas de UnitStatsComponent sur {unitObject.name}", this);
            return;
        }

        ConfigureVisibility(EntityType.Unit);
        currentSelectedEntity = unitObject;

        SetActive(sacrificeIcon, true);
        SetActive(cancelIcon,    false);

        // Début du tracking pour mises à jour HP temps réel
        TrackUnit(stats);

        UnitStatsData statsData = stats.GetBaseStats();
        ApplyPortraitAndName(unitObject);
        ApplyTeamColorToPortrait(unitObject);

        // HP
        RefreshHPDisplay(stats);

        // Stats — affichées et colorées en neutre pour les unités
        SetText(statTextATK, stats.GetAttackDamage().ToString());
        SetText(statTextDEF, stats.GetDefense().ToString());
        SetText(statTextSPD, ConvertSpeedToDisplay(stats.GetMoveSpeed()).ToString());

        SetTextColor(statTextATK, Color.white);
        SetTextColor(statTextDEF, Color.white);
        SetTextColor(statTextSPD, Color.white);

        // Watersap stock UI : visible uniquement pour les Rootborn
        RootbornStockComponent stockComponent = unitObject.GetComponent<RootbornStockComponent>();
        trackedRootbornStock = stockComponent;
        trackedMovementController = stockComponent != null ? unitObject.GetComponent<MovementController>() : null;
        trackedAttackController = currentSelectedEntity.GetComponent<AttackController>();
        trackedStockComponent = currentSelectedEntity.GetComponent<RootbornStockComponent>();
        if (watersapStockUI != null)
        {
            if (stockComponent != null)
                watersapStockUI.Show(stockComponent);
            else
                watersapStockUI.Hide();
        }

        // Slot ability : conditionnel sur la présence d'une AbilityData
        bool showAbility = statsData != null && statsData.abilityData != null;
        SetActive(abilityButton, showAbility);
        if (abilityTooltipTrigger != null && statsData?.abilityData != null)
        {
            string body = $"{statsData.abilityData.description}\n\n<color=#007A8A>Coût : {statsData.abilityData.watersapCost} Watersap</color>";
            abilityTooltipTrigger.SetContent(statsData.abilityData.abilityName, body);
        }
        if (stockComponent == null)
            ResetAbilityButtonState();
    }

    // Mode groupe (2+ unités) : placeholder ×N, nom, tout le reste masqué
    private void RefreshGroup(int count)
    {
        watersapStockUI?.Hide();
        trackedBud = null;
        UntrackUnit();
        UntrackBuilding();

        ConfigureVisibility(EntityType.UnitGroup);

        if (portraitImage != null)
        {
            portraitImage.sprite = groupPortraitSprite;
            portraitImage.color  = groupPortraitSprite != null ? Color.white : new Color(0.45f, 0.35f, 0.15f);
        }

        SetText(unitNameText, $"{count} unités sélectionnées");
    }

    // Mode terrain : portrait couleur terrain, nom, modificateurs SPD/DEF si non nuls
    private void RefreshGround(Vector2Int gridPosition)
    {
        watersapStockUI?.Hide();
        trackedBud = null;
        UntrackUnit();
        UntrackBuilding();

        ConfigureVisibility(EntityType.Ground);

        // Récupère le type de terrain depuis GridManager
        GroundType groundType = GridManager.Instance.GetGroundTypeAt(gridPosition);

        // Portrait : couleur debug du terrain
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.color  = groundType.GetDebugColor();
        }

        // Nom du terrain
        SetText(unitNameText, groundType.GetDisplayName());

        // SPD — affiché uniquement si modificateur != 1.0
        float spdMod = groundType.GetMoveSpeedModifier();
        bool showSPD = !Mathf.Approximately(spdMod, 1f);
        SetActive(statSlotSPD, showSPD);
        if (showSPD && statTextSPD != null)
        {
            if (spdMod > 1f)
            {
                statTextSPD.text  = $"SPD +{Mathf.RoundToInt((spdMod - 1f) * 100f)}%";
                statTextSPD.color = ColorStatBuff;
            }
            else
            {
                statTextSPD.text  = $"SPD -{Mathf.RoundToInt((1f - spdMod) * 100f)}%";
                statTextSPD.color = ColorStatDebuff;
            }
        }

        // DEF — affiché uniquement si modificateur != 0
        int defMod = groundType.GetDefenseModifier();
        bool showDEF = defMod != 0;
        SetActive(statSlotDEF, showDEF);
        if (showDEF && statTextDEF != null)
        {
            if (defMod > 0)
            {
                statTextDEF.text  = $"DEF +{defMod}";
                statTextDEF.color = ColorStatBuff;
            }
            else
            {
                statTextDEF.text  = $"DEF {defMod}";
                statTextDEF.color = ColorStatDebuff;
            }
        }

        // Accessibilité : types d'unités autorisés sur ce terrain
        string accessibility = groundType.GetAccessibleUnits();
        bool showAccessibility = !string.IsNullOrEmpty(accessibility);
        SetActive(accessibilityText?.gameObject, showAccessibility);
        if (showAccessibility && accessibilityText != null)
        {
            accessibilityText.text      = accessibility;
            accessibilityText.fontStyle = FontStyles.Italic;
            accessibilityText.color     = new Color(0.6f, 0.6f, 0.6f);
        }
    }

    // Mode bâtiment : portrait, nom, HP, DEF — détecte Nexus via NexusController
    private void RefreshBuilding(GameObject buildingObject)
    {
        watersapStockUI?.Hide();
        trackedBud = null;
        UntrackUnit();
        UntrackBuilding();

        BuildingStatsComponent stats = buildingObject.GetComponent<BuildingStatsComponent>();
        if (stats == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, $"🏗️ UnitPanelUI: Pas de BuildingStatsComponent sur {buildingObject.name}", this);
            return;
        }

        // Détecte si le bâtiment est un Nexus (abilityButton visible)
        bool isNexus = buildingObject.GetComponent<NexusController>() != null;
        ConfigureVisibility(isNexus ? EntityType.Nexus : EntityType.Building);
        currentSelectedEntity = buildingObject;

        BuildingStatsData statsData = stats.GetBaseStats();
        ApplyPortraitAndName(buildingObject);
        ApplyTeamColorToPortrait(buildingObject);

        // Tracking HP temps réel
        TrackBuilding(stats);
        RefreshBuildingHPDisplay(stats);

        if (abilityTooltipTrigger != null && statsData?.abilityData != null)
        {
            string body = $"{statsData.abilityData.description}\n<color=#007A8A>Coût : {statsData.abilityData.watersapCost} Watersap</color>";
            abilityTooltipTrigger.SetContent(statsData.abilityData.abilityName, body);
        }

        // DEF
        SetText(statTextDEF, stats.GetDefense().ToString());
        SetTextColor(statTextDEF, Color.white);
    }

    // Mode obstacle : placeholder brun/gris, nom du GameObject, le reste masqué
    private void RefreshObstacle(GameObject obstacle)
    {
        watersapStockUI?.Hide();
        trackedBud = null;
        UntrackUnit();
        UntrackBuilding();

        ConfigureVisibility(EntityType.Obstacle);
        currentSelectedEntity = obstacle;

        ApplyPortraitAndName(obstacle);
    }

    // Mode Bud : portrait de l'unité en cours, nom, HP bâtiment, DEF, barre de progression
    private void RefreshBud(GameObject budObject)
    {
        watersapStockUI?.Hide();
        trackedBud = null;
        UntrackUnit();
        UntrackBuilding();

        BudComponent budComponent = budObject.GetComponent<BudComponent>();
        if (budComponent == null) return;

        ConfigureVisibility(EntityType.Bud);
        currentSelectedEntity = budObject;

        SetActive(sacrificeIcon, false);
        SetActive(cancelIcon,    true);

        // Portrait
        if (portraitImage != null)
        {
            if (budComponent.UnitInProduction?.portrait != null)
            {
                portraitImage.sprite = budComponent.UnitInProduction.portrait;
                portraitImage.color  = Color.white;
            }
            else
            {
                portraitImage.sprite = null;
                portraitImage.color  = new Color(0.5f, 0.5f, 0.5f);
            }
        }

        // Nom
        string displayName = !string.IsNullOrEmpty(budComponent.UnitInProduction?.unitName)
            ? budComponent.UnitInProduction.unitName
            : "Bud";
        SetText(unitNameText, displayName);

        // HP et DEF via tracking bâtiment — masqués si pas de BuildingStatsComponent
        BuildingStatsComponent buildingStats = budObject.GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
        {
            TrackBuilding(buildingStats);
            RefreshBuildingHPDisplay(buildingStats);
            SetText(statTextDEF, buildingStats.GetDefense().ToString());
            SetTextColor(statTextDEF, Color.white);
        }
        else
        {
            SetActive(hpContainer, false);
            SetActive(statSlotDEF, false);
        }

        // Barre de progression
        if (budProgressBarFill != null)
            budProgressBarFill.fillAmount = budComponent.Progress;
        trackedBud = budComponent;
    }

    // === TRACKING HP TEMPS RÉEL ===

    // Commence à tracker une unité pour les mises à jour HP en temps réel
    // Désabonne l'ancienne unité, s'abonne à la nouvelle
    private void TrackUnit(UnitStatsComponent unit)
    {
        // Désabonne l'ancienne unité si différente
        if (trackedUnit != null && trackedUnit != unit)
        {
            trackedUnit.OnDamaged.RemoveListener(OnTrackedUnitChanged);
            trackedUnit.OnHealed.RemoveListener(OnTrackedUnitChanged);
            trackedUnit.OnDeath.RemoveListener(OnTrackedUnitChanged);
        }

        trackedUnit = unit;

        // Abonne les events de l'unité pour rafraîchir les HP
        if (trackedUnit != null)
        {
            trackedUnit.OnDamaged.AddListener(OnTrackedUnitChanged);
            trackedUnit.OnHealed.AddListener(OnTrackedUnitChanged);
            trackedUnit.OnDeath.AddListener(OnTrackedUnitChanged);
        }
    }

    // Arrête de tracker l'unité courante
    private void UntrackUnit()
    {
        if (trackedUnit != null)
        {
            trackedUnit.OnDamaged.RemoveListener(OnTrackedUnitChanged);
            trackedUnit.OnHealed.RemoveListener(OnTrackedUnitChanged);
            trackedUnit.OnDeath.RemoveListener(OnTrackedUnitChanged);
            trackedUnit = null;
        }

        trackedMovementController = null;
        trackedAttackController = null;
        trackedStockComponent = null;
        trackedRootbornStock = null;
        ResetAbilityButtonState();
    }

    // Callback déclenché quand l'unité trackée prend des dégâts, est soignée, ou meurt
    private void OnTrackedUnitChanged()
    {
        if (trackedUnit != null)
        {
            RefreshHPDisplay(trackedUnit);
        }
    }

    // Commence à tracker un bâtiment pour les mises à jour HP en temps réel
    private void TrackBuilding(BuildingStatsComponent building)
    {
        if (trackedBuilding != null && trackedBuilding != building)
        {
            trackedBuilding.OnDamaged.RemoveListener(OnTrackedBuildingChanged);
            trackedBuilding.OnHealed.RemoveListener(OnTrackedBuildingChanged);
            trackedBuilding.OnBuildingDestroyed.RemoveListener(OnTrackedBuildingChanged);
        }

        trackedBuilding = building;

        if (trackedBuilding != null)
        {
            trackedBuilding.OnDamaged.AddListener(OnTrackedBuildingChanged);
            trackedBuilding.OnHealed.AddListener(OnTrackedBuildingChanged);
            trackedBuilding.OnBuildingDestroyed.AddListener(OnTrackedBuildingChanged);
        }
    }

    // Arrête de tracker le bâtiment courant
    private void UntrackBuilding()
    {
        if (trackedBuilding != null)
        {
            trackedBuilding.OnDamaged.RemoveListener(OnTrackedBuildingChanged);
            trackedBuilding.OnHealed.RemoveListener(OnTrackedBuildingChanged);
            trackedBuilding.OnBuildingDestroyed.RemoveListener(OnTrackedBuildingChanged);
            trackedBuilding = null;
        }
    }

    // Callback déclenché quand le bâtiment tracké prend des dégâts, est soigné, ou détruit
    private void OnTrackedBuildingChanged()
    {
        if (trackedBuilding != null)
        {
            RefreshBuildingHPDisplay(trackedBuilding);
        }
    }

    // Met à jour la barre HP et le texte HP depuis les getters de BuildingStatsComponent
    private void RefreshBuildingHPDisplay(BuildingStatsComponent stats)
    {
        int currentHP = stats.GetCurrentHP();
        int maxHP     = stats.GetMaxHP();
        float percent = stats.GetHealthPercentage();

        SetText(hpText, $"{currentHP} / {maxHP}");

        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = percent;
            hpBarFill.color = percent > 0.75f ? ColorHPFull
                            : percent > 0.50f ? ColorHPGood
                            : percent > 0.25f ? ColorHPLow
                                              : ColorHPCrit;
        }
    }

    // Met à jour la barre HP et le texte HP depuis les getters de UnitStatsComponent
    private void RefreshHPDisplay(UnitStatsComponent stats)
    {
        int currentHP = stats.GetCurrentHP();
        int maxHP     = stats.GetMaxHP();
        float percent = stats.GetHealthPercentage(); // 0.0 → 1.0

        // Texte HP "actuel / max"
        SetText(hpText, $"{currentHP} / {maxHP}");

        // Couleur du fill selon le pourcentage
        Color fillColor = percent > 0.75f ? ColorHPFull
                        : percent > 0.50f ? ColorHPGood
                        : percent > 0.25f ? ColorHPLow
                                          : ColorHPCrit;

        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = percent;
            hpBarFill.color      = fillColor;
        }
    }

    // Convertit la vitesse float en valeur d'affichage 1/2/3
    // 0.8 → 1 / 1.0 → 2 / 1.2+ → 3
    private int ConvertSpeedToDisplay(float speed)
    {
        if (speed <= 0.9f) return 1;
        if (speed <= 1.1f) return 2;
        return 3;
    }

    // === CALLBACKS BOUTONS UI ===

    // Appelé quand le bouton Ability est cliqué
    private void OnAbilityButtonClicked()
    {
        if (currentSelectedEntity == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "🖱️ Ability button cliqué mais aucune entité sélectionnée", this);
            return;
        }

        AbilityData abilityData = GetAbilityDataFromEntity(currentSelectedEntity);
        if (abilityData == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, $"🖱️ Pas d'AbilityData sur {currentSelectedEntity.name}", this);
            return;
        }

        AbilityManager.Instance.TryExecuteAbility(currentSelectedEntity, abilityData);
    }

    // Appelé quand le bouton Sacrifice ou Cancel est cliqué — contextuel selon l'entité sélectionnée
    private void OnSacrificeOrCancelButtonClicked()
    {
        if (currentSelectedEntity == null) return;

        BudComponent bud = currentSelectedEntity.GetComponent<BudComponent>();
        if (bud != null)
            bud.Cancel();
        else if (sacrificeAbilityData != null)
            AbilityManager.Instance.TryExecuteAbility(currentSelectedEntity, sacrificeAbilityData);
        else
            GameLogger.Warn(GameLogger.Category.UI, " SacrificeAbility non assigné dans UnitPanelUI", this);
    }

    // Récupère l'AbilityData d'une entité (bâtiment ou unité)
    private AbilityData GetAbilityDataFromEntity(GameObject entity)
    {
        BuildingStatsComponent building = entity.GetComponent<BuildingStatsComponent>();
        if (building != null)
            return building.GetBaseStats()?.abilityData;

        UnitStatsComponent unit = entity.GetComponent<UnitStatsComponent>();
        if (unit != null)
            return unit.GetBaseStats()?.abilityData;

        return null;
    }

    // Applique le portrait et le nom via ISelectable avec fallback robuste.
    private void ApplyPortraitAndName(GameObject entity)
    {
        ISelectable selectable = entity?.GetComponent<ISelectable>();
        Sprite portrait = selectable?.GetPortrait();
        string displayName = selectable?.GetDisplayName() ?? entity?.name ?? "Inconnu";

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.color = portrait != null ? Color.white : new Color(0.4f, 0.35f, 0.3f);
        }

        SetText(unitNameText, displayName);
    }

    // Applique la couleur d'équipe au portrait uniquement pour les entités avec TeamComponent.
    private void ApplyTeamColorToPortrait(GameObject entity)
    {
        if (portraitImage == null || portraitImage.sprite == null || portraitImage.material == null)
            return;

        TeamComponent team = entity != null ? entity.GetComponent<TeamComponent>() : null;
        if (team != null)
            portraitImage.material.SetColor("_TeamColor", team.GetTeamColor());
    }

    // === HELPERS SÉCURISÉS ===

    // Affiche ou masque le panneau principal
    private void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }
    }

    // Masque le panneau principal
    private void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    // Active ou désactive un GameObject en sécurité (null-safe)
    private void SetActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }

    private void ResetAbilityButtonState()
    {
        if (cachedAbilityButton != null)
            cachedAbilityButton.interactable = true;

        if (cachedAbilityCanvasGroup != null)
            cachedAbilityCanvasGroup.alpha = 1f;
    }

    // Définit le texte d'un TMP en sécurité (null-safe)
    private void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null)
        {
            tmp.text = text;
        }
    }

    // Définit la couleur d'un TMP en sécurité (null-safe)
    private void SetTextColor(TextMeshProUGUI tmp, Color color)
    {
        if (tmp != null)
        {
            tmp.color = color;
        }
    }

}
