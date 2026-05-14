# Bloom & Ashes — Game Design Document

## Vision

Old school 2D RTS inspired by **Blood and Magic** (1996), modernized with dynamic gameplay, network multiplayer (up to 6 players), and deeper mechanics. Designed for mobile-first accessibility with grid-based movement and limited unit micro-management.

## Universe

- **Theme**: Medieval-fantasy centered on plant magic and druids
- **Setting**: The Shifting Groves of Cirgus
- **Visual style**: 3D Low Poly to 2D Line Art, old school 8-bit feel
- **Audio**: 8-bit music per map, 8-bit sound effects
- **Language**: English — all in-game content visible to the player is in English (unit names, ability names, tooltips, UI text, etc.)
- **Single resource**: "Watersap" — collected and stored individually by each Rootborn, then transferred to the player's pool (max 300 per player)

## Core Mechanics

- Top-down view, tile-by-tile movement in 8 directions
- Real-time: all units move simultaneously
- One game object per tile
- Simplified but tactical management, limited unit count
- Fast and strategic engagements
- Abilities cost Watersap directly (army building vs spell casting trade-off)
- **Ability Auto-Cast**: All active abilities can be toggled to auto-cast. When enabled, the unit uses the ability automatically when conditions are met (target in range, enough Watersap).
- **Sacrifice**: Any unit can be sacrificed (active ability) to grant 10 Watersap. Strategic use: sacrifice a dying unit before death to recover resources.
- Units size 1x1. Building 1x1 or more.
- Pas de virgule pour les stats. (Except Path Ground)

## Map

- **Target size**: 80x80 tiles
- **Biomes**: Meadows, Gardens, Dark Forest, Caves, Scorched Lands, Volcano, Desert, Jungle, Beach, Ocean Palace
- One game object per tile

## Foundations & Buildings

### Foundations
Foundations are strategic points scattered across the map, used to construct faction buildings.

- **Count**: 3 foundations per player (scales with player count: 6 players = 18 foundations)
- **Starting foundation**: Always placed near the player's Nexus
- **Remaining foundations**: Scattered across the map — neutral and contestable
- Foundations and Watersap sources are the primary **strategic objectives** on the map

### Building Construction
A foundation consists of **4 tiles**. To build:
1. Place 1 idle Rootborn on each of the 4 foundation tiles
2. Once all 4 are in position, a **faction selection UI** appears
3. Choose a faction → the 4 Rootborn sacrifice themselves and the building is constructed

### Unit Production
Once a building exists:
1. Move idle Rootborn to any walkable tile adjacent to the building
2. A **unit selection UI** appears on the Rootborn
3. Choose a unit type → Rootborn becomes a seed, then hatches into the chosen unit after build time

### Tier & Research System
- **T1 unit** is available immediately when the building is constructed
- **T2, T3, T4 units** must be researched via the building UI (increasing Watersap cost)
- Research is per-building and persistent for the rest of the game

### Strategic Implications
- A player can construct **different faction buildings** across their foundations
- Controlling more foundations = access to more factions = more unit diversity
- **Inter-faction synergies** open up when a player controls multiple buildings of different factions (to be detailed)

## Technical Implementation (Game Design)

- **Grid**: 80x80 tiles, A* pathfinding with MinHeap
- **Prefabs**: 1 prefab per unit per faction, stats via ScriptableObjects (no generic prefab)
- **Rootborn → combat**: Destroy + Instantiate new prefab (fixed type via SO, not mutable state)
- **Input**: Migration planned to New Input System with netcode

---

## Rootborn (Common Worker Unit)

The base unit shared by all factions, created at the Nexus.

**Description**:

*Worker unit. Collects Watersap, builds faction structures, and transforms into combat units.*

| Stat          | Value     |
| ------------- | --------- |
| HP            | 20        |
| Attack Damage | 3         |
| Attack Speed  | 2         |
| Attack Range  | 1 (melee) |
| Move Speed    | 2         |
| Defense       | 0         |
| Unit Type     | Ground    |

| Upgrade | Cost | Build Time |
| ------- | ---- | ---------- |
| Train   | 40   | 10         |

**Abilities**:

