# REFACTORING PROMPTS — BLOOM & ASHES
# Fichier unique : stratégie + prompts copier-coller pour Claude Code
# Teste en Play Mode entre chaque sous-étape

---

# PHASE 4 — Interfaces

**Déclencheur** : Bâtiments implémentés (Nexus + bâtiments de faction) → 2 types d'entités prennent des dégâts → justifie `IDamageable`.

## 4.1 — IDamageable

```
Contexte : Lis claude.md et REFACTORING_STRATEGY.md.

Phase 4.1 — Créer l'interface IDamageable.

Objectif : Abstraire "chose qui prend des dégâts" pour que AttackController puisse cibler unités ET bâtiments.

À faire :
1. Créer Components/Interfaces/IDamageable.cs avec : TakeDamage(int damage, GameObject attacker = null), Heal(int amount), GetCurrentHP(), GetMaxHP(), GetHealthPercentage(), IsDead()
2. Faire implémenter IDamageable par UnitStatsComponent (les méthodes existent déjà, ajouter juste ": IDamageable")
3. Modifier AttackController pour cibler IDamageable au lieu de UnitStatsComponent dans : IsTargetValid(), FindNearestEnemyInRange(), PerformAttack(), DealDamageAtEndOfAnimation()
4. Le FindNearestEnemyInRange() doit aussi détecter les bâtiments (pas juste layer Units)

Règles :
- Migration progressive : le jeu doit compiler et fonctionner après cette étape
- Ne PAS créer d'autres interfaces (IMovable, ISelectable, etc.) — seulement quand justifié
- Commentaires en français avec // devant chaque méthode
- Debug.Log informatifs avec contexte et emojis
```

## 4.2 — ITeamMember

```
Contexte : Lis claude.md. Phase 4.2 du refactoring.

Phase 4.2 — Créer l'interface ITeamMember.

Objectif : Abstraire l'appartenance à une équipe pour que les bâtiments puissent aussi être amis/ennemis.

À faire :
1. Créer Components/Interfaces/ITeamMember.cs avec : GetTeamID(), IsEnemy(ITeamMember other), IsAlly(ITeamMember other)
2. Faire implémenter ITeamMember par TeamComponent
3. Adapter les signatures IsEnemy/IsAlly dans TeamComponent pour accepter ITeamMember en plus de GameObject (surcharge)
4. Dans AttackController, utiliser ITeamMember au lieu de TeamComponent directement quand possible

Règles :
- Le jeu doit compiler et fonctionner après cette étape
- Les méthodes existantes avec GameObject restent pour rétrocompatibilité
- Commentaires en français
```

## 4.3 — BuildingStatsComponent

```
Contexte : Lis claude.md et GDD.md (section Buildings). Phase 4.3 du refactoring.

Phase 4.3 — Créer BuildingStatsComponent.

Objectif : Premier composant bâtiment qui implémente IDamageable et ITeamMember, validant les interfaces créées en 4.1/4.2.

À faire :
1. Créer Data/Buildings/BuildingStatsData.cs (ScriptableObject) avec : maxHP, defense, buildingName, isInvulnerable (bool pour le Nexus)
2. Créer Components/Buildings/BuildingStatsComponent.cs qui implémente IDamageable et ITeamMember
3. Le composant doit gérer : prise de dégâts (avec invulnérabilité), mort/destruction, appartenance équipe
4. Créer un BuildingStatsData.asset pour le Nexus (HP=1, Defense=Invulnerable) et un pour Great Oak (HP=100, Defense=3)

Règles :
- Clean Architecture : SO pour données, MonoBehaviour pour cycle de vie
- Le Nexus est invulnérable (isInvulnerable=true dans le SO)
- Commentaires en français, Debug.Log avec emojis
- Le jeu compile et fonctionne
```

### Validation Phase 4

