# PROMPTS REFACTORING — BLOOM & ASHES
# À utiliser étape par étape dans Claude Code
# Teste en Play Mode entre chaque étape avant de passer à la suivante
# Le claude.md et REFACTORING_STRATEGY.md dans le projet donnent le contexte

---

## ═══════════════════════════════════════
## PHASE 0 — CORRECTIONS BLOQUANTES
## ═══════════════════════════════════════

### Étape 0.1 — Supprimer import Editor

```
Lis le fichier REFACTORING_STRATEGY.md pour le contexte du plan de refactoring.
Phase 0, étape 0.1 : dans Core/UnitStats.cs, supprime le `using UnityEditor.Experimental.GraphView;` en ligne 1. Cet import casse la build standalone. Ne touche à rien d'autre dans le fichier.
```

🧪 **Test** : Vérifie que le projet compile sans erreur.

---

### Étape 0.2 — Pont UnitStats → UnitStatsComponent

```
Phase 0, étape 0.2 : implémente un pont de redirection dans UnitStats.cs vers UnitStatsComponent.

Dans Awake(), détecte si un UnitStatsComponent existe sur le même GameObject. Si oui, stocke la référence et redirige TOUS les appels publics vers lui : TakeDamage, Heal, GetCurrentHP, GetMaxHP, GetHealthPercentage, GetAttackDamage, GetAttackRange, GetDefense, GetMoveSpeed, GetAttackSpeed, IsDead, IsAlive.

Si UnitStatsComponent est détecté, UnitStats ne doit plus gérer ses propres HP ni sa propre logique de mort — tout passe par UnitStatsComponent. Log un message "[PONT] UnitStats redirige vers UnitStatsComponent" dans Awake pour confirmer.

Si UnitStatsComponent n'est PAS détecté, le comportement actuel est conservé (rétrocompatibilité).

Conserve le reste du code intact, c'est temporaire avant suppression en Phase 1.
```

🧪 **Test** : Lance le Play Mode, fais attaquer une unité. Vérifie dans les logs :
- "[PONT] UnitStats redirige vers UnitStatsComponent" apparaît
- Les dégâts passent par UnitStatsComponent (logs "UnitStatsComponent ... Dégâts reçus")
- La HealthBar se met à jour correctement
- La mort fonctionne (animation Death, destruction après 10s)

---

### Étape 0.3 — Centraliser IsRootborn

```
Phase 0, étape 0.3 : centralise la détection IsRootborn via le ScriptableObject au lieu du string matching.

1. Dans Data/UnitStatsData.cs (SO), ajoute un champ :
   [Header("Unit Type")]
   [Tooltip("Cocher pour les unités Rootborn (animation IdleReversed)")]
   public bool isRootborn = false;

2. Dans Components/Units/UnitStatsComponent.cs, ajoute un getter public :
   public bool IsRootborn() qui lit baseStats.isRootborn

3. Dans AttackController.cs, MovementController.cs, et AnimatorController.cs :
   - Remplace chaque `gameObject.name.Contains("Rootborn")` par un appel à UnitStatsComponent.IsRootborn()
   - Cache la référence UnitStatsComponent dans Awake() si pas déjà fait
   - Supprime les méthodes privées `IsRootborn()` dans chaque fichier

Note : AttackController utilise encore UnitStats (pas UnitStatsComponent) à cause du pont Phase 0.2. Pour IsRootborn, utilise GetComponent<UnitStatsComponent>() directement car le pont ne couvre pas IsRootborn.
```

🧪 **Test** : 
- Vérifie que les Rootborn ont bien `isRootborn = true` coché dans leur SO dans l'inspecteur Unity (pense à le cocher manuellement !)
- Lance le Play Mode, sélectionne un Rootborn et donne un ordre de mouvement → IdleReversed doit jouer
- Fais attaquer un Rootborn → IdleReversed pré-attaque doit jouer
- Vérifie qu'une unité non-Rootborn ne joue PAS IdleReversed

---

### Étape 0.4 — Réorg dossiers (1/3)