| Ability             | Type             | Cost | Cooldown | Description                                                                                  |
| ------------------- | ---------------- | ---- | -------- | -------------------------------------------------------------------------------------------- |
| Watersap Collection | Passive + Active | 0    | 0        | Collects and stores Watersap individually, then transfers it to the player's pool.           |
| Source Collection   | Passive          | 0    | —        | Doubles the collection rate when standing on a Watersap source.                              |
| Build               | Active           | 0    | —        | 4 Rootborn on a foundation site sacrifice themselves to build a faction building.            |
| Transform           | Active           | —    | —        | Adjacent to a building, transforms into a chosen combat unit (becomes a seed, then hatches). |

See the **Abilities** section below for detailed mechanics.

---

## Bud (Transitional State)

When a Rootborn transforms into a combat unit adjacent to a building, it enters the **Bud** state — a dormant seed-pod that gestates the chosen unit.

**Description**:

*Transitional state. Keeps the Rootborn's current HP but gains defense. Immobile until transformation is complete.*

- The Bud is **immobile** — it can be attacked and destroyed before hatching
- A progress bar tracks the remaining build time
- The Bud can be **cancelled** (Cancel button in UnitPanelUI) — the Rootborn is lost, no refund
- On completion, the Bud is destroyed and the combat unit spawns on its tile

---

## Nexus (Common Building)

| Stat    | Value        |
| ------- | ------------ |
| HP      | 1            |
| Defense | Invulnerable |
| Size    | 1x1          |

**Function**: Spawns Rootborn units. Reverse battle unit in Rootborn for 10 Watersap.

---

## The 5 Factions

### 1. Mycelian Champions

**Identity**: Mushrooms, poison, spores
**Biomes**: Dark Forest, Caves
**Strength**: Very versatile — healing, control, defense, damage, flying unit
**Weakness**: Doesn't excel at anything
**Building**: Shroomhaven

#### Building

**Shroomhaven**

*Fungal stronghold rooted deep into the earth. Produces all Mycelian combat units.*

| Stat         | Value |
| ------------ | ----- |
| HP           | 100   |
| Defense      | 3     |
| Size         | 2x2   |

#### Units

**Lionsmane Priest** — Melee / Support (T1)

**Description**:

*Melee support unit. Heals adjacent allies with regenerating spores.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 25     |
| Attack Damage | 1      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 2      |
| Defense       | 1      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 20   | 20         |
| Research | 0    | 0          |

| Ability             | Type   | Cost | Cooldown | Area Effect | Heal | Time Effect | Description             |
| ------------------- | ------ | ---- | -------- | ----------- | ---- | ----------- | ----------------------- |
| Regenerating Spores | Active | 5    | 20       | 1x1         | 5hp  | Once        | Heals an ally for 5 HP. |
|                     |        |      |          |             |      |             |                         |


**Deathcap Assassin** — CAC DPS / Control / Scout (T2)

**Description**:

*Melee DPS and control unit. Sets paralyzing traps and hides in bushes to ambush enemies.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 20     |
| Attack Damage | 5      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 1.2    |
| Defense       | 3      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 25   | 25         |
| Research | 50   | 0          |

| Ability       | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect      | Description                                                            |
| ------------- | ------- | ---- | -------- | ----------- | ------ | ---------------- | ---------------------------------------------------------------------- |
| Puffball Trap | Active  | 10   | 5        | 1x1         | 0      | Once             | Places a trap that paralyzes its victim for 3 seconds. (Only Non-ally) |
| Hide          | Passive | 0    | 0        | 1x1         | 0      | 2s before effect | Can hide in bushes                                                     |


**Bolete Golem** — Tank (T3)

**Description**:

*Frontline tank. Slow but extremely durable — absorbs damage for the rest of the army.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 35     |
| Attack Damage | 5      |
| Attack Speed  | 1      |
| Attack Range  | 1      |
| Move Speed    | 1      |
| Defense       | 5      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 30   | 30         |
| Research | 80   | 0          |

No active ability.


**Flying Enoki** — Ranged DPS / Poison / Flying (T4)

**Description**:

*Ranged flying DPS. Spits poison clouds that deal increased damage to paralyzed targets.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 30     |
| Attack Damage | 4      |
| Attack Speed  | 2      |
| Attack Range  | 2      |
| Move Speed    | 2      |
| Defense       | 2      |
| Unit Type     | Flying |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 35   | 35         |
| Research | 100  | 0          |

