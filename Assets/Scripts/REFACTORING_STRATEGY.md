# STRATÉGIE DE REFACTORING - CLEAN ARCHITECTURE

## Vue d'Ensemble

Plan de refactoring progressif du projet Bloom & Ashes vers une architecture clean, production-ready, compatible netcode et performante sur grilles 80x80+. Chaque phase garde le jeu fonctionnel. La réorganisation des dossiers est intégrée dans chaque phase.

### Contraintes structurantes
- **Netcode réseau** : Input découplé, pas de singleton static, logique serveur-ready
- **Grille 80x80+** : PathFinding performant (MinHeap), recherche par Registry (pas Physics2D)
- **25+ prefabs** : 1 par unité par faction, configurés par ScriptableObjects
- **Interfaces à la demande** : créées seulement quand 2ème implémentation concrète arrive
- **Organisation hybride** : couche archi au top (Data/Systems/Components), feature en sous-dossiers

---

## Phase 0 — Corrections Bloquantes

**Objectif** : Rendre le code compilable en build, éliminer les incohérences critiques, et commencer la réorg dossiers.

### 0.1 — Supprimer import Editor dans UnitStats.cs
- Supprimer `using UnityEditor.Experimental.GraphView;` (ligne 1)
- Casse la build standalone

### 0.2 — Pont UnitStats → UnitStatsComponent
- Modifier `UnitStats.Awake()` pour détecter `UnitStatsComponent` et rediriger
- Rediriger `TakeDamage`, `Heal`, `GetCurrentHP`, `GetMaxHP`, `GetHealthPercentage`, `GetAttackDamage`, `GetAttackRange`, `GetDefense`, `GetMoveSpeed`, `GetAttackSpeed`, `IsDead`, `IsAlive`
- Résultat : AttackController continue de fonctionner via UnitStats, mais les dégâts passent par UnitStatsComponent

### 0.3 — Centraliser IsRootborn()
- Ajouter `bool isRootborn` dans `UnitStatsData` (SO)
- Ajouter getter `IsRootborn()` dans `UnitStatsComponent` qui lit le SO
- Remplacer les 3 occurrences de `gameObject.name.Contains("Rootborn")` dans :
  - `AttackController.cs`
  - `MovementController.cs`
  - `AnimatorController.cs`

### 0.4 — Réorg dossiers : déplacer PathFinding
- Créer `Systems/Grid/`
- Déplacer `Core/PathFinding.cs` → `Systems/Grid/PathFinding.cs`
- Créer `Data/Units/` (sous-dossier)
- Déplacer `Data/UnitStatsData.cs` → `Data/Units/UnitStatsData.cs`
- Créer `Shared/`
- Déplacer `Ground/GroundType.cs` → `Shared/GroundType.cs`
- Supprimer dossier `Ground/`

### Validation Phase 0
- [ ] Build standalone compile sans erreur
- [ ] Dégâts passent par UnitStatsComponent (vérifier logs)
- [ ] HealthBar se met à jour quand unité prend des dégâts
- [ ] IsRootborn fonctionne via SO au lieu de string matching
- [ ] PathFinding dans `Systems/Grid/`, GroundType dans `Shared/`
- [ ] Play Mode identique au comportement actuel

---

## Phase 1 — Supprimer UnitStats.cs + Réorg Controllers

**Objectif** : Un seul système de stats, zéro duplication. Les Controllers et composants d'unités sont dans Components/Units/.

### 1.1 — Migrer toutes les références UnitStats → UnitStatsComponent
Fichiers à modifier :
- `AttackController.cs` : remplacer `GetComponent<UnitStats>()` par `GetComponent<UnitStatsComponent>()`
- `AnimatorController.cs` : toutes les vérifications `IsDead()` via UnitStatsComponent
- `HealthBar.cs` : supprimer le fallback UnitStats, garder uniquement UnitStatsComponent