```
Phase 0, étape 0.4 : première vague de réorganisation des dossiers.

IMPORTANT : déplace les fichiers DANS UNITY (Project window, drag & drop) pour préserver les .meta. Ne le fais PAS dans l'explorateur Windows. Je te donne juste la liste des déplacements à faire manuellement dans Unity :

1. Créer le dossier Systems/Grid/
2. Déplacer Core/PathFinding.cs → Systems/Grid/PathFinding.cs
3. Créer le dossier Data/Units/
4. Déplacer Data/UnitStatsData.cs → Data/Units/UnitStatsData.cs
5. Créer le dossier Shared/
6. Déplacer Ground/GroundType.cs → Shared/GroundType.cs
7. Supprimer le dossier Ground/ (vide)
8. Créer le dossier Systems/Units/
9. Déplacer Systems/UnitStatsLogic.cs → Systems/Units/UnitStatsLogic.cs

Après les déplacements, vérifie la compilation. Unity met à jour les références automatiquement via les .meta.
```

🧪 **Test** : Compile. Play Mode identique. Aucune référence cassée.

---

## ═══════════════════════════════════════
## PHASE 1 — SUPPRIMER UNITSTATS.CS
## ═══════════════════════════════════════

### Étape 1.1 — Migrer AttackController

```
Phase 1, étape 1.1 : migre AttackController.cs pour utiliser UnitStatsComponent au lieu de UnitStats.

Remplace :
- Le champ `private UnitStats unitStats` par `private UnitStatsComponent unitStatsComponent`
- Le `GetComponent<UnitStats>()` dans Awake() par `GetComponent<UnitStatsComponent>()`
- TOUTES les occurrences de `unitStats.` par `unitStatsComponent.` dans le fichier
- Les `GetComponent<UnitStats>()` sur les cibles (targetStats) par `GetComponent<UnitStatsComponent>()`
- Le type des variables locales `UnitStats targetStats` par `UnitStatsComponent targetStats`

Dans FindNearestEnemyInRange(), remplace aussi le `GetComponent<UnitStats>()` qui détecte les unités ennemies.

Vérifie que chaque getter appelé (GetAttackDamage, GetAttackRange, GetAttackSpeed, IsDead, etc.) existe bien dans UnitStatsComponent — ils existent tous.
```

🧪 **Test** : Play Mode → attaque une unité. Dégâts, mort, auto-attaque, projectiles fonctionnent.

---

### Étape 1.2 — Migrer AnimatorController

```
Phase 1, étape 1.2 : migre AnimatorController.cs pour utiliser UnitStatsComponent au lieu de UnitStats.

Remplace TOUS les `GetComponent<UnitStats>()` par `GetComponent<UnitStatsComponent>()`. Il y en a dans :
- PlayWalkAnimation
- PlayAttackAnimation
- PlayIdleAttackAnimation
- PlayIdleAnimation
- PlayRootbornIdleReversed
- PlayHitAnimation

Idéalement, cache la référence UnitStatsComponent dans Awake() et utilise-la partout au lieu de GetComponent à chaque appel.
```

🧪 **Test** : Play Mode → vérifie animations Walk, Attack, Hit, Death, IdleReversed, Idle.

---

### Étape 1.3 — Nettoyer HealthBar

```
Phase 1, étape 1.3 : simplifie HealthBar.cs en supprimant le fallback vers l'ancien système.

Supprime :
- Le champ `[SerializeField] private UnitStats unitStats`
- Le flag `private bool usingNewSystem`
- Toute la branche else dans Awake() qui cherche UnitStats
- La condition `usingNewSystem` dans GetHealthPercentage()
- La condition `usingNewSystem` dans OnDestroy()

Garde uniquement UnitStatsComponent comme source de données. Le code doit être plus simple et direct.
```

🧪 **Test** : Play Mode → HealthBar s'affiche, change de couleur, se cache à la mort.

---

### Étape 1.4 — Supprimer UnitStats.cs

```
Phase 1, étape 1.4 : supprime définitivement Core/UnitStats.cs.

1. Supprime le fichier Core/UnitStats.cs
2. Compile — s'il reste des erreurs, c'est qu'une référence a été oubliée. Corrige-les.
3. Fais un Ctrl+Shift+F dans tout le projet pour "UnitStats" (sans "Component") pour vérifier qu'il n'y a plus aucune référence.

IMPORTANT : après suppression du fichier, va dans Unity et retire le composant UnitStats de tous les prefabs d'unités (il sera marqué "Missing Script" ou restera comme composant orphelin). Vérifie chaque prefab.
```

🧪 **Test** : Compilation propre. Play Mode complet. Aucun warning "Missing Script" dans la console.

---

### Étape 1.5 — Réorg dossiers (2/3)

