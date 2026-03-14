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

## Notes techniques

- Panneau terrain/obstacle réutilise le même prefab que le panneau unité (cohérence visuelle)
- Abilities : liste dynamique (pas hide/show mais add/remove de boutons)
- Tooltip : composant générique réutilisable (unité, objet, ability, terrain)
- Branché sur SelectionManager via UnityEvents (découplé)
- Minimap : emplacement réservé bas droite, non implémenté