### 1.2 — Supprimer UnitStats.cs
- Supprimer le fichier `Core/UnitStats.cs`
- Supprimer le composant UnitStats de tous les prefabs dans Unity
- Vérifier qu'aucun script ne référence encore UnitStats (compilation = validation)

### 1.3 — Nettoyer HealthBar
- Supprimer le champ `unitStats` (ancien)
- Supprimer le flag `usingNewSystem` et la logique de fallback
- Simplifier : ne garde que `unitStatsComponent`

### 1.4 — Réorg dossiers : regrouper composants d'unités
- Déplacer `Core/Controller/MovementController.cs` → `Components/Units/`
- Déplacer `Core/Controller/AttackController.cs` → `Components/Units/`
- Déplacer `Core/Controller/ChaseController.cs` → `Components/Units/`
- Déplacer `Core/Controller/AnimatorController.cs` → `Components/Units/`
- Déplacer `Core/Team/TeamComponent.cs` → `Components/Units/`
- Déplacer `Units/HealthBar.cs` → `Components/Units/`
- Déplacer `Units/UnitType.cs` → `Components/Units/UnitTypeHelper.cs`
- Créer `Components/Visual/`
- Déplacer `Core/SelectionSquare.cs` → `Components/Visual/`
- Déplacer `Core/ProjectileVisual.cs` → `Components/Visual/`
- Supprimer dossiers `Core/Controller/`, `Core/Team/`, `Units/`

### Validation Phase 1
- [ ] `UnitStats.cs` supprimé du projet
- [ ] Compilation sans erreur
- [ ] Aucune référence à `UnitStats` dans le code (Ctrl+Shift+F)
- [ ] Dégâts, mort, HealthBar fonctionnent
- [ ] Animations Hit/Death fonctionnent
- [ ] Tous les controllers dans `Components/Units/`
- [ ] Visuels dans `Components/Visual/`
- [ ] Dossiers `Core/Controller/`, `Core/Team/`, `Units/` supprimés

---

## Phase 2 — Découplage Input + Réorg Managers

**Objectif** : Séparer capture d'input et logique métier. Core/ disparaît complètement.

### 2.1 — Créer InputManager
- Créer `Components/Managers/InputManager.cs` (MonoBehaviour)
- Capture tous les inputs souris/clavier
- Émet des UnityEvents : `OnLeftClickDown`, `OnRightClickDown`, `OnDragStart`, `OnDragUpdate`, `OnDragEnd`
- Seul script qui lit `Input.GetMouseButton*` dans tout le projet

### 2.2 — Migrer SelectionManager
- Supprimer `HandleSelectionInput()` et lecture Input directe
- S'abonner aux events de InputManager dans `Start()`
- Logique de sélection inchangée, juste la source d'input qui change

### 2.3 — Migrer OrderManager
- Supprimer `HandleOrderInput()` et lecture Input directe
- S'abonner à `OnRightClickDown` de InputManager
- Logique d'ordres inchangée

### 2.4 — Réorg dossiers : déplacer Managers, supprimer Core/
- Déplacer `Core/Managers/GridManager.cs` → `Components/Managers/`
- Déplacer `Core/Managers/SelectionManager.cs` → `Components/Managers/`
- Déplacer `Core/Managers/OrderManager.cs` → `Components/Managers/`
- Supprimer dossier `Core/` entièrement (doit être vide)

### Validation Phase 2
- [ ] Aucun `Input.GetMouseButton` en dehors de InputManager
- [ ] Sélection clic simple / rectangle / Shift fonctionne
- [ ] Ordres mouvement / attaque / chase fonctionnent
- [ ] Dossier `Core/` supprimé
- [ ] Tous les Managers dans `Components/Managers/`
- [ ] Structure projet correspond à "Structure cible" dans claude.md

---

## Phase 3 — Performance Grille 80x80+

**Objectif** : Rendre le PathFinding viable sur grandes grilles et mutualiser le code dupliqué.

