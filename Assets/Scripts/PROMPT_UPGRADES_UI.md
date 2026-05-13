# Système d'upgrades — Slots UI bas droite du panneau

## Contexte
Lis `claude.md`, `GDD.md` et `UI_PLAN.md` (section Étape 1b).

Architecture Clean Architecture Unity — respecte strictement la séparation Data/Systems/Components.

---

## Ce qu'il faut créer

### 1. Modifier `Data/Units/UnitStatsData.cs`
Ajouter deux champs :
```csharp
[Header("Tier")]
[Tooltip("T1 = toujours disponible, T2/T3/T4 = nécessite recherche")]
public int tier = 1;

[Tooltip("Coût en Watersap pour former cette unité")]
public int trainCost = 20;

    [Tooltip("Rôle affiché dans le slot d'upgrade (ex: Melee / Support)")]
    public string role = "";
```

### 2. Modifier `Data/Buildings/BuildingStatsData.cs`
Ajouter la liste d'unités disponibles :
```csharp
[Header("Unités disponibles")]
[Tooltip("Toutes les unités que ce bâtiment peut produire (T1 inclus, T2/T3/T4 à rechercher)")]
public List<UnitStatsData> availableUnits = new List<UnitStatsData>();
```

### 3. Modifier `Components/Buildings/BuildingStatsComponent.cs`
Ajouter la gestion runtime des recherches :
- Liste privée `researchedUnits` (initialisée vide dans `Awake`, NON dans le SO)
- `UnityEvent OnResearchChanged` déclenché après chaque recherche
- `ResearchUnit(UnitStatsData unit)` : ajoute si T2/T3/T4 et pas déjà recherchée → déclenche event
- `IsResearched(UnitStatsData unit)` → bool
- `GetAvailableUnits()` → retourne `baseStats.availableUnits`
- `GetUnitsAccessibleToRootborn()` → retourne T1 + déjà recherchés seulement

### 4. Créer `Systems/Buildings/BuildingProximitySystem.cs` (POCO)
Logique pure sans MonoBehaviour :
```csharp
// Retourne le BuildingStatsComponent allié le plus proche à portée mêlée (distance 1)
// Un bâtiment 2x2 occupe 4 cases → vérifie les 8 voisins du Rootborn
// Si une case voisine est occupée par le bâtiment → retourne ce bâtiment
public static BuildingStatsComponent FindAdjacentAlliedBuilding(
    Vector2Int rootbornPosition,
    List<BuildingStatsComponent> alliedBuildings,
    GridManager gridManager)
```
Pour savoir quelles cases un bâtiment 2x2 occupe : utilise la position grille du bâtiment + offset sur 2x2.

### 5. Créer `Components/UI/UpgradeSlotUI.cs`
MonoBehaviour sur chaque slot :
```csharp
public enum SlotMode { Normal, Researched, InsufficientWatersap, NotResearched }
```
- `[SerializeField] Image portraitImage`
- `[SerializeField] TextMeshProUGUI costText`
- `[SerializeField] TextMeshProUGUI descriptionText` — description courte de l'unité (nom + rôle)
- `[SerializeField] Image overlayImage` — overlay coloré selon état
- `Setup(UnitStatsData unit, SlotMode mode)` : configure visuels + interactivité
  - Normal : pas d'overlay, cliquable — Button.interactable = true
  - Researched : overlay gris `(0.3, 0.3, 0.3, 0.6)`, Button.interactable = false
  - InsufficientWatersap : overlay bleuté `(0.1, 0.3, 0.8, 0.5)`, Button.interactable = false
  - NotResearched : overlay gris foncé `(0.1, 0.1, 0.1, 0.7)`, Button.interactable = false
- Effets visuels du Button (à configurer dans l'Inspector, Transition = Color Tint) :
  - Normal Color : `(1, 1, 1, 1)`
  - Highlighted Color : `(1.2, 1.2, 1.2, 1)` — éclaircissement au hover
  - Pressed Color : `(0.8, 0.8, 0.8, 1)` — assombrissement au clic
  - Disabled Color : `(0.5, 0.5, 0.5, 0.6)` — grisé automatique si interactable = false
  - Fade Duration : `0.1`
  - Target Graphic : l'Image Portrait
- `OnClick` : callback `System.Action` assigné par UnitPanelUI

### 6. Modifier `Components/UI/UnitPanelUI.cs`
Nouveaux SerializeFields :
```csharp
[Header("Slots upgrades")]
[SerializeField] private Transform upgradeSlotsContainer;
[SerializeField] private GameObject upgradeSlotPrefab;
```

Nouveaux comportements :
- `HandleUnitsSelected` : si 1 unité = Rootborn (`UnitStatsData.isRootborn`) → appelle `BuildingProximitySystem.FindAdjacentAlliedBuilding` → si bâtiment trouvé → `RefreshUpgradeSlotsRootborn(building)`
- `HandleObstacleSelected` : si obstacle a `BuildingStatsComponent` → `RefreshUpgradeSlotsBuilding(building)`
- `HandleSelectionCleared` : vide et masque `upgradeSlotsContainer`
- `RefreshUpgradeSlotsBuilding(BuildingStatsComponent building)` :
  - Affiche T2/T3/T4 uniquement
  - Recherchés → SlotMode.Researched, non recherchés → SlotMode.Normal
  - Clic sur Normal → `building.ResearchUnit(unit)`
- `RefreshUpgradeSlotsRootborn(BuildingStatsComponent building)` :
  - Affiche T1 + recherchés uniquement (pas les non-recherchés)
  - Pas assez Watersap → SlotMode.InsufficientWatersap (TODO: brancher WatersapManager)
  - Pour l'instant, tous affichés en SlotMode.Normal (Watersap non implémenté)
  - Non recherchés → SlotMode.NotResearched (grisés)
- S'abonne à `building.OnResearchChanged` pour refresh auto, désabonne dans cleanup

---

## Hiérarchie prefab `UpgradeSlot`
```
UpgradeSlot (UpgradeSlotUI.cs + Button)
├── Portrait (Image — portrait unité)
├── CostText (TextMeshProUGUI — coût ex: "25")
├── DescriptionText (TextMeshProUGUI — nom + rôle ex: "Lionsmane Priest — Support")
└── Overlay (Image — couleur état, raycast target OFF)
```

## Hiérarchie dans le panneau
```
PanelRoot
└── UpgradeSlotsContainer (HorizontalLayoutGroup, ancré bas droite, SetActive selon contexte)
    └── [slots instanciés dynamiquement]
```

---

## Règles importantes
- `BuildingProximitySystem` = POCO dans `Systems/Buildings/` — aucun MonoBehaviour, aucun GameObject
- `researchedUnits` = état runtime uniquement, jamais persisté dans le SO
- T1 toujours accessible au Rootborn (pas de recherche)
- Slots non cliquables = Button.interactable = false
- Container masqué (`SetActive(false)`) si aucun contexte upgrade
- Toujours null-check (bâtiment détruit, Rootborn mort, etc.)
- Commentaires en français avec `//` devant chaque méthode
- Pour récupérer les bâtiments alliés dans la scène : `Object.FindObjectsByType<BuildingStatsComponent>(FindObjectsSortMode.None)`