```
Contexte : Phase 4 terminée. Vérifie que tout fonctionne.

Checklist à valider :
- [ ] AttackController cible IDamageable (peut attaquer unités ET bâtiments)
- [ ] Bâtiments avec BuildingStatsComponent prennent des dégâts et sont détruits
- [ ] Le Nexus est invulnérable (ne prend pas de dégâts)
- [ ] Bâtiments ont une équipe (TeamComponent + ITeamMember)
- [ ] Aucune interface inutilisée (chaque interface a 2+ implémentations)
- [ ] Pas de régression : le combat unité vs unité fonctionne toujours

Propose des corrections si un point échoue.
```

---

# PHASE 5 — EntityRegistry + Systems POCO

**Déclencheur** : Contenu gameplay commence, besoin de performance sur 80x80 et préparation serveur autoritaire.

## 5.1 — EntityRegistry

```
Contexte : Lis claude.md. Phase 5.1 du refactoring.

Phase 5.1 — Créer EntityRegistry (MonoBehaviour singleton).

Objectif : Remplacer Physics2D.OverlapBoxAll par un registre centralisé pour la recherche d'entités. Performance critique pour 80x80 avec 50+ unités.

Problème actuel : AttackController.FindNearestEnemyInRange() utilise Physics2D.OverlapBoxAll à chaque check — coûteux et pas compatible serveur.

À faire :
1. Créer Systems/EntityRegistry.cs (MonoBehaviour singleton, PAS static)
2. Auto-register dans Awake() des UnitStatsComponent et BuildingStatsComponent (via IDamageable)
3. Auto-unregister dans OnDestroy()
4. API publique :
   - RegisterEntity(IDamageable entity, GameObject go)
   - UnregisterEntity(GameObject go)
   - FindEntitiesInRange(Vector2Int center, int range) → List<GameObject>
   - FindEnemiesInRange(Vector2Int center, int range, int teamID) → List<GameObject>
   - GetAllEntities() → List<GameObject>
5. Stocker les entités dans un Dictionary<GameObject, IDamageable>
6. Modifier AttackController.FindNearestEnemyInRange() pour utiliser EntityRegistry au lieu de Physics2D.OverlapBoxAll

Règles :
- MonoBehaviour singleton (pas static) pour compatibilité netcode
- Le jeu doit fonctionner identiquement après migration
- Aucun Physics2D.OverlapBoxAll ne doit rester pour la recherche d'unités
- Commentaires en français, Debug.Log avec emojis
```

## 5.2 — CombatSystem POCO

```
Contexte : Lis claude.md. Phase 5.2 du refactoring.

Phase 5.2 — Extraire la logique de combat en POCO (Systems/Combat/CombatSystem.cs).

Objectif : Logique de combat pure, testable sans Unity, prête pour serveur autoritaire.

À faire :
1. Créer Systems/Combat/CombatSystem.cs (classe static ou POCO, PAS MonoBehaviour)
2. Extraire depuis AttackController :
   - IsInRange(Vector2Int attackerPos, Vector2Int targetPos, int range) → bool
   - CalculateDamage(int baseDamage, int defense) → int (déjà dans UnitStatsLogic, centraliser ici)
   - FindBestAttackPosition(Vector2Int targetPos, Vector2Int currentPos, int range, Func<Vector2Int, bool> isWalkable) → Vector2Int
   - CalculateChebyshevDistance(Vector2Int a, Vector2Int b) → int
3. AttackController délègue les calculs à CombatSystem
4. RangeOffsets.cs reste tel quel (déjà dans Systems/Combat/)

Règles :
- POCO : aucune dépendance Unity (pas de MonoBehaviour, pas de GameObject, pas de Vector3)
- Utiliser Vector2Int pour les positions de grille
- Le jeu doit fonctionner identiquement
- Commentaires en français
```

## 5.3 — GridSystem POCO