### 3.1 — PathFinding : List → MinHeap
- Créer `Systems/Grid/MinHeap.cs` (classe générique ~30 lignes)
- Remplacer `List<PathNode> openList` par `MinHeap<PathNode>` dans PathFinding
- Gain : O(n²) → O(n log n), facteur ~500x sur 80x80

### 3.2 — Extraire offsets partagés
- Créer `Systems/Combat/RangeOffsets.cs` (static class)
- Centraliser `range1Offsets`, `range2Offsets`, `range3Offsets`
- Utilisé par AttackController et ChaseController
- Supprimer les arrays dupliqués dans les deux controllers

### 3.3 — GridManager cleanup
- Ajouter `OnDestroy() { Instance = null; }` (évite références fantômes entre sessions Play Mode)
- Préparer extraction logique pure (Phase 5)

### Validation Phase 3
- [ ] PathFinding fonctionne identiquement (mêmes chemins)
- [ ] Tests PathFinding passent toujours
- [ ] Performance mesurable : profiler sur grille 80x80 avec 30+ unités
- [ ] Offsets dans `Systems/Combat/RangeOffsets.cs`, plus de duplication

---

## Phase 4 — Interfaces (à la demande)

**Objectif** : Créer les interfaces quand un 2ème type d'entité les justifie.

### Déclencheur : arrivée des bâtiments
Quand le système de bâtiments est implémenté (Nexus, bâtiments de faction), on aura 2 implémentations de "chose qui prend des dégâts" → justifie `IDamageable`.

### 4.1 — IDamageable
```csharp
public interface IDamageable
{
    void TakeDamage(int damage, GameObject attacker = null);
    void Heal(int amount);
    int GetCurrentHP();
    int GetMaxHP();
    float GetHealthPercentage();
    bool IsDead();
}
```
- `UnitStatsComponent` implémente IDamageable
- Nouveau `BuildingStatsComponent` implémente IDamageable
- `AttackController` cible `IDamageable` au lieu de `UnitStatsComponent`

### 4.2 — ITeamMember (si bâtiments ont des équipes)
```csharp
public interface ITeamMember
{
    int GetTeamID();
    bool IsEnemy(ITeamMember other);
    bool IsAlly(ITeamMember other);
}
```

### Note
Ne PAS créer `IMovable`, `ISelectable`, `IAttacker` tant qu'il n'y a pas de 2ème implémentation concrète. Éviter l'over-engineering.

### Validation Phase 4
- [ ] AttackController cible IDamageable (unités + bâtiments)
- [ ] Bâtiments prennent des dégâts et meurent
- [ ] Aucune interface inutilisée (chaque interface a 2+ implémentations)

---

## Phase 5 — EntityRegistry + Systems POCO

**Objectif** : Performance recherche d'entités et extraction logique pure pour serveur autoritaire futur.

### 5.1 — EntityRegistry (MonoBehaviour singleton, PAS static)
```
Systems/EntityRegistry.cs
```
- Auto-register/unregister dans Awake/OnDestroy des unités
- Recherche par équipe, par zone, par type
- Remplace `Physics2D.OverlapBoxAll` dans AttackController (auto-attaque)
- MonoBehaviour singleton pour lifecycle Unity propre (compatible netcode)

### 5.2 — CombatSystem (POCO)
```
Systems/Combat/CombatSystem.cs
```
- `IsInRange(attackerPos, targetPos, range)` : calcul portée pur
- `CalculateDamage(baseDamage, defense, modifiers)` : formule dégâts
- `FindBestAttackPosition(targetPos, currentPos, range, grid)` : position optimale
- AttackController délègue les calculs au CombatSystem

### 5.3 — GridSystem (POCO)
```
Systems/Grid/GridSystem.cs
```
- Extraire logique pure de GridManager (WorldToGrid, GridToWorld, IsWalkable, IsInBounds)
- GridManager devient juste un pont Unity (Gizmos, singleton, lifecycle)
- Prépare serveur autoritaire : GridSystem peut tourner côté serveur sans Unity

