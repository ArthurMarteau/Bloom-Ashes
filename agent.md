# Agent.md - Contexte Bloom & Ashes

## Vue d'ensemble du projet

**Bloom and Ashes** est un RTS (Real-Time Strategy) 2D old school en temps réel développé avec Unity 6000.0.31f1. Le jeu s'inspire de Blood and Magic (1996) mais modernisé avec un gameplay plus dynamique, un support multijoueur étendu (jusqu'à 6 joueurs simultanés), et des mécaniques plus profondes.

## Game Design - Vision Générale

### Univers et Ambiance
- **Thème** : Medieval-fantasy centré sur la magie végétale et les druides
- **Factions** : 5 factions de druides aux spécialisations distinctes
- **Ressource unique** : "Watersap" collectée par les Rootborn en position statique
- **Style visuel** : Low poly en line art, sprites 2D/3D flat, ambiance old school 8-bit

### Mécaniques de Jeu Core
- **Vue du dessus** avec déplacements case par case en 8 directions
- **Temps réel** : toutes les unités peuvent bouger simultanément
- **Gestion simplifiée** mais tactique avec micro-management d'unités limitées
- **Affrontements rapides** mais stratégiques
- **Système de grille** pour les mouvements, attaques et poursuites

### Système d'Unités et Bâtiments
- **Unité de base** : Rootborn (collecte Watersap, construction, transformation en combat)
- **Point de spawn** : Nexus invulnérable pour créer des Rootborn
- **Bâtiments** : 5 types représentant les factions (construction = sacrifier 4 Rootborn)
- **Progression** : 1 type d'unité de combat au début, déblocage via Watersap

### Les 5 Factions de Druides
1. **Champions Mycéliens** : Champignons, poison, vol, soins
2. **Charmeuses des Jardins Dorés** : Fleurs, contrôle mental, lumière  
3. **Crocs Sauvages** : Plantes dures, désert, passifs défensifs
4. **Calcinés** : Feu et nécromancie, burst et mobilité
5. **Élus des Abysses** : Plantes marines, magie, soins, contrôle

### Modes de Jeu
- **Campagne solo** narrative (factions, trahisons, artefacts, spirale abyssale)
- **Multijoueur** jusqu'à 6 joueurs simultanés
- **Conditions de victoire** : Élimination ennemie, capture de points, résistance à vagues

## Architecture Technique Actuelle

### Structure du Projet Unity
```
Assets/Scripts/
├── Core/           # Systèmes fondamentaux (GridManager, SelectionManager)
├── Units/          # Composants liés aux unités (UnitHaloEffect)
├── Animation/      # Contrôleurs d'animation (vide actuellement)
├── Effect/         # Effets visuels (vide actuellement)
```

### Systèmes Implémentés

#### 1. GridManager.cs (Core)
**Fonction** : Gestionnaire principal du système de grille
- Grille 10x10 par défaut, taille de cellule 1f
- Singleton pattern pour accès global
- Conversion bidirectionnelle monde ↔ grille
- Validation des limites et centrage automatique
- Visualisation Gizmos pour l'édition
- Méthodes de test intégrées

**Points clés** :
- Origine calculée pour centrer la grille sur (0,0)
- Système de coordonnées précis avec Vector2Int/Vector3
- Validation robuste des positions via IsInBounds()

#### 2. SelectionManager.cs (Core)  
**Fonction** : Gestion de la sélection d'unités par clic souris
- Sélection simple et multiple (Shift)
- Filtrage par layers (Units:8, Obstacles:6, Ground:3)
- Priorisation des clics : Unités > Obstacles > Terrain
- Exclusion des clics sur interface utilisateur
- Intégration avec GridManager pour coordonnées

**Points clés** :
- HashSet pour stockage optimisé des unités sélectionnées
- Raycast 2D pour détection précise
- Intégration avec SelectionSquare pour feedback visuel
- Gestion de la sélection additive/soustractive

#### 3. UnitHaloEffect.cs (Units)
**Fonction** : Effet visuel de sélection d'unité
- Utilise Light2D (URP) pour effet halo
- Activation/désactivation programmable
- Feedback visuel immédiat pour le joueur