```
Contexte : Lis claude.md. Phase 5.3 du refactoring.

Phase 5.3 — Extraire la logique de grille en POCO (Systems/Grid/GridSystem.cs).

Objectif : Logique de grille pure, testable sans Unity, prête pour serveur autoritaire.

À faire :
1. Créer Systems/Grid/GridSystem.cs (classe POCO, PAS MonoBehaviour)
2. Extraire depuis GridManager la logique pure :
   - WorldToGrid(float x, float y, float cellSize, float originX, float originY) → Vector2Int
   - GridToWorld(int gridX, int gridY, float cellSize, float originX, float originY) → (float x, float y)
   - IsInBounds(int x, int y, int width, int height) → bool
   - IsWalkable(int x, int y, ...) → bool
3. GridManager devient un pont Unity :
   - Conserve : singleton, Gizmos, Awake/lifecycle, SerializeField
   - Délègue les calculs à GridSystem
   - Ses méthodes publiques appellent GridSystem en interne
4. PathFinding.cs utilise GridSystem au lieu de GridManager pour les calculs purs (si possible sans casser)

Règles :
- POCO : aucune dépendance Unity dans GridSystem
- GridManager garde son API publique identique (pas de breaking change)
- Le jeu doit fonctionner identiquement
- Commentaires en français
```

### Validation Phase 5

```
Contexte : Phase 5 terminée. Vérifie que tout fonctionne.

Checklist à valider :
- [ ] Plus aucun Physics2D.OverlapBoxAll pour recherche d'unités
- [ ] EntityRegistry enregistre/désenregistre correctement les entités
- [ ] CombatSystem est testable unitairement (pas de dépendance Unity)
- [ ] GridSystem est testable unitairement (pas de dépendance Unity)
- [ ] Auto-attaque fonctionne toujours correctement
- [ ] PathFinding fonctionne toujours
- [ ] Performance stable avec les unités existantes

Propose des corrections si un point échoue.
```

---

# PHASE 6 — Broker Chain (Modificateurs de stats)

**Déclencheur** : Implémentation des premières capacités de faction (poison, slow, buffs).

## 6.1 — IStatModifier + StatModifierMediator

```
Contexte : Lis claude.md et GDD.md (section abilities des factions). Phase 6.1 du refactoring.

Phase 6.1 — Créer le système de modificateurs de stats.

Objectif : Système extensible de buffs/debuffs pour les capacités des 5 factions. Pattern Broker Chain : Stats de base (SO) → Mediator → Modificateurs → Valeur finale.

À faire :
1. Créer Systems/Modifiers/IStatModifier.cs (interface) :
   - int Priority (ordre d'application)
   - float Duration (durée en secondes, -1 = permanent)
   - float RemainingTime (temps restant)
   - bool IsExpired()
   - int ModifyAttack(int baseValue)
   - int ModifyDefense(int baseValue)
   - float ModifyMoveSpeed(float baseValue)
   - int ModifyMaxHP(int baseValue)

2. Créer Systems/Modifiers/StatModifierMediator.cs (POCO) :
   - List<IStatModifier> de modificateurs actifs
   - AddModifier(IStatModifier mod)
   - RemoveModifier(IStatModifier mod)
   - UpdateTimers(float deltaTime) — décrémente les durées, retire les expirés
   - GetModifiedAttack(int baseValue) → applique tous les mods triés par Priority
   - GetModifiedDefense(int baseValue)
   - GetModifiedMoveSpeed(float baseValue)
   - GetModifiedMaxHP(int baseValue)

Règles :
- POCO : aucune dépendance Unity dans les Systems
- Triés par Priority (plus bas = appliqué en premier)
- Commentaires en français
```

## 6.2 — Intégration UnitStatsComponent

