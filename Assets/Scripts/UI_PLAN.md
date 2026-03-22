# Bloom & Ashes — Plan UI

---

## Étape 1b — Slots d'upgrades (bas droite du panneau)

**Contexte :**
Les bâtiments de faction (2x2) permettent aux Rootborn adjacents de se transformer en unités de combat.
T1 toujours disponible. T2/T3/T4 doivent être recherchés (achat unique, immédiat).

**Affichage selon la sélection :**

### Sélection bâtiment (clic sur l'une des 4 cases du 2x2)
- Affiche les slots d'upgrades T2/T3/T4
- Slot état **normal** : upgrade disponible, pas encore recherché
- Slot état **grisé** : upgrade déjà recherché (affiché mais non cliquable)

### Sélection Rootborn adjacent à un bâtiment allié
- Rootborn doit être à portée 1 (range mêlée) d'au moins une case du bâtiment 2x2
- Affiche les slots de transformation disponibles (T1 + ceux déjà recherchés)
- Slot état **normal** : assez de Watersap, transformable
- Slot état **bleuté** : pas assez de Watersap
- Slot état **grisé** : pas encore recherché (non transformable)

**Visuels des slots :**
- Sprite rond identique aux portraits d'unités
- Portrait de l'unité cible dans le slot
- Coût affiché en bas du slot (Watersap)
- 1 à 4 slots selon les upgrades du bâtiment, positionnés en bas droite du panneau

# Bloom & Ashes — Plan UI

## Stack
Unity 6, uGUI (Canvas classique), branché sur SelectionManager existant

---

## Layout général

```
┌─────────────────────────────────────────────────────┐
│                                                     │
│                   JEU                               │
│                                                     │
├──────────────┬──────────────────────┬───────────────┤
│ PANNEAU      │  WATERSAP + ABILITIES│   MINIMAP     │
│ UNITÉ/TERRAIN│                      │   (vide)      │
└──────────────┴──────────────────────┴───────────────┘
```

---

## Étape 1 — Panneau unité (bas gauche)

**Affiché quand une ou plusieurs unités sont sélectionnées.**

### Sélection simple
- Portrait rond de l'unité
- Nom de l'unité
- Barre de vie + valeur HP (ex: 20/25)
- Stats : ATK / DEF / SPD (avec icônes)
- Slot objet : icône ronde/carrée (vide par défaut) + tooltip hover sur l'objet
- Icône Sacrifice : bouton gratuit (0 Watersap), tooltip hover

### Sélection de groupe
- Image de groupe générique (placeholder pour l'instant)
- Pas de stats affichées
- Prévu pour plus tard : unités cliquables dans le groupe pour switcher

### Sélection terrain/obstacle (même panneau, contenu alternatif)
- Portrait du terrain (image)
- Nom du terrain
- Stats disposées pareil que les unités MAIS affichent uniquement les malus/bonus actifs
  - Si aucun effet → rien affiché pour ce stat
  - Si malus → valeur négative en rouge
  - Si bonus → valeur positive en vert
- Pas de barre de vie, pas de slot objet, pas de sacrifice

---

## Étape 2 — Abilities (bas centre, au-dessus du Watersap)

- Rangée horizontale de boutons dynamiques (add/remove selon contexte)
- Chaque bouton : icône ability + coût Watersap affiché dessus
- Icône toggle Auto-cast sur chaque ability (on/off)
- Tooltip au hover : nom + description + effets
- **Système contextuel Rootborn** : quand un Rootborn est adjacent à un bâtiment,
  les abilities de transformation apparaissent dynamiquement dans la rangée

---

## Étape 3 — Ressource Watersap (bas centre, sous les abilities)

- Icône Watersap + compteur numérique
- Positionné juste sous les abilities pour lecture facile coût/disponible

---

## Approche technique UI — Blender + Unity

### Philosophie
Les éléments visuels complexes (cercles, arcs, icônes) sont créés dans Blender comme sprites PNG transparents.
Unity ne gère que les données dynamiques (textes, fillAmount) superposées sur les sprites.
Pas de Layout Groups complexes — positionnement manuel en absolu sur le Canvas.

### Ce que Blender produit
- **Sprite principal** : un seul modèle 3D fixe avec :
  - Le grand cercle portrait
  - L'arc HP en haut (180°)
  - Le coin nom bas gauche (90°)
  - Le coin stats bas droit (90°) divisé en 3 sections avec icônes (épée ATK, bouclier DEF, botte SPD)
  - Les séparateurs entre sections
  - Les éléments du bas : ovale Watersap, petits cercles Sacrifice/Item, ovale Ability
- Rendu en PNG RGBA depuis la Camera_Vue_2D, fond transparent

### Ce que Unity gère
- `Image (fillAmount)` sur l'arc HP → `Fill Method = Radial 360`
- Textes TMP positionnés manuellement aux bons emplacements :
  - HP : "19/20" centré en haut de l'arc
  - Nom unité : coin bas gauche
  - ATK / DEF / SPD : valeurs entières (speed décimal → 1/2/3, bonus/malus ±0.2 → ±1)
- Portrait : Image circulaire centrée dans le cercle
- Pas de Layout Group — tout en positionnement absolu RectTransform

### Conversion vitesse → affichage
- Speed 1.0 → affiché "1"
- Speed 2.0 → affiché "2"
- Speed 3.0 → affiché "3"
- Bonus +0.2 → affiché "+1" (vert)
- Malus -0.2 → affiché "-1" (rouge)

---

## Notes techniques

- Panneau terrain/obstacle réutilise le même prefab que le panneau unité (cohérence visuelle)
- Abilities : liste dynamique (pas hide/show mais add/remove de boutons)
- Tooltip : composant générique réutilisable (unité, objet, ability, terrain)
- Branché sur SelectionManager via UnityEvents (découplé)
- Minimap : emplacement réservé bas droite, non implémenté