```
Phase 1, étape 1.5 : deuxième vague de réorganisation des dossiers.

Déplacements à faire DANS UNITY (Project window) :

1. Déplacer Core/Controller/MovementController.cs → Components/Units/
2. Déplacer Core/Controller/AttackController.cs → Components/Units/
3. Déplacer Core/Controller/ChaseController.cs → Components/Units/
4. Déplacer Core/Controller/AnimatorController.cs → Components/Units/
5. Supprimer le dossier Core/Controller/ (vide)
6. Déplacer Core/Team/TeamComponent.cs → Components/Units/
7. Supprimer le dossier Core/Team/ (vide)
8. Déplacer Units/HealthBar.cs → Components/Units/
9. Déplacer Units/UnitType.cs → Components/Units/
10. Supprimer le dossier Units/ (vide)
11. Créer le dossier Components/Visual/
12. Déplacer Core/SelectionSquare.cs → Components/Visual/
13. Déplacer Core/ProjectileVisual.cs → Components/Visual/

Après les déplacements, vérifie la compilation.
```

🧪 **Test** : Compile. Play Mode identique.

---

## ═══════════════════════════════════════
## PHASE 2 — DÉCOUPLAGE INPUT
## ═══════════════════════════════════════

### Étape 2.1 — Créer InputManager

```
Phase 2, étape 2.1 : crée un InputManager qui capture tous les inputs et les émet comme events.

Crée Components/Managers/InputManager.cs (MonoBehaviour) avec :
- UnityEvent<Vector3> OnLeftClickDown (position souris en monde)
- UnityEvent<Vector3> OnRightClickDown (position souris en monde)
- UnityEvent<Vector3> OnDragStart (position souris écran)
- UnityEvent<Vector3> OnDragUpdate (position souris écran)
- UnityEvent OnDragEnd
- UnityEvent<Vector3> OnLeftClickUp (position souris écran)
- bool IsShiftHeld (property publique)

L'InputManager lit Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0), Input.GetMouseButtonDown(1) dans Update() et invoque les events correspondants.

Utilise le pattern MonoBehaviour singleton (pas static). Convertis la position souris en position monde via Camera.main pour les events qui en ont besoin.

Ne modifie PAS SelectionManager ni OrderManager pour l'instant.
```

🧪 **Test** : Ajoute l'InputManager à un GameObject dans la scène. Vérifie dans les logs qu'il capture bien les clics.

---

### Étape 2.2 — Migrer SelectionManager

```
Phase 2, étape 2.2 : migre SelectionManager pour écouter les events de InputManager au lieu de lire Input directement.

- Supprime HandleSelectionInput() et tout le code de lecture Input dans Update()
- Dans Start(), trouve l'InputManager et s'abonne à OnLeftClickDown, OnDragStart, OnDragUpdate, OnLeftClickUp
- Utilise InputManager.IsShiftHeld au lieu de Input.GetKey(KeyCode.LeftShift)
- La logique de sélection (HandleSimpleClick, UpdateRectangleSelection, CompleteRectangleSelection, etc.) reste identique, seule la source d'input change
- Update() ne doit plus contenir de lecture Input

Assure-toi que le rectangle de sélection fonctionne toujours (le drag utilise les positions écran).
```

🧪 **Test** : Clic simple sur unité, rectangle de sélection, Shift+clic, clic sur terrain → tout fonctionne.

---

### Étape 2.3 — Migrer OrderManager

```
Phase 2, étape 2.3 : migre OrderManager pour écouter les events de InputManager au lieu de lire Input directement.

- Supprime HandleOrderInput() et la lecture Input dans Update()
- Dans Start(), s'abonne à InputManager.OnRightClickDown
- La méthode HandleMovementOrder() est appelée par l'event au lieu de Update()
- Update() peut être supprimé ou vidé

La logique d'ordres (mouvement, attaque, chase) reste identique.
```

🧪 **Test** : Clic droit sur terrain = mouvement, clic droit sur ennemi = attaque, clic droit sur allié = chase.

---

### Étape 2.4 — Vérification et réorg finale

```
Phase 2, étape 2.4 : vérifie qu'aucun Input.GetMouseButton* n'existe en dehors de InputManager.

Fais un Ctrl+Shift+F pour "Input.GetMouseButton" et "Input.GetKey" dans tout le projet. Seul InputManager.cs doit les contenir.

Ensuite, réorg dossiers DANS UNITY :
1. Déplacer Core/Managers/GridManager.cs → Components/Managers/
2. Déplacer Core/Managers/SelectionManager.cs → Components/Managers/
3. Déplacer Core/Managers/OrderManager.cs → Components/Managers/
4. Supprimer le dossier Core/Managers/ (vide)
5. Supprimer le dossier Core/ (doit être vide)

La structure du projet doit correspondre exactement à la "Structure cible" dans claude.md.
```

