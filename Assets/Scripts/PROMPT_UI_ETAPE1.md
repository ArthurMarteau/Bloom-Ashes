# UI Panneau Unité — Étape 1

## Contexte

Lis `claude.md`, `GDD.md` et `UI_PLAN.md`.

Créer le panneau d'information bas gauche en **uGUI (Canvas classique)**, branché sur `SelectionManager`. Style : fond sombre semi-transparent, coins arrondis, cohérent avec un RTS 8-bit low poly.

---

## Ce qu'il faut créer

### Scripts à créer

**`Components/UI/UnitPanelUI.cs`** — MonoBehaviour principal du panneau
**`Components/UI/TooltipUI.cs`** — Tooltip générique réutilisable (hover objet + sacrifice)

### Structure Canvas (créée par code dans Awake ou via prefab)

```
Canvas (Screen Space - Overlay)
└── PanelRoot (bas gauche, ancré BottomLeft)
    ├── Portrait (Image ronde, mask circulaire)
    ├── InfoBlock (layout vertical à droite du portrait)
    │   ├── UnitName (TextMeshPro)
    │   ├── HPBar (Slider ou Image fill)
    │   ├── HPText (TextMeshPro "20 / 25")
    │   ├── StatsBlock (layout vertical)
    │   │   ├── StatRow_ATK (icône + TextMeshPro)
    │   │   ├── StatRow_DEF (icône + TextMeshPro)
    │   │   └── StatRow_SPD (icône + TextMeshPro)
    │   └── BottomRow (layout horizontal)
    │       ├── ItemSlot (Image ronde, vide par défaut, hover tooltip)
    │       └── SacrificeButton (Image icône, cliquable, hover tooltip)
```

---

## Comportement UnitPanelUI

### Affichage selon la sélection (branché sur SelectionManager)

```csharp
// Dans Start(), s'abonner aux events via polling sur SelectionManager
// Vérifier chaque frame si la sélection a changé (ou exposer un event dans SelectionManager)
```

**Sélection simple (1 unité)** :
- Affiche portrait, nom, HP, stats depuis `UnitStatsComponent` et `UnitStatsData`
- Portrait : chercher sprite nommé `{UnitName}_Portrait` dans Resources, sinon placeholder coloré
- HP : lire `unitStats.GetCurrentHP()` et `unitStats.GetMaxHP()`
- Couleur barre HP : verte > 75%, jaune > 50%, orange > 25%, rouge ≤ 25%
- ATK : `unitStats.GetAttackDamage()`
- DEF : `unitStats.GetDefense()`
- SPD : `unitStats.GetMoveSpeed()`
- ItemSlot : vide pour l'instant (système item non implémenté)
- SacrificeButton : toujours visible, tooltip "Sacrifice — Récupère 20 Watersap"

**Sélection de groupe (2+ unités)** :
- Portrait remplacé par image générique "groupe" (placeholder coloré avec icône ×N)
- Nom : "X unités sélectionnées"
- HP/Stats : masqués
- ItemSlot + SacrificeButton : masqués

**Sélection terrain (ground)** :
- Portrait : image placeholder couleur selon `groundType.GetDebugColor()`
- Nom : `groundType.GetDisplayName()`
- Stats : affiche uniquement les effets actifs via les extensions de `GroundType`
  - Lire depuis `GridManager.Instance.GetGroundTypeAt(position)`
  - SPD : afficher si `GetMoveSpeedModifier() != 1.0f`
    - > 1.0 → vert ex: "SPD +50%" (Path = 1.5)
    - < 1.0 → rouge ex: "SPD -50%" (Brambles = 0.5)
  - DEF : afficher si `GetDefenseModifier() != 0`
    - > 0 → vert ex: "DEF +2" (Bush)
    - < 0 → rouge ex: "DEF -1" (Scorched)
  - Si aucun effet sur un stat → ligne masquée
  - Ligne supplémentaire : `GetAccessibleUnits()` en italique gris
- ItemSlot + SacrificeButton : masqués

**Sélection obstacle** :
- Portrait : image placeholder brun/gris
- Nom : nom du GameObject
- HP/Stats : masqués
- ItemSlot + SacrificeButton : masqués

**Aucune sélection** :
- Panneau entier masqué (`SetActive(false)`)

---

## Branchement SelectionManager

`SelectionManager` expose déjà :
- `GetSelectedUnits()` → `List<GameObject>`
- `GetSelectedUnitCount()` → `int`
- `HasUnitsSelected()` / `HasGroundSelected()` / `HasObstacleSelected()`
- `GetSelectedGroundPosition()` → `Vector2Int`
- `GetSelectedObstacle()` → `GameObject`

**Ajouter dans SelectionManager** les UnityEvents suivants pour notifier l'UI :
```csharp
public UnityEvent<List<GameObject>> OnUnitsSelected    // unités sélectionnées
public UnityEvent<Vector2Int>       OnGroundSelected   // terrain sélectionné
public UnityEvent<GameObject>       OnObstacleSelected // obstacle sélectionné
public UnityEvent                   OnSelectionCleared // sélection effacée
```
Les invoquer aux bons endroits dans `SelectUnit`, `SelectGround`, `SelectObstacle`, `ClearAllSelection`.

---

## TooltipUI

Composant générique affiché au hover sur ItemSlot et SacrificeButton :
- Petit panneau flottant suivant le curseur (offset fixe)
- Titre (TextMeshPro bold)
- Description (TextMeshPro normal)
- Méthodes : `Show(string title, string description)` / `Hide()`
- Singleton accessible via `TooltipUI.Instance`

Textes par défaut :
- ItemSlot vide : "Aucun objet équipé"
- SacrificeButton : "Sacrifice", "Détruire cette unité pour récupérer 20 Watersap."

---

## Style visuel

```
Fond panneau    : noir #1A1A1A, alpha 200/255, border-radius 12px
Fond portrait   : #2A2A2A, cercle avec mask
Barre HP        : fond #3A3A3A, fill vert/jaune/orange/rouge selon %
Texte nom       : blanc #FFFFFF, taille 14, bold
Texte stats     : gris clair #CCCCCC, taille 11
Icônes stats    : carrés 16×16 placeholder (ATK=rouge, DEF=bleu, SPD=vert)
SacrificeButton : icône placeholder jaune/dorée, 32×32
ItemSlot        : cercle 32×32, fond #2A2A2A, bordure #555555
```

---

## Organisation dossiers

```
Assets/Scripts/Components/UI/UnitPanelUI.cs
Assets/Scripts/Components/UI/TooltipUI.cs
Assets/Prefabs/UI/UnitPanel.prefab
Assets/Prefabs/UI/Tooltip.prefab
```

---

## Règles

- Commentaires en français avec `//`
- `Debug.Log` avec emojis pour les events UI
- Pas de `FindObjectOfType` dans Update — cacher les références dans Awake/Start
- Le panneau se met à jour via les events `SelectionManager` (pas de polling dans Update)
- HP et stats relus en temps réel via `UnitStatsComponent` getters existants
- Utiliser **TextMeshPro** pour tous les textes
- Tester : sélectionner unité → stats correctes, HP diminue quand unité prend des dégâts, désélectionner → panneau masqué