### Validation Phase 5
- [ ] Plus de `Physics2D.OverlapBoxAll` pour recherche d'unités
- [ ] CombatSystem testable unitairement (pas de dépendance Unity)
- [ ] GridSystem testable unitairement
- [ ] Performance auto-attaque stable avec 50+ unités sur 80x80

---

## Phase 6 — Broker Chain (quand factions arrivent)

**Objectif** : Système de buffs/debuffs extensible pour les capacités des 5 factions.

### Déclencheur : implémentation des premières capacités de faction

### 6.1 — IStatModifier + StatModifierMediator
```
Systems/Modifiers/IStatModifier.cs
Systems/Modifiers/StatModifierMediator.cs
```
- Interface modificateur : `Priority`, `Duration`, `ModifyAttack()`, `ModifyDefense()`, etc.
- Mediator : stack de modificateurs triés par priorité, gestion durée
- Pattern : Stats de base (SO) → Mediator → Modificateurs → Valeur finale

### 6.2 — Intégrer dans UnitStatsComponent
- Getters passent par le Mediator : `GetAttackDamage()` → `mediator.GetModifiedAttack(baseStats.attackDamage)`
- API publique : `AddModifier()`, `RemoveModifier()`
- Update timers dans `Update()` du component

### 6.3 — Exemples concrets
- Poison Mycélien : -2 HP/s pendant 5s
- Bouclier Crocs Sauvages : +10 defense pendant 8s
- Haste Calcinés : +50% moveSpeed pendant 3s

### Validation Phase 6
- [ ] Buffs/debuffs s'appliquent et expirent correctement
- [ ] Stack de modificateurs fonctionne (2+ buffs simultanés)
- [ ] Stats reviennent à la normale après expiration
- [ ] Pas d'impact performance notable

---

## Checklist Globale

| Phase | Statut | Dépendance | Déclencheur | Réorg dossiers |
|---|---|---|---|---|
| Phase 0 | 🔴 À faire | Aucune | Immédiat | PathFinding → Systems/Grid/, GroundType → Shared/ |
| Phase 1 | 🔴 À faire | Phase 0 | Après Phase 0 | Controllers → Components/Units/, visuels → Components/Visual/ |
| Phase 2 | 🔴 À faire | Phase 1 | Après Phase 1 | Managers → Components/Managers/, Core/ supprimé |
| Phase 3 | 🔴 À faire | Aucune (indépendant) | Immédiat ou après Phase 1 | MinHeap → Systems/Grid/, RangeOffsets → Systems/Combat/ |
| Phase 4 | ⏳ En attente | Phase 1 | Quand bâtiments arrivent | — |
| Phase 5 | ⏳ En attente | Phase 3 | Quand contenu gameplay commence | EntityRegistry, CombatSystem, GridSystem dans Systems/ |
| Phase 6 | ⏳ En attente | Phase 5 | Quand factions arrivent | Modifiers dans Systems/Modifiers/ |

### Règles de progression
- **Toujours fonctionnel** : le jeu tourne après chaque sous-étape
- **Compiler = valider** : aucun warning ignoré
- **Tester en Play Mode** après chaque phase
- **Phase 3 peut être parallélisée** avec Phase 1-2 (PathFinding est isolé)
- **Phases 4-5-6 sont déclenchées par le contenu**, pas par le calendrier
- **Déplacer les fichiers dans Unity** (pas dans l'explorateur) pour préserver les .meta

---

## Points d'Attention Netcode

Ces éléments ne sont pas dans les phases actuelles mais doivent être gardés en tête :
- `GridManager.Instance` (singleton static) devra devenir un service injecté ou MonoBehaviour singleton
- `OrderManager` devra valider les ordres côté serveur
- `PathFinding` devra potentiellement tourner côté serveur (GridSystem POCO le prépare)
- Les positions logiques (`logicalPosition` dans MovementController) sont la source de vérité — bon pattern pour netcode
- `EntityRegistry` en MonoBehaviour singleton est compatible avec le lifecycle réseau