| Ability              | Type    | Cost | Cooldown | Area Effect | Damage       | Time Effect | Description                                                                                     |
| -------------------- | ------- | ---- | -------- | ----------- | ------------ | ----------- | ----------------------------------------------------------------------------------------------- |
| Poisonous Spore Mist | Active  | 10   | 15       | 1x1         | 2/sec (3sec) | 3s          | Spits a poison cloud lasting 3 seconds, dealing 2 dmg/sec. +1 dmg if target is paralyzed.      |
| Flying               | Passive | 0    | 0        | —           | —            | —           | Ignores ground terrain effects, can cross Water/Mountain tiles.                                 |

---

### 2. Golden Garden Charmers

**Identity**: Flowers, thorns, sunlight
**Biomes**: Meadows, Gardens
**Strength**: Control and defense, flying unit
**Weakness**: No heavy damage, no tank
**Building**: Great Solar Crystal

#### Building

**Great Solar Crystal**

*A towering crystal that channels sunlight into the battlefield. Produces all Charmer combat units.*

| Stat         | Value |
| ------------ | ----- |
| HP           | 100   |
| Defense      | 3     |
| Size         | 2x2   |

#### Units

**Thistle Warrior** — Melee

**Description**:

*Melee frontline unit. Solid damage and defense for its cost.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 25     |
| Attack Damage | 5      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 2      |
| Defense       | 4      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 20   | 20         |
| Research | 0    | 0          |

No active ability.


**Flying Rose** — Ranged DPS / Flying

**Description**:

*Ranged flying DPS. Fires piercing projectiles that hit multiple targets in a line.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 20     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 3      |
| Move Speed    | 2      |
| Defense       | 1      |
| Unit Type     | Flying |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 25   | 25         |
| Research | 50   | 0          |

| Ability       | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                                             |
| ------------- | ------- | ---- | -------- | ----------- | ------ | ----------- | ----------------------------------------------------------------------- |
| Piercing Shot | Passive | 0    | 0        | —           | —      | —           | Projectile passes through its target, hitting units behind.             |
| Flying        | Passive | 0    | 0        | —           | —      | —           | Ignores ground terrain effects, can cross Water/Mountain tiles.         |


**Luminous Sunflower** — Mage

**Description**:

*Ranged mage. Reflects enemy projectiles back to their sender.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 25     |
| Attack Damage | 4      |
| Attack Speed  | 2      |
| Attack Range  | 2      |
| Move Speed    | 2      |
| Defense       | 2      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 30   | 25         |
| Research | 80   | 0          |

| Ability        | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                        |
| -------------- | ------ | ---- | -------- | ----------- | ------ | ----------- | -------------------------------------------------- |
| Blazing Mirror | Active | 5    | 20       | 1x1         | —      | Once        | Reflects projectiles back to their sender.         |


**Seductress Dahlia** — Support / Mind Control

**Description**:

*Support unit. Charms an enemy unit and turns it against its allies. Very fragile.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 20     |
| Attack Damage | 1      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 2      |
| Defense       | 1      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 35   | 30         |
| Research | 100  | 0          |

| Ability          | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                                            |
| ---------------- | ------ | ---- | -------- | ----------- | ------ | ----------- | ---------------------------------------------------------------------- |
| Hypnotic Pollen  | Active | 15   | 30       | 1x1         | 0      | 8s          | Charms an enemy for 8 seconds, turning it against its allies.          |

---

### 3. Wild Fangs

**Identity**: Survivalist plants, cacti, carnivorous plants
**Biomes**: Desert, Jungle
**Strength**: Low unit production cost, high defense, many passive abilities
**Weakness**: No flying unit, low mobility, little healing, longer unit build time
**Building**: Great Stone

#### Building

**Great Stone**

*An ancient carved monolith. Produces all Wild Fangs combat units.*

| Stat         | Value |
| ------------ | ----- |
| HP           | 100   |
| Defense      | 3     |
| Size         | 2x2   |

#### Units

**Cactus Warrior** — Melee

**Description**:

*Melee tank. Returns damage to melee attackers. Cheap and durable.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 30     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 1      |
| Defense       | 4      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 15   | 25         |
| Research | 0    | 0          |

| Ability | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                          |
| ------- | ------- | ---- | -------- | ----------- | ------ | ----------- | ------------------------------------ |
| Thorns  | Passive | 0    | 0        | —           | 1      | —           | Returns 1 damage to melee attackers. |


**Nepenthes Shooter** — Ranged DPS / Control / Scout

**Description**:

*Ranged DPS. Slows enemy movement speed on hit — synergizes with Venus Assassin.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 20     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 3      |
| Move Speed    | 2      |
| Defense       | 1      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 20   | 30         |
| Research | 50   | 0          |

