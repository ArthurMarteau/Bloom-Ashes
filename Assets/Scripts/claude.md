# Agent.md - Contexte Bloom & Ashes

## Vue d'ensemble du projet

**Bloom and Ashes** est un RTS (Real-Time Strategy) 2D old school en temps réel développé avec Unity 6000.0.31f1. Le jeu s'inspire de Blood and Magic (1996) mais modernisé avec un gameplay plus dynamique, un support multijoueur réseau (jusqu'à 6 joueurs simultanés), et des mécaniques plus profondes.

---

## Game Design - Vision Générale

### Univers et Ambiance
- **Thème** : Medieval-fantasy centré sur la magie végétale et les druides
- **Factions** : 5 factions de druides aux spécialisations distinctes
- **Ressource unique** : "Watersap" collectée par les Rootborn en position statique
- **Style visuel** : Low poly en line art, sprites 2D/3D flat, ambiance old school 8-bit

### Mécaniques de Jeu Core
- **Vue du dessus** avec déplacements case par case en 8 directions
- **Temps réel** : toutes les unités peuvent bouger simultanément
- **Grille cible** : 80x80+ cases en production
- **Gestion simplifiée** mais tactique avec micro-management d'unités limitées
- **Affrontements rapides** mais stratégiques

### Système d'Unités et Bâtiments
- **Unité de base** : Rootborn (collecte Watersap, construction, Destroy+Instantiate pour transformation en unité de combat)
- **Point de spawn** : Nexus invulnérable pour créer des Rootborn
- **Bâtiments** : 5 types représentant les factions (construction = sacrifier 4 Rootborn)
- **Progression** : 1 type d'unité de combat au début, déblocage via Watersap
- **Prefabs** : 1 prefab par unité par faction (25+ prefabs)

### Les 5 Factions de Druides
1. **Champions Mycéliens** : Champignons, poison, vol, soins
2. **Charmeuses des Jardins Dorés** : Fleurs, contrôle mental, lumière
3. **Crocs Sauvages** : Plantes dures, désert, passifs défensifs
4. **Calcinés** : Feu et nécromancie, burst et mobilité
5. **Élus des Abysses** : Plantes marines, magie, soins, contrôle

### Modes de Jeu
- **Campagne solo** narrative (factions, trahisons, artefacts, spirale abyssale)
- **Multijoueur réseau** jusqu'à 6 joueurs simultanés (netcode)
- **Conditions de victoire** : Élimination ennemie, capture de points, résistance à vagues

---

## Décisions Techniques Validées

| Sujet | Décision | Impact |
|---|---|---|
| Rootborn → combat | Destroy + Instantiate nouveau prefab | IsRootborn = type fixe (SO), pas état mutable |
| Multijoueur | Netcode réseau (online) | Input découplé de la logique, pas de singletons static |
| Grille production | 80x80+ | PathFinding MinHeap obligatoire, EntityRegistry pour recherche |
| Prefabs factions | 1 prefab par unité par faction | SO pour config stats, pas de prefab générique |
| IdleReversed | Spécifique Rootborn uniquement | Flag `isRootborn` dans SO, pas de généralisation |
| Input System | Ancien système, migration New Input System quand netcode | InputManager isole l'ancien Input, migration = modifier 1 fichier |
| Interfaces | À la demande seulement | Créées quand 2ème implémentation concrète apparaît |
| Organisation dossiers | Hybride (couche archi + sous-dossiers feature) | Data/ Systems/ Components/ au top, features en sous-dossiers |

---

## Architecture Technique - Clean Architecture

### Principes Architecturaux

**Clean Architecture Unity** : Séparation stricte données/logique/présentation
- **Data Layer** : ScriptableObjects (configuration, stats de base)
- **Domain Layer** : POCO (logique métier pure, testable sans Unity)
- **Presentation Layer** : MonoBehaviors (pont Unity, cycle de vie, events)

