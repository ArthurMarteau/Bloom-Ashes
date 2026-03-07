# NEW FEATURES PROMPTS — BLOOM & ASHES
# Fichier unique : stratégie + prompts copier-coller pour Claude Code
# Teste en Play Mode entre chaque sous-étape


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