| Ability      | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                    |
| ------------ | ------- | ---- | -------- | ----------- | ------ | ----------- | ---------------------------------------------- |
| Lactic Acid  | Passive | 0    | 0        | —           | 0      | 3s          | Slows target by 1 Move Speed for 3 seconds.   |


**Venus Assassin** — Assassin

**Description**:

*Melee assassin. Deals bonus damage to slowed targets — pairs well with Nepenthes Shooter.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 15     |
| Attack Damage | 3      |
| Attack Speed  | 1      |
| Attack Range  | 1      |
| Move Speed    | 3      |
| Defense       | 0      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 25   | 30         |
| Research | 80   | 0          |

| Ability | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                          |
| ------- | ------ | ---- | -------- | ----------- | ------ | ----------- | ---------------------------------------------------- |
| Bite    | Active | 10   | 10       | 1x1         | 5 (8)  | Once        | Deals 5 damage. 8 to slowed targets.                |


**Welwitschia Troll** — Melee / Tank

**Description**:

*Melee tank. Regenerates HP passively — extremely hard to wear down in prolonged fights.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 40     |
| Attack Damage | 4      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 1      |
| Defense       | 4      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 30   | 35         |
| Research | 100  | 0          |

| Ability      | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                        |
| ------------ | ------- | ---- | -------- | ----------- | ------ | ----------- | ---------------------------------- |
| Regeneration | Passive | 0    | 0        | —           | —      | Every 3s    | Recovers 1 HP every 3 seconds.    |

---

### 4. The Scorched

**Identity**: Fire and necromancy, aggressive and invasive
**Biomes**: Scorched Lands, Volcano
**Strength**: Very high mobility, high damage, flying unit
**Weakness**: Low defense, no healing
**Building**: Great Furnace

#### Building

**Great Furnace**

*A roaring forge of scorched wood and ember. Produces all Scorched combat units.*

| Stat         | Value |
| ------------ | ----- |
| HP           | 100   |
| Defense      | 3     |
| Size         | 2x2   |

#### Units

**Charred Branch** — Melee

**Description**:

*Melee DPS. Fast and cheap — can self-destruct to deal area damage.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 20     |
| Attack Damage | 4      |
| Attack Speed  | 1      |
| Attack Range  | 1      |
| Move Speed    | 3      |
| Defense       | 1      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 15   | 15         |
| Research | 0    | 0          |

| Ability           | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                                    |
| ----------------- | ------ | ---- | -------- | ----------- | ------ | ----------- | -------------------------------------------------------------- |
| Explosive Suicide | Active | 10   | —        | 3x3         | 10     | Once        | Sacrifices itself to deal 10 damage to all units in 3x3 area.  |


**Screaming Ember** — Ranged DPS

**Description**:

*Ranged DPS. High damage output, high speed. Very low defense.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 15     |
| Attack Damage | 5      |
| Attack Speed  | 2      |
| Attack Range  | 3      |
| Move Speed    | 3      |
| Defense       | 0      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 20   | 20         |
| Research | 50   | 0          |

No active ability.


**Blazing Fury** — Assassin / Flying

**Description**:

*Flying assassin. Targets a single enemy and fights to the death. Spawns a Rootborn on kill.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 10     |
| Attack Damage | 8      |
| Attack Speed  | 1      |
| Attack Range  | 1      |
| Move Speed    | 3      |
| Defense       | 0      |
| Unit Type     | Flying |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 25   | 25         |
| Research | 80   | 0          |

| Ability | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                                                 |
| ------- | ------- | ---- | -------- | ----------- | ------ | ----------- | --------------------------------------------------------------------------- |
| Phoenix | Passive | 0    | 0        | —           | —      | —           | Dies after eliminating its target. Spawns a Rootborn if the kill succeeds.  |
| Flying  | Passive | 0    | 0        | —           | —      | —           | Ignores ground terrain effects, can cross Water/Mountain tiles.             |


**Burning Trunk** — Melee / DPS

**Description**:

*Melee bruiser. Burns and frightens nearby enemies with an area nova.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 25     |
| Attack Damage | 4      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 2      |
| Defense       | 2      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 30   | 30         |
| Research | 100  | 0          |