**Design Patterns** :
- **Data-Driven** : ScriptableObjects pour toute configuration
- **Event-Driven** : Communication découplée (UnityEvents, InputManager events)
- **Registry Pattern** : Auto-enregistrement des entités (MonoBehaviour singleton, pas static)
- **Broker Chain** : Système de modificateurs pour buffs/debuffs (Phase 6)
- **Interfaces à la demande** : Créées seulement quand justifiées par 2+ implémentations

### Règles d'Architecture

1. Si testable sans Unity → System (POCO)
2. Si données configurables → ScriptableObject
3. Si cycle de vie Unity → MonoBehavior Component
4. Jamais de GameObject dans la logique pure
5. Interfaces quand 2ème implémentation concrète arrive (pas préventif)
6. Pas de singleton static (incompatible netcode) → MonoBehaviour singleton ou injection

---

## Structure du Projet

### Structure actuelle
```
Assets/Scripts/
├── Data/
│   └── UnitStatsData.cs            # ✅ Bien placé
├── Systems/
│   └── UnitStatsLogic.cs           # ✅ Bien placé
├── Components/
│   └── Units/
│       └── UnitStatsComponent.cs   # ✅ Bien placé
├── Core/                           # ⚠️ Fourre-tout, sera supprimé
│   ├── Managers/
│   │   ├── GridManager.cs
│   │   ├── SelectionManager.cs
│   │   └── OrderManager.cs
│   ├── Controller/
│   │   ├── MovementController.cs
│   │   ├── AttackController.cs
│   │   ├── ChaseController.cs
│   │   └── AnimatorController.cs
│   ├── Team/
│   │   └── TeamComponent.cs
│   ├── UnitStats.cs                # ⚠️ ANCIEN, sera supprimé
│   ├── PathFinding.cs              # ⚠️ POCO, devrait être dans Systems/
│   ├── SelectionSquare.cs
│   └── ProjectileVisual.cs
├── Units/                          # ⚠️ Sera absorbé dans Components/Units/
│   ├── UnitType.cs
│   └── HealthBar.cs
├── Ground/                         # ⚠️ Sera déplacé dans Shared/
│   └── GroundType.cs
└── Tests/
    └── PathFindingTests.cs
```

### Structure cible (fin Phase 2)
```
Assets/Scripts/
├── Data/                           # ScriptableObjects
│   ├── Units/
│   │   └── UnitStatsData.cs
│   └── Grid/                       (Phase 5)
│       └── GridConfig.cs
│
├── Systems/                        # Logique pure POCO
│   ├── Units/
│   │   └── UnitStatsLogic.cs
│   ├── Grid/
│   │   ├── PathFinding.cs
│   │   ├── GridSystem.cs           (Phase 5)
│   │   └── MinHeap.cs             (Phase 3)
│   ├── Combat/
│   │   ├── CombatSystem.cs         (Phase 5)
│   │   └── RangeOffsets.cs         (Phase 3)
│   └── Modifiers/                  (Phase 6)
│       ├── IStatModifier.cs
│       └── StatModifierMediator.cs
│
├── Components/                     # MonoBehaviors
│   ├── Units/
│   │   ├── UnitStatsComponent.cs
│   │   ├── MovementController.cs
│   │   ├── AttackController.cs
│   │   ├── ChaseController.cs
│   │   ├── AnimatorController.cs
│   │   ├── TeamComponent.cs
│   │   ├── HealthBar.cs
│   │   └── UnitTypeHelper.cs
│   ├── Visual/
│   │   ├── SelectionSquare.cs
│   │   └── ProjectileVisual.cs
│   └── Managers/
│       ├── GridManager.cs
│       ├── SelectionManager.cs
│       ├── OrderManager.cs
│       └── InputManager.cs         (Phase 2)
│
├── Shared/                         # Enums, constantes, extensions
│   └── GroundType.cs
│
└── Tests/
    └── PathFindingTests.cs
```

---

## Systèmes Implémentés

