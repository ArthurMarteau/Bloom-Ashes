using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Panneau d'information bas gauche — affiche les infos de la sélection courante
// Branché sur les UnityEvents de SelectionManager (pas de polling dans Update)
// Gère 4 modes : unité simple, groupe, terrain, obstacle — masqué si aucune sélection
//
// SETUP PREFAB — Hiérarchie attendue (à créer dans l'éditeur) :
//   Canvas (Screen Space Overlay)
//   └── PanelRoot (ancré BottomLeft, pivot 0,0, pos 16,16)
//       ├── Portrait (Image ronde, Mask circulaire)
//       ├── HPBar (Image Filled Radial 360 — arc HP)
//       ├── HPText (TextMeshProUGUI — "20 / 25", 11px)
//       ├── UnitName (TextMeshProUGUI — bold, 14px, blanc)
//       ├── StatValueATK (TextMeshProUGUI — valeur ATK avec modificateurs)
//       ├── StatValueDEF (TextMeshProUGUI — valeur DEF avec modificateurs)
//       ├── StatValueSPD (TextMeshProUGUI — valeur SPD)
//       └── AccessibilityText (TextMeshProUGUI — italique gris, terrain uniquement)
public class UnitPanelUI : MonoBehaviour
{
    [Header("Racine du panneau")]
    [SerializeField] private GameObject panelRoot; // SetActive(false) quand rien n'est sélectionné

    [Header("Portrait")]
    [SerializeField] private Image portraitImage; // Image enfant dans le Mask — reçoit le sprite du portrait

    [Header("HP")]
    [SerializeField] private Image hpBarFill;              // Image Filled Radial 360 — arc HP
    [SerializeField] private TextMeshProUGUI hpText;       // Texte "19/20" sur l'arc HP
    [SerializeField] private Image budProgressBarFill;     // Barre de progression du Bud (0→1)

    [Header("Infos unité")]
    [SerializeField] private TextMeshProUGUI unitNameText; // Nom unité (courbé côté gauche)
    [SerializeField] private TextMeshProUGUI statValueATK; // Valeur ATK avec modificateurs
    [SerializeField] private TextMeshProUGUI statValueDEF; // Valeur DEF avec modificateurs
    [SerializeField] private TextMeshProUGUI statValueSPD; // Valeur SPD (converti en 1/2/3)

    [Header("Accessibilité terrain")]
    [SerializeField] private TextMeshProUGUI accessibilityText; // Texte italique gris — terrain uniquement