| Ability            | Type   | Cost | Cooldown | Area Effect | Damage       | Time Effect | Description                                                          |
| ------------------ | ------ | ---- | -------- | ----------- | ------------ | ----------- | -------------------------------------------------------------------- |
| Incandescent Nova  | Active | 10   | 20       | 3x3         | 1/sec (4sec) | 4s          | Frightens and burns nearby enemies for 1 dmg/sec for 4 seconds.     |

---

### 5. Chosen of the Abyss

**Identity**: Tides, aquatic plants, healing and control
**Biomes**: Beach, Swamp
**Strength**: Low spell costs, many spells
**Weakness**: No flying unit, low mobility
**Building**: Great Shell

#### Building

**Great Shell**

*A colossal shell risen from the deep. Produces all Abyss combat units.*

| Stat         | Value |
| ------------ | ----- |
| HP           | 100   |
| Defense      | 3     |
| Size         | 2x2   |

#### Units

**Lily Pad Guard** — Melee

**Description**:

*Melee tank. Blocks incoming projectiles to protect allied spellcasters.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 30     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 2      |
| Defense       | 3      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 20   | 20         |
| Research | 0    | 0          |

| Ability | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                       |
| ------- | ------ | ---- | -------- | ----------- | ------ | ----------- | --------------------------------- |
| Block   | Active | 3    | 5        | 1x1         | 0      | Once        | Blocks one incoming projectile.   |


**Lotus Priest** — Ranged DPS / Support

**Description**:

*Ranged support unit. Heals allies and deals ranged damage. Cheapest heal in the game.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 20     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 3      |
| Move Speed    | 2      |
| Defense       | 0      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 25   | 25         |
| Research | 50   | 0          |

| Ability | Type   | Cost | Cooldown | Area Effect | Heal | Time Effect | Description                |
| ------- | ------ | ---- | -------- | ----------- | ---- | ----------- | -------------------------- |
| Heal    | Active | 3    | 15       | 1x1         | 5hp  | Once        | Heals an ally for 5 HP.    |


**Kelp Executioner** — Melee / Control

**Description**:

*Melee control unit. Pulls allies or enemies to its position. Deals damage to pulled enemies.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 25     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 2      |
| Defense       | 2      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 30   | 30         |
| Research | 80   | 0          |

| Ability         | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                                                      |
| --------------- | ------ | ---- | -------- | ----------- | ------ | ----------- | -------------------------------------------------------------------------------- |
| Abyssal Embrace | Active | 8    | 15       | 1x1         | 5      | Once        | Pulls a unit (ally or enemy) to itself. Enemies take 5 damage.                   |


**Royal Coral** — Tank / Mage

**Description**:

*Melee tank. Sends a damaging wave across a line of tiles. Slow but powerful.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 40     |
| Attack Damage | 3      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
| Move Speed    | 1      |
| Defense       | 4      |
| Unit Type     | Ground |

| Upgrade  | Cost | Build Time |
| -------- | ---- | ---------- |
| Train    | 35   | 35         |
| Research | 100  | 0          |

| Ability       | Type   | Cost | Cooldown | Area Effect | Damage | Time Effect | Description                                                   |
| ------------- | ------ | ---- | -------- | ----------- | ------ | ----------- | ------------------------------------------------------------- |
| Abyssal Wave  | Active | 8    | 20       | 3x1 line    | 5      | Once        | Sends a wave across 3 tiles in front, dealing 5 damage.       |

---

## Neutral Entities

### Neutral Buildings (Points of Interest)
Destructible objects that drop items when destroyed. One per biome.

| Entity              | Biome          | HP  | Defense | Size | Drop        |
| ------------------- | -------------- | --- | ------- | ---- | ----------- |
| Mycelian Idol       | Dark Forest    | 75  | 5       | 1x1  | Random item |
| Ancient Statue      | Caves          | 75  | 5       | 1x1  | Random item |
| Solar Obelisk       | Meadows        | 60  | 3       | 1x1  | Random item |
| Golden Fountain     | Gardens        | 60  | 3       | 1x1  | Random item |
| Desert Monolith     | Desert         | 75  | 5       | 1x1  | Random item |
| Jungle Totem        | Jungle         | 60  | 3       | 1x1  | Random item |
| Charred Altar       | Scorched Lands | 50  | 2       | 1x1  | Random item |
| Magma Crystal       | Volcano        | 75  | 5       | 1x1  | Random item |
| Coral Shrine        | Beach          | 60  | 3       | 1x1  | Random item |
| Abyssal Monument    | Ocean Palace   | 75  | 5       | 1x1  | Random item |