### ✅ Système de Stats (Clean Architecture)
- `UnitStatsData.cs` (SO) → Données de base configurables
- `UnitStatsLogic.cs` (POCO) → Calculs purs, testables
- `UnitStatsComponent.cs` (MB) → Cycle de vie Unity, events
- ⚠️ `UnitStats.cs` (ANCIEN) encore utilisé par AttackController et AnimatorController

### ✅ Système de Grille
- `GridManager.cs` : Grille configurable, conversion monde↔grille, terrain, accessibilité
- ⚠️ Singleton static (incompatible netcode long terme)

### ✅ Système de Mouvement
- `MovementController.cs` : Architecture logique/visuel séparée, queue de chemin, collisions temps réel
- `PathFinding.cs` : A* fonctionnel, static class découplée
- ⚠️ openList en List (O(n²), insuffisant pour 80x80+)

### ✅ Système de Combat
- `AttackController.cs` : Attaque persistante, portée variable, auto-attaque, projectiles
- ⚠️ Offsets hardcodés (range1/2/3) dupliqués avec ChaseController
- ⚠️ Utilise encore UnitStats au lieu de UnitStatsComponent

### ✅ Système de Poursuite
- `ChaseController.cs` : Poursuite persistante, recalcul auto, adjacence

### ✅ Système d'Animation
- `AnimatorController.cs` : 8 directions + miroir, Walk/Attack/Hit/Death, IdleReversed Rootborn
- ⚠️ Utilise encore `GetComponent<UnitStats>()` pour IsDead()
- ⚠️ `IsRootborn()` par string matching (fragile)

### ✅ Système de Sélection & Ordres
- `SelectionManager.cs` + `OrderManager.cs` : Clic/drag/Shift, priorisation, ordres auto
- ⚠️ Input couplé directement dans les Managers

### ✅ Système d'Équipes
- `TeamComponent.cs` : Team ID, IsEnemy/IsAlly, neutre

### ✅ Utilitaires
- `UnitTypeHelper.cs` : Capacités terrain via tags Unity
- `HealthBar.cs` : Rétrocompatible ancien/nouveau système stats
- `SelectionSquare.cs` : Feedback visuel sélection
- `ProjectileVisual.cs` : Interpolation linéaire projectiles

---

## Technologies et Dépendances

- **Unity 6000.0.31f1** avec Universal Render Pipeline (URP)
- **Unity Input System** (package installé, ancien système utilisé dans le code)
- **2D Feature Set** avec rendu pixel perfect
- **UnityEvents** pour communication découplée

---

## Conventions de Développement

### Nommage et Code
- **Variables** en anglais, noms complets sans abréviations
- **Commentaires** en français avec `//` devant chaque méthode
- **Éviter** les doublons de code et répétitions
- **Debug.Log** informatifs avec contexte et emojis
- **[ContextMenu]** pour tests rapides en éditeur

### Workflow
- **Proposer plan avant code** : Valider l'approche avant de coder
- **Corrections précises** : Fichier + méthode + ligne + commentaire explicatif
- **Signaler code obsolète** lors de remplacement
- **Migration progressive** : Jeu fonctionnel à chaque étape

---

## Priorités de Développement

Voir **REFACTORING_STRATEGY.md** pour le plan détaillé phase par phase.

### Résumé des phases
- **Phase 0** : Corrections bloquantes + début réorg dossiers
- **Phase 1** : Supprimer UnitStats.cs + déplacer Controllers/Components
- **Phase 2** : Découplage Input + déplacer Managers → Core/ supprimé
- **Phase 3** : Performance grille 80x80+ (MinHeap, offsets partagés)
- **Phase 4** : Interfaces (à la demande, quand bâtiments arrivent)
- **Phase 5** : EntityRegistry + Systems POCO
- **Phase 6** : Broker Chain (quand factions arrivent)

---

## Fichiers de Référence

- **claude.md** : Ce fichier (contexte projet pour IA)
- **REFACTORING_STRATEGY.md** : Plan de refactoring détaillé phase par phase