    [Header("Slots Ability")]
    [SerializeField] private Image abilitySlot;   // Background du slot ability (enfant = icône)
    [SerializeField] private Image sacrificeSlot; // Background du slot sacrifice (enfant = icône)


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
    }

    // Trouve le SelectionManager et s'abonne à ses events UI
    private void Start()
    {
        // Trouve le SelectionManager dans la scène (usage unique dans Start, acceptable)
        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        if (selectionManager == null)
        {
            Debug.LogError("🖥️ UnitPanelUI: SelectionManager introuvable dans la scène !");
            return;
        }

        // Abonnement aux events de sélection
        selectionManager.OnUnitsSelected.AddListener(HandleUnitsSelected);
        selectionManager.OnGroundSelected.AddListener(HandleGroundSelected);
        selectionManager.OnObstacleSelected.AddListener(HandleObstacleSelected);
        selectionManager.OnBuildingSelected.AddListener(HandleBuildingSelected);
        selectionManager.OnSelectionCleared.AddListener(HandleSelectionCleared);

        // Masqué par défaut au démarrage
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        Debug.Log("🖥️ UnitPanelUI: Initialisé et abonné aux events SelectionManager");
    }

    // Désabonne les events et untrack l'unité lors de la destruction
    private void OnDestroy()
    {
        if (selectionManager != null)
        {
            selectionManager.OnUnitsSelected.RemoveListener(HandleUnitsSelected);
            selectionManager.OnGroundSelected.RemoveListener(HandleGroundSelected);
            selectionManager.OnObstacleSelected.RemoveListener(HandleObstacleSelected);
            selectionManager.OnBuildingSelected.RemoveListener(HandleBuildingSelected);
            selectionManager.OnSelectionCleared.RemoveListener(HandleSelectionCleared);
        }

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
            if (budComponent != null) { ShowPanel(); RefreshBud(units[0]); return; }

            RefreshSingleUnit(units[0]);
        }
        else
        {
            RefreshGroup(units.Count);
        }

        Debug.Log($"🖥️ UnitPanelUI: {units.Count} unité(s) sélectionnée(s)");
    }

    // Reçoit les coordonnées grille du terrain sélectionné
    private void HandleGroundSelected(Vector2Int gridPosition)
    {
        ShowPanel();
        RefreshGround(gridPosition);
        Debug.Log($"🖥️ UnitPanelUI: Terrain sélectionné en {gridPosition}");
    }

    // Reçoit le bâtiment sélectionné
    private void HandleBuildingSelected(GameObject buildingObject)
    {
        BudComponent budComponent = buildingObject.GetComponent<BudComponent>();
        if (budComponent != null)
        {
            ShowPanel();
            RefreshBud(buildingObject);
            return;
        }

        if (buildingObject == null)
        {
            HandleSelectionCleared();
            return;
        }

        ShowPanel();
        RefreshBuilding(buildingObject);
        Debug.Log($"🏗️ UnitPanelUI: Bâtiment sélectionné — {buildingObject.name}");
    }

    // Reçoit l'obstacle sélectionné
    private void HandleObstacleSelected(GameObject obstacle)
    {
        ShowPanel();
        RefreshObstacle(obstacle);
        Debug.Log($"🖥️ UnitPanelUI: Obstacle sélectionné — {obstacle?.name}");
    }

    // Aucune sélection — masque le panneau entier
    private void HandleSelectionCleared()
    {
        trackedBud = null;
        HidePanel();
        UntrackUnit();
        UntrackBuilding();
        Debug.Log("🖥️ UnitPanelUI: Sélection effacée — panneau masqué");
    }

    // === MODES D'AFFICHAGE ===

    // Mode unité simple : portrait, nom, HP, stats ATK/DEF/SPD
    private void RefreshSingleUnit(GameObject unitObject)
    {
        SetActive(budProgressBarFill?.gameObject, false);
        trackedBud = null;

        UnitStatsComponent stats = unitObject.GetComponent<UnitStatsComponent>();
        if (stats == null)
        {
            Debug.LogWarning($"🖥️ UnitPanelUI: Pas de UnitStatsComponent sur {unitObject.name}");
            return;
        }

        // Début du tracking pour mises à jour HP temps réel
        TrackUnit(stats);

        // Portrait : lu depuis le ScriptableObject UnitStatsData, sinon placeholder coloré
        UnitStatsData statsData = stats.GetBaseStats();
        if (portraitImage != null)
        {
            if (statsData != null && statsData.portrait != null)
            {
                portraitImage.sprite = statsData.portrait;
                portraitImage.color  = Color.white;
                TeamComponent team = unitObject.GetComponent<TeamComponent>();
                if (team != null && portraitImage.material != null)
                    portraitImage.material.SetColor("_TeamColor", team.GetTeamColor());
            }
            else
            {
                portraitImage.sprite = null;
                portraitImage.color  = new Color(0.20f, 0.30f, 0.45f);
            }
        }

        // Nom depuis le SO (évite "Rootborn(1)" etc.)
        string displayName = (statsData != null && !string.IsNullOrEmpty(statsData.unitName)) ? statsData.unitName : unitObject.name;
        SetText(unitNameText, displayName);

        // HP — affiche valeurs initiales
        RefreshHPDisplay(stats);

        // Affiche HP bar
        SetActive(hpBarFill?.gameObject, true);
        SetActive(hpText?.gameObject, true);

        // Stats — affichées et colorées en neutre pour les unités
        SetActive(statValueATK?.gameObject, true);
        SetActive(statValueDEF?.gameObject, true);
        SetActive(statValueSPD?.gameObject, true);

        SetText(statValueATK, stats.GetAttackDamage().ToString());
        SetText(statValueDEF, stats.GetDefense().ToString());
        SetText(statValueSPD, ConvertSpeedToDisplay(stats.GetMoveSpeed()).ToString());

        SetTextColor(statValueATK, Color.white);
        SetTextColor(statValueDEF, Color.white);
        SetTextColor(statValueSPD, Color.white);

        // Masque le texte d'accessibilité terrain
        SetActive(accessibilityText?.gameObject, false);

        // Slots ability et sacrifice
        RefreshAbilitySlots(statsData);
    }

    // Affiche ou masque les slots ability et sacrifice selon les données du SO
    // Les sprites sont fixés dans le prefab Unity — seule la visibilité change
    private void RefreshAbilitySlots(UnitStatsData statsData)
    {
        // Slot ability active — affiché uniquement si hasActiveAbility = true
        bool showAbility = statsData != null && statsData.hasActiveAbility;
        SetActive(abilitySlot?.gameObject, showAbility);

        // Slot sacrifice — toujours affiché pour les unités
        SetActive(sacrificeSlot?.gameObject, true);
    }

    // Mode groupe (2+ unités) : placeholder ×N, nom, tout le reste masqué
    private void RefreshGroup(int count)
    {
        SetActive(budProgressBarFill?.gameObject, false);
        trackedBud = null;

        // Untrack l'unité précédente (pas de HP temps réel en mode groupe)
        UntrackUnit();

        // Portrait : placeholder coloré avec indicateur de groupe
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.color  = new Color(0.45f, 0.35f, 0.15f); // Doré-brun
        }

        SetText(unitNameText, $"{count} unités sélectionnées");

        // Masque HP, stats et accessibilité
        SetActive(hpBarFill?.gameObject, false);
        SetActive(hpText?.gameObject, false);
        SetActive(statValueATK?.gameObject, false);
        SetActive(statValueDEF?.gameObject, false);
        SetActive(statValueSPD?.gameObject, false);
        SetActive(accessibilityText?.gameObject, false);
    }

    // Mode terrain : portrait couleur terrain, nom, modificateurs SPD/DEF si non nuls
    private void RefreshGround(Vector2Int gridPosition)
    {
        SetActive(budProgressBarFill?.gameObject, false);
        trackedBud = null;

        UntrackUnit();

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

        // Masque HP et ATK (pas pertinents pour le terrain)
        SetActive(hpBarFill?.gameObject, false);
        SetActive(hpText?.gameObject, false);
        SetActive(statValueATK?.gameObject, false);

        // SPD — affiche si modificateur != 1.0
        float spdMod = groundType.GetMoveSpeedModifier();
        bool showSPD = !Mathf.Approximately(spdMod, 1f);
        SetActive(statValueSPD?.gameObject, showSPD);
        if (showSPD && statValueSPD != null)
        {
            if (spdMod > 1f)
            {
                statValueSPD.text  = $"SPD +{Mathf.RoundToInt((spdMod - 1f) * 100f)}%";
                statValueSPD.color = ColorStatBuff;
            }
            else
            {
                statValueSPD.text  = $"SPD -{Mathf.RoundToInt((1f - spdMod) * 100f)}%";
                statValueSPD.color = ColorStatDebuff;
            }
        }

        // DEF — affiche si modificateur != 0
        int defMod = groundType.GetDefenseModifier();
        bool showDEF = defMod != 0;
        SetActive(statValueDEF?.gameObject, showDEF);
        if (showDEF && statValueDEF != null)
        {
            if (defMod > 0)
            {
                statValueDEF.text  = $"DEF +{defMod}";
                statValueDEF.color = ColorStatBuff;
            }
            else
            {
                statValueDEF.text  = $"DEF {defMod}";
                statValueDEF.color = ColorStatDebuff;
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

    // Mode bâtiment : portrait, nom, HP, DEF — ATK/SPD masqués
    private void RefreshBuilding(GameObject buildingObject)
    {
        SetActive(budProgressBarFill?.gameObject, false);
        trackedBud = null;

        UntrackUnit();
        UntrackBuilding();

        BuildingStatsComponent stats = buildingObject.GetComponent<BuildingStatsComponent>();
        if (stats == null)
        {
            Debug.LogWarning($"🏗️ UnitPanelUI: Pas de BuildingStatsComponent sur {buildingObject.name}");
            return;
        }

        // Portrait depuis le SO
        BuildingStatsData statsData = stats.GetBaseStats();
        if (portraitImage != null)
        {
            if (statsData != null && statsData.portrait != null)
            {
                portraitImage.sprite = statsData.portrait;
                portraitImage.color  = Color.white;
                TeamComponent team = buildingObject.GetComponent<TeamComponent>();
                if (team != null && portraitImage.material != null)
                    portraitImage.material.SetColor("_TeamColor", team.GetTeamColor());
            }
            else
            {
                portraitImage.sprite = null;
                portraitImage.color  = new Color(0.40f, 0.30f, 0.20f);
            }
        }

        // Nom
        string displayName = (statsData != null && !string.IsNullOrEmpty(statsData.buildingName)) ? statsData.buildingName : buildingObject.name;
        SetText(unitNameText, displayName);

        // Tracking HP temps réel
        TrackBuilding(stats);

        // HP
        SetActive(hpBarFill?.gameObject, true);
        SetActive(hpText?.gameObject, true);
        RefreshBuildingHPDisplay(stats);

        // DEF uniquement
        SetActive(statValueDEF?.gameObject, true);
        SetText(statValueDEF, stats.GetDefense().ToString());
        SetTextColor(statValueDEF, Color.white);

        // Masque ATK, SPD, accessibilité et slots ability
        SetActive(statValueATK?.gameObject, false);
        SetActive(statValueSPD?.gameObject, false);
        SetActive(accessibilityText?.gameObject, false);
        SetActive(abilitySlot?.gameObject, false);
        SetActive(sacrificeSlot?.gameObject, false);
    }

    // Mode obstacle : placeholder brun/gris, nom du GameObject, le reste masqué
    private void RefreshObstacle(GameObject obstacle)
    {
        SetActive(budProgressBarFill?.gameObject, false);
        trackedBud = null;

        UntrackUnit();

        // Portrait : placeholder brun/gris
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.color  = new Color(0.40f, 0.35f, 0.30f); // Brun-gris
        }

        SetText(unitNameText, obstacle != null ? obstacle.name : "Obstacle");

        // Masque tout sauf le nom
        SetActive(hpBarFill?.gameObject, false);
        SetActive(hpText?.gameObject, false);
        SetActive(statValueATK?.gameObject, false);
        SetActive(statValueDEF?.gameObject, false);
        SetActive(statValueSPD?.gameObject, false);
        SetActive(accessibilityText?.gameObject, false);
    }

    // Mode Bud : portrait de l'unité en cours, nom, HP bâtiment, DEF, barre de progression
    private void RefreshBud(GameObject budObject)
    {
        SetActive(budProgressBarFill?.gameObject, false);
        trackedBud = null;

        BudComponent budComponent = budObject.GetComponent<BudComponent>();
        BuildingStatsComponent buildingStats = budObject.GetComponent<BuildingStatsComponent>();

        if (budComponent == null) return;

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

        // HP via tracking bâtiment
        if (buildingStats != null)
        {
            TrackBuilding(buildingStats);
            SetActive(hpBarFill?.gameObject, true);
            SetActive(hpText?.gameObject, true);
            RefreshBuildingHPDisplay(buildingStats);

            // DEF
            SetActive(statValueDEF?.gameObject, true);
            SetText(statValueDEF, buildingStats.GetDefense().ToString());
            SetTextColor(statValueDEF, Color.white);
        }
        else
        {
            SetActive(hpBarFill?.gameObject, false);
            SetActive(hpText?.gameObject, false);
            SetActive(statValueDEF?.gameObject, false);
        }

        // Masque les éléments non pertinents
        SetActive(statValueATK?.gameObject, false);
        SetActive(statValueSPD?.gameObject, false);
        SetActive(abilitySlot?.gameObject, false);
        SetActive(sacrificeSlot?.gameObject, false);
        SetActive(accessibilityText?.gameObject, false);

        // Barre de progression
        SetActive(budProgressBarFill?.gameObject, true);
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