### Hostile Wildlife (Neutral faction)
Aggressive creatures not affiliated with any faction. One per biome.

| Entity              | Biome          | HP  | ATK | ATK Speed | Range | Move | DEF | Behavior                    |
| ------------------- | -------------- | --- | --- | --------- | ----- | ---- | --- | --------------------------- |
| Shadow Spore        | Dark Forest    | 20  | 3   | 2         | 1     | 2    | 1   | Patrol, aggressive          |
| Cave Crawler        | Caves          | 25  | 4   | 2         | 1     | 1    | 3   | Guards passages             |
| Meadow Hornet       | Meadows        | 12  | 2   | 1         | 1     | 3    | 0   | Swarm, aggressive           |
| Garden Beetle       | Gardens        | 15  | 3   | 2         | 1     | 2    | 2   | Patrol, moderate            |
| Sand Scorpion       | Desert         | 20  | 5   | 2         | 1     | 2    | 2   | Ambush, aggressive          |
| Jungle Strangler    | Jungle         | 25  | 3   | 2         | 2     | 0    | 2   | Stationary, area denial     |
| Ember Hound         | Scorched Lands | 18  | 4   | 1         | 1     | 3    | 0   | Patrol, aggressive          |
| Magma Slug          | Volcano        | 30  | 3   | 2         | 1     | 1    | 4   | Guards resources            |
| Tidal Crab          | Beach          | 20  | 3   | 2         | 1     | 2    | 3   | Patrol, moderate            |
| Abyssal Eel         | Ocean Palace   | 25  | 5   | 2         | 2     | 2    | 1   | Guards Watersap sources     |

---

## Terrain System

### Terrain Types

| Terrain         | Path Cost  | Move Speed Mod | Defense Mod | Notes                                         |
| --------------- | ---------- | -------------- | ----------- | --------------------------------------------- |
| Grass           | 1.0        | 0              | 0           | Default terrain                               |
| Dirt            | 1.0        | 0              | 0           | Default terrain                               |
| Path            | 0.5        | +1             | 0           | A* prefers paths for faster routes            |
| Rocky           | 2.0        | -1 (min 1)     | +2          | Slows but protects                            |
| Swamp           | 1.5        | -1 (min 1)     | 0           | Slow, potential poison for non-Abyss          |
| Brambles        | 1.5        | -1 (min 1)     | +1          | Deals 1 dmg/cell to non-Charmers             |
| Sand            | 1.5        | -1 (min 1)     | 0           | Desert terrain                                |
| Scorched        | 1.0        | 0              | 0           | Deals 1 dmg/turn to non-Scorched             |
| Lava            | 1.0        | 0              | 0           | Kills instantly to non-Scorched               |
| Water           | Impassable | —              | —           | Only aquatic/flying units                     |
| Mountain        | Impassable | —              | —           | Only flying units                             |
| Ravine          | Impassable | —              | —           | Only flying units. Kills instantly.           |
| Bush            | 1.0        | 0              | +1          | Provides camouflage for Puffball Scout        |
| Watersap Source | 1.0        | 0              | +1          | Doubles Watersap collection for the Rootborn. |

### Faction-Terrain Interactions

| Faction                | Terrain Bonus                                                                 |
| ---------------------- | ----------------------------------------------------------------------------- |
| Mycelian Champions     | Immune to Forest/Swamp penalties. +1 Defense in Forest.                       |
| Golden Garden Charmers | +1 Move Speed on Grass. Immune to Grass/Path/Brambles penalties.              |
| Wild Fangs             | Immune to Sand/Rocky penalties. +1 Defense on Rocky.                         |
| The Scorched           | Immune to Scorched damage. +1 Attack on Scorched tiles.                       |
| Chosen of the Abyss    | Can walk on Water (cost 1.0). Immune to Swamp penalties. +1 Defense in Water. |

### Terrain Visibility (Nice-to-Have)
- **Bush**: Units inside have camouflage — enemies must be adjacent to see them

### Terrain Damage
- **Scorched**: 1 damage per turn to non-Scorched units
- **Swamp (toxic variant)**: 1 damage per turn to non-Abyss units (TBD)

### Pathfinding Integration
- A* uses terrain path cost as tile weight
- Flying units ignore terrain cost (always 1.0)
- Nice-to-have: A* prefers Path tiles for faster travel even if route is longer in tile count