```
Contexte : Lis claude.md. Phase 6.2 du refactoring.

Phase 6.2 — Intégrer le StatModifierMediator dans UnitStatsComponent.

Objectif : Les getters de stats passent par le Mediator pour appliquer buffs/debuffs en temps réel.

À faire :
1. Ajouter un StatModifierMediator privé dans UnitStatsComponent
2. Modifier les getters pour passer par le Mediator :
   - GetAttackDamage() → mediator.GetModifiedAttack(baseStats.attackDamage)
   - GetDefense() → mediator.GetModifiedDefense(baseStats.defense)
   - GetMoveSpeed() → mediator.GetModifiedMoveSpeed(baseStats.moveSpeed)
   - GetMaxHP() → mediator.GetModifiedMaxHP(baseStats.maxHP)
3. Ajouter API publique :
   - AddModifier(IStatModifier mod)
   - RemoveModifier(IStatModifier mod)
4. Dans Update(), appeler mediator.UpdateTimers(Time.deltaTime) pour gérer les expirations
5. Supprimer les anciens setters (SetMaxHP, SetAttackDamage, SetDefense, SetMoveSpeed) qui étaient des placeholders

Règles :
- Les stats de base dans le SO ne sont JAMAIS modifiées
- Le Mediator calcule les valeurs finales à chaque appel
- Le jeu doit fonctionner identiquement (sans mods, les getters retournent les valeurs de base)
- Commentaires en français, Debug.Log avec emojis
```

## 6.3 — Exemples concrets de modificateurs

```
Contexte : Lis claude.md et GDD.md. Phase 6.3 du refactoring.

Phase 6.3 — Créer des modificateurs concrets pour tester le système.

Objectif : Valider le Broker Chain avec des exemples réels tirés du GDD.

À faire :
1. Créer Systems/Modifiers/PoisonModifier.cs :
   - Implémente IStatModifier
   - Durée : 3 secondes
   - Effet : inflige 2 HP/sec (via un callback ou event, pas via ModifyAttack)
   - Note : le poison est un DoT, pas un modifier de stat — adapter l'interface si nécessaire (ajouter ApplyEffect(UnitStatsComponent target, float deltaTime) ?)

2. Créer Systems/Modifiers/SlowModifier.cs :
   - Implémente IStatModifier
   - Durée : 3 secondes
   - ModifyMoveSpeed : -1 (min 1)

3. Créer Systems/Modifiers/DefenseBuffModifier.cs :
   - Implémente IStatModifier
   - Durée : configurable
   - ModifyDefense : +valeur configurable

4. Tester : ajouter un [ContextMenu] sur UnitStatsComponent pour appliquer chaque modifier et vérifier que les stats changent puis reviennent à la normale.

Règles :
- Chaque modifier est une classe séparée
- Les DoT (poison) nécessitent peut-être une extension de l'interface — propose la meilleure approche
- Commentaires en français, Debug.Log avec emojis
```

### Validation Phase 6

```
Contexte : Phase 6 terminée. Vérifie que tout fonctionne.

Checklist à valider :
- [ ] Buffs/debuffs s'appliquent correctement (stats modifiées visibles dans les logs)
- [ ] Les modificateurs expirent après leur durée
- [ ] Stack de modificateurs fonctionne (2+ mods simultanés sur une unité)
- [ ] Les stats reviennent exactement à la normale après expiration
- [ ] Le poison (DoT) fonctionne et inflige des dégâts sur la durée
- [ ] Le slow réduit la vitesse et elle revient à la normale
- [ ] Pas de régression sur le combat existant
- [ ] Pas d'impact performance notable

Propose des corrections si un point échoue.
```

---

# Points d'Attention Netcode (Hors Phases)

À garder en tête pour le futur :
- New Input System (migration dans InputManager.cs uniquement)
- `GridManager.Instance` (singleton static) → service injecté ou MonoBehaviour singleton
- `OrderManager` → validation ordres côté serveur
- `PathFinding` → potentiellement côté serveur (GridSystem POCO le prépare)
- Positions logiques (`logicalPosition` dans MovementController) = source de vérité → bon pattern netcode
- `EntityRegistry` en MonoBehaviour singleton = compatible lifecycle réseau