### Technologies et Dépendances
- **Unity 6000.0.31f1** avec Universal Render Pipeline (URP)
- **Unity Input System** pour gestion des entrées
- **2D Feature Set** avec rendu pixel perfect
- **EventSystem** pour interface utilisateur

## État Actuel du Développement

### Systèmes Fonctionnels ✅
- **Système de grille** : Conversion, validation, visualisation
- **Sélection d'unités** : Clic simple/multiple, feedback visuel
- **Architecture de base** : Singletons, organisation modulaire

### Systèmes Manquants 🚧
- **Système de mouvement** : PathFinding, déplacement case par case
- **Système d'unités** : Rootborn, unités de combat, comportements
- **Système de ressources** : Watersap, collecte, dépenses
- **Système de construction** : Bâtiments, fondations, sacrifices
- **Système de combat** : Attaques, portées, dégâts, mort
- **Intelligence artificielle** : IA des unités, stratégies
- **Interface utilisateur** : HUD, menus, informations joueur
- **Audio** : Musiques 8-bit, effets sonores
- **Animations** : Sprites directionnels, états d'animation

### Architecture Cible Recommandée
```
Assets/Scripts/
├── Core/           # GridManager, GameManager, ResourceManager
├── Units/          # UnitBase, Rootborn, CombatUnit, UnitMovement
├── Buildings/      # BuildingBase, Nexus, FactionBuildings
├── Combat/         # CombatSystem, AttackRange, DamageSystem
├── AI/             # UnitAI, StrategicAI, Behaviors
├── Resources/      # WatersapSystem, CollectionManager
├── UI/             # HUD, Menus, SelectionUI, BuildingUI
├── Animation/      # AnimationControllers, SpriteManagers
├── Audio/          # AudioManager, SoundEffects, Music
├── Input/          # InputManager, CommandSystem
└── Utilities/      # Helpers, Extensions, Constants
```

## Conventions de Développement

### Nommage et Code
- **Variables** en anglais, noms complets sans abréviations
- **Commentaires** en français avec // devant chaque méthode
- **Structure** : Cohérence et clarté prioritaires
- **Éviter** les doublons de code et répétitions

### Bonnes Pratiques Observées
- **Singleton pattern** pour managers globaux
- **Validation robuste** des entrées et limites
- **Séparation des responsabilités** claire
- **Debug.Log** informatifs avec contexte
- **Gizmos** pour visualisation en éditeur
- **[ContextMenu]** pour tests rapides

## Priorités de Développement Suggérées

### Phase 1 : Fondations Gameplay 🎯
1. **Système de mouvement** : Implémentation A* pathfinding
2. **Unité Rootborn** : Classe de base avec mouvement 8-directionnels
3. **Système Watersap** : Collecte, stockage, dépense
4. **Interface basique** : Affichage ressources, sélection

### Phase 2 : Mécaniques RTS 🎮  
1. **Système de construction** : Bâtiments, fondations, placement
2. **Unités de combat** : Classes par faction, compétences basiques
3. **Combat simple** : Attaque, portée, dégâts, mort
4. **IA basique** : Comportements d'unités automatiques

### Phase 3 : Contenu et Polish ✨
1. **5 factions complètes** : Unités uniques, capacités spéciales
2. **Modes de jeu** : Multijoueur, campagne, objectifs variés
3. **Audio et animations** : Ambiance old school complète
4. **Interface avancée** : Menus, options, feedback riche

## Notes pour l'Assistant IA

### Contexte de Collaboration
- **Préférences utilisateur** : Tutoiement, neutralité, analyse objective
- **Format propositions** : Plans clairs avant génération de code
- **Corrections** : Fichier + méthode + ligne précise + commentaire explicatif
- **Suppressions** : Signaler code obsolète lors de remplacement
- **Explications** : Parenthèses pour sigles/abréviations

### Philosophie du Projet
Bloom & Ashes vise à recréer l'esprit des RTS classiques avec une approche moderne : gameplay accessible mais profond, visuels nostalgiques mais nets, mécaniques simplifiées mais stratégiques. L'accent est mis sur la rapidité d'exécution et la clarté tactique plutôt que sur la complexité de gestion.