---

## Abilities

This section provides detailed mechanics for abilities that go beyond the unit table descriptions.

### Watersap Collection (Rootborn)

**Type**: Passive (auto) + Active (manual trigger)

**Passive collection**:
- Each idle Rootborn collects **1 Watersap every 5 seconds** automatically.
- Watersap is **stored inside the Rootborn**, up to a maximum of **10**.
- The collection timer **resets to 0 whenever the Rootborn receives an order** (move, transform, build, etc.). This prevents timer-stacking exploits.

**Auto-transfer**:
- When the internal stock reaches **10**, the Rootborn automatically transfers all 10 Watersap to the player's pool.
- A luminous wave VFX plays inside the Rootborn from roots to head.

**Manual trigger (Active ability)**:
- Cost: **0 Watersap**
- Cooldown: **0**
- Effect: instantly transfers the entire stored stock to the player's pool (same VFX as auto-transfer).
- Does **not** reset the collection timer (the next 5s tick continues normally).

**Manual trigger availability**:
- Stock = 0 → ❌ disabled (nothing to transfer)
- Stock between 1 and 9 → ✅ available
- Stock = 10 → ❌ disabled (auto-transfer is about to fire — prevents double trigger)

**Loss conditions**:
- If the Rootborn dies, the stored Watersap is **lost** (not transferred, not dropped).

**Visual feedback**:
- On any transfer (auto or manual): a luminous wave propagates inside the Rootborn from the roots up the trunk and branches to the head, symbolizing the Watersap leaving the body toward the player's pool.

### Source Collection (Rootborn)

**Type**: Passive

**Mechanics**:
- A Rootborn standing on a Watersap source tile collects at **double rate** (2 Watersap every 5 seconds instead of 1).
- All other rules of Watersap Collection still apply (storage cap, timer reset on order, etc.).

**Implementation status**: Designed, not yet implemented.

---

## Buildings

| Building      | Faction             | HP  | Defense      | Size | Construction             |
| ------------- | ------------------- | --- | ------------ | ---- | ------------------------ |
| Nexus         | All                 | 1   | Invulnerable | 1x1  | Starting building        |
| Shroomhaven   | Mycelian Champions  | 100 | 3            | 2x2  | 4 Rootborn on foundation |
| Solar Crystal | Golden Garden       | 100 | 3            | 2x2  | 4 Rootborn on foundation |
| Great Stone   | Wild Fangs          | 100 | 3            | 2x2  | 4 Rootborn on foundation |
| Great Furnace | The Scorched        | 100 | 3            | 2x2  | 4 Rootborn on foundation |
| Great Shell   | Chosen of the Abyss | 100 | 3            | 2x2  | 4 Rootborn on foundation |

**Construction**: 4 Rootborn placed on a foundation site (4 tiles) sacrifice themselves.
**Unit Creation**: Rootborn moves to any walkable tile adjacent to the building, becomes a seed, transforms into chosen unit after build time.

---

## Items (Nice-to-Have)

Units can equip one item dropped by destroying a point of interest (building, statue...). Item system TBD.

---

## Victory Conditions

- **Elimination**: Destroy all enemy units and buildings
- **Fort Destruction**: Destroy the enemy Nexus (if not invulnerable)
- **Point Capture**: Control designated points for a set duration
- **Survival**: Resist enemy waves for a set duration

## Game Modes

- **Solo Campaign**: Narrative with factions, betrayals, artifacts, abyssal spiral
- **Multiplayer**: Up to 6 players, network
- **vs AI**: Skirmish against AI opponents
- **Tutorial**: Learn game mechanics

## Progression

Players accumulate Watersap to unlock stronger units. Constant trade-off between creating Rootborn, building combat units, casting spells, or unlocking new unit types.

Nice-to-have: unit upgrades, research tree in nexus, score system, cosmetic skins.

---

## TODO Netcode

### Filtre équipe dans SelectionManager
Actuellement le SelectionManager sélectionne toutes les unités sans filtrer par équipe.
En multijoueur, il faudra :
- Empêcher de sélectionner les unités/bâtiments ennemis (ou les afficher en lecture seule)
- Filtrer `SelectGameObject()` pour ne sélectionner que les entités du joueur local
- S'appuyer sur `PlayerManager.Instance.IsLocalPlayer(teamID)` déjà utilisé dans OrderManager

À implémenter quand le système netcode sera en place.