🧪 **Test** : Compile. Play Mode complet. Dossier Core/ n'existe plus.

---

## ═══════════════════════════════════════
## PHASE 3 — PERFORMANCE 80x80+
## ═══════════════════════════════════════

### Étape 3.1 — Créer MinHeap

```
Phase 3, étape 3.1 : crée une classe MinHeap générique pour optimiser le PathFinding.

Crée Systems/Grid/MinHeap.cs : une classe générique MinHeap<T> où T : IComparable<T>.
Méthodes : Insert(T item), ExtractMin(), Peek(), Count, Contains(T item).

Implémente avec un tableau dynamique (List<T>) et les opérations classiques de heap : BubbleUp et BubbleDown. La classe doit être testable sans Unity (pas de MonoBehaviour, pas de Mathf).
```

🧪 **Test** : Ajoute un test unitaire basique dans Tests/ pour valider Insert/ExtractMin.

---

### Étape 3.2 — Intégrer MinHeap dans PathFinding

```
Phase 3, étape 3.2 : remplace la List<PathNode> openList par un MinHeap dans PathFinding.cs.

Dans Systems/Grid/PathFinding.cs :
- Fais implémenter IComparable<PathNode> à PathNode (compare par fCost, puis hCost en cas d'égalité)
- Remplace `List<PathNode> openList = new List<PathNode>()` par `MinHeap<PathNode> openList = new MinHeap<PathNode>()`
- Remplace `GetLowestFCostNode(openList)` + `openList.Remove(currentNode)` par `openList.ExtractMin()`
- Adapte la logique de mise à jour d'un nœud existant (si tentativeGCost < existingNode.gCost)
- Supprime la méthode GetLowestFCostNode (plus nécessaire)

Le reste de l'algorithme A* ne change pas.
```

🧪 **Test** : Tests PathFinding existants passent. Vérifie en Play Mode que les chemins sont identiques. Profile sur grille 80x80 si possible.

---

### Étape 3.3 — Extraire RangeOffsets

```
Phase 3, étape 3.3 : mutualise les offsets de portée entre AttackController et ChaseController.

Crée Systems/Combat/RangeOffsets.cs (static class) contenant :
- public static Vector2Int[] GetOffsets(int range) qui retourne le bon array selon la portée
- Les arrays range1Offsets, range2Offsets, range3Offsets (déplacés depuis AttackController)

Dans AttackController.cs :
- Supprime les arrays range1Offsets, range2Offsets, range3Offsets
- Dans FindPositionInRange(), appelle RangeOffsets.GetOffsets(attackRange) au lieu du switch

Dans ChaseController.cs :
- Supprime le array adjacentOffsets dans FindClosestAccessibleCell()
- Appelle RangeOffsets.GetOffsets(1) pour les positions adjacentes (range 1 = adjacence)
```

🧪 **Test** : Play Mode → attaque mêlée, attaque distance, chase fonctionnent identiquement.

---

### Étape 3.4 — GridManager cleanup

```
Phase 3, étape 3.4 : ajoute le cleanup du singleton GridManager.

Dans Components/Managers/GridManager.cs, ajoute :

private void OnDestroy()
{
    if (Instance == this)
    {
        Instance = null;
    }
}

C'est tout. Ça évite les références fantômes entre sessions Play Mode dans l'éditeur Unity.
```

🧪 **Test** : Entre/sors du Play Mode plusieurs fois. Pas de NullReferenceException.

---

## ═══════════════════════════════════════
## PHASES 4-5-6 — DÉCLENCHÉES PAR LE CONTENU
## ═══════════════════════════════════════

Ces phases ne sont pas scriptées en prompts car elles dépendent du contenu gameplay :

- **Phase 4** (Interfaces) → quand tu implémentes les bâtiments
- **Phase 5** (EntityRegistry + Systems POCO) → quand tu commences le contenu gameplay
- **Phase 6** (Broker Chain) → quand tu implémentes les capacités de factions

Réfère-toi au REFACTORING_STRATEGY.md pour les détails de chaque phase.
