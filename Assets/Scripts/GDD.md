# Bloom & Ashes — Game Design Document

## Vision

Old school 2D RTS inspired by **Blood and Magic** (1996), modernized with dynamic gameplay, network multiplayer (up to 6 players), and deeper mechanics. Designed for mobile-first accessibility with grid-based movement and limited unit micro-management.

## Universe

- **Theme**: Medieval-fantasy centered on plant magic and druids
- **Setting**: The Shifting Groves of Cirgus
- **Visual style**: 3D Low Poly to 2D Line Art, old school 8-bit feel
- **Audio**: 8-bit music per map, 8-bit sound effects
- **Single resource**: "Watersap" — collected by Rootborn in static position (max 300 per player)

## Core Mechanics

- Top-down view, tile-by-tile movement in 8 directions
- Real-time: all units move simultaneously
- One game object per tile
- Simplified but tactical management, limited unit count
- Fast and strategic engagements
- Abilities cost Watersap directly (army building vs spell casting trade-off)
- **Ability Auto-Cast**: All active abilities can be toggled to auto-cast. When enabled, the unit uses the ability automatically when conditions are met (target in range, enough Watersap).
- **Sacrifice**: Any unit can be sacrificed (active ability) to grant 20 Watersap. Strategic use: sacrifice a dying unit before death to recover resources.
- Units size 1x1. Building 1x1 or more.
- Pas de virgule pour les stats. (Except Path Ground)

## Map

- **Target size**: 80x80 tiles
- **Biomes**: Meadows, Gardens, Dark Forest, Caves, Scorched Lands, Volcano, Desert, Jungle, Beach, Ocean Palace
- One game object per tile

## Technical Implementation (Game Design)

- **Grid**: 80x80 tiles, A* pathfinding with MinHeap
- **Prefabs**: 1 prefab per unit per faction, stats via ScriptableObjects (no generic prefab)
- **Rootborn → combat**: Destroy + Instantiate new prefab (fixed type via SO, not mutable state)
- **Input**: Migration planned to New Input System with netcode

---

## Rootborn (Common Worker Unit)

The base unit shared by all factions, created at the Nexus.

**Description**:

*A humble golem of mud and roots, the Rootborn is the backbone of every army. It gathers Watersap, builds structures, and sacrifices itself to birth combat units. Weak in battle but indispensable for economy.*

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
- **Static Collection**: While stationary, collects 1 Watersap every 5 seconds
- **Source Collection**: 3 Rootborn on a Watersap source collect faster/more
- **Build**: 4 Rootborn on a foundation site sacrifice themselves to build a faction building
- **Transform**: Adjacent to a building, transforms into a chosen combat unit (becomes a seed, then hatches)

---

## Nexus (Common Building)

| Stat    | Value        |
| ------- | ------------ |
| HP      | 1            |
| Defense | Invulnerable |
| Size    | 1x1          |

**Function**: 
- Spawns Rootborn units. Reverse battle unit in Rootborn for 20 Watersap. 
- Unique state : Iswalkable by every units ans IsUnvulnerable.
- Unique ground : +2 DEF and +2 ATT. 1 DMG/sec on ennemies.


---

## The 5 Factions

### 1. Mycelian Champions

**Identity**: Mushrooms, poison, spores
**Biomes**: Dark Forest, Caves
**Strength**: Very versatile — healing, control, defense, damage, flying unit
**Weakness**: Doesn't excel at anything
**Building**: Great Oak (2x2)

#### Units

**Luminescent Mycena** — Melee / Support

**Description**:

*A small glowing mushroom that serves as the Mycelian Champions' frontline healer. Fragile but essential, it keeps allies alive with regenerating spores.*

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


**Puffball Scout** — Ranged DPS / Control / Scout

**Description**:

*A lightweight shroom unit that excels at reconnaissance and area denial. It lays paralyzing traps to control the battlefield from a distance. Can hide in bushes.*

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
| Train    | 25   | 25         |
| Research | 50   | 0          |

| Ability       | Type    | Cost | Cooldown | Area Effect | Damage | Time Effect      | Description                                                            |
| ------------- | ------- | ---- | -------- | ----------- | ------ | ---------------- | ---------------------------------------------------------------------- |
| Puffball Trap | Active  | 10   | 5        | 1x1         | 0      | Once             | Places a trap that paralyzes its victim for 3 seconds. (Only Non-ally) |
| Hide          | Passive | 0    | 0        | 1x1         | 0      | 2s before effect | Can hide in bushes                                                     |


**Bolete Golem** — Tank

**Description**:

*A massive creature made of hardened mycelia and rock. Slow but nearly unbreakable, it absorbs punishment so its allies don't have to.*

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


**Flying Death Cap** — Ranged DPS / Poison / Flying

**Description**:

*A toxic airborne mushroom that spews venomous clouds from above. Its spore mist is devastating against paralyzed targets, making it a deadly combo partner for the Puffball Scout.*

| Stat          | Value  |
| ------------- | ------ |
| HP            | 30     |
| Attack Damage | 4      |
| Attack Speed  | 2      |
| Attack Range  | 1      |
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
**Building**: Great Solar Crystal (2x2)

#### Units

**Thistle Warrior** — Melee

**Description**:

*A sturdy thorn-covered fighter, the Thistle Warrior is the Charmers' reliable frontline. It hits hard for its cost and can take a beating thanks to its natural bark armor.*

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

*An elegant airborne flower that fires thorn projectiles piercing through everything in their path. Fragile but lethal in groups, it excels at harassing from angles enemies can't reach.*

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

*A radiant flower mage that channels sunlight into devastating beams. Its Blazing Mirror ability makes it a nightmare for ranged enemies, turning their own projectiles against them.*

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

*The most manipulative unit on the battlefield. The Dahlia's hypnotic pollen can turn the strongest enemy into a temporary ally, swinging fights in an instant. Extremely fragile — protect at all costs.*

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
**Building**: Great Stone (2x2)

#### Units

**Cactus Warrior** — Melee

**Description**:

*A tough desert fighter covered in razor-sharp spines. Cheap to produce and hard to kill, the Cactus Warrior punishes anyone who dares attack it in close combat.*

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

*A carnivorous plant that spits corrosive acid from a distance. Its shots slow enemies down, making them easy prey for the Venus Assassin to finish off.*

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

*A lightning-fast carnivorous plant that strikes from the shadows. Its deadly bite deals massive damage to slowed targets, making it the perfect follow-up to the Nepenthes' acid shots.*

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

*An ancient desert plant of immense resilience. It regenerates constantly, making it almost impossible to wear down. Slow but nearly unkillable in prolonged fights.*

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
**Building**: Great Furnace (2x2)

#### Units

**Charred Branch** — Melee

**Description**:

*A fast and reckless burned warrior. Cheap and expendable, it rushes into enemy lines and can detonate itself in a devastating explosion, taking everyone nearby with it.*

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

*A howling ball of fire that hurls flaming projectiles at high speed. The Scorched's main ranged damage dealer — fast, deadly, but paper-thin.*

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

*A suicidal flying flame that locks onto a single target and won't stop until it's dead — or it is. If it succeeds, a Rootborn rises from its ashes. The ultimate kamikaze unit.*

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

*A smoldering tree trunk that radiates intense heat. Its Incandescent Nova terrifies and burns nearby enemies, making it a fearsome area-denial bruiser.*

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
**Building**: Great Shell (2x2)

#### Units

**Lily Pad Guard** — Melee

**Description**:

*A broad-leafed aquatic warrior with a built-in shield. It can block incoming projectiles at minimal cost, making it the perfect bodyguard for the faction's fragile spellcasters.*

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

*A sacred water flower that channels healing tides and offensive water bolts. With the cheapest heal in the game, it's the backbone of any Abyss army — versatile, efficient, indispensable.*

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

*A sinister deep-sea kelp that drags units toward it with crushing tentacles. It can pull allies to safety or yank enemies into kill zones — a master of battlefield positioning.*

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

*An imposing coral fortress that moves at a glacier's pace but hits like a tidal wave. Its Abyssal Wave clears entire lines of enemies, making it devastating in narrow passages.*

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

| Terrain         | Path Cost  | Move Speed Mod | Attribut Mod | Notes                                         |
| --------------- | ---------- | -------------- | ------------ | --------------------------------------------- |
| Grass           | 1.0        | 0              | 0            | Default terrain                               |
| Dirt            | 1.0        | 0              | 0            | Default terrain                               |
| Path            | 0.5        | +1             | 0            | A* prefers paths for faster routes            |
| Rocky           | 2.0        | -1 (min 1)     | +2 DEF       | Slows but protects                            |
| Swamp           | 1.5        | -1 (min 1)     | 0            | Slow, potential poison for non-Abyss          |
| Brambles        | 1.5        | -1 (min 1)     | +1 DEF       | Deals 1 dmg/cell to non-Charmers              |
| Sand            | 1.5        | -1 (min 1)     | 0            | Desert terrain                                |
| Scorched        | 1.0        | 0              | 0            | Deals 1 dmg/sec to non-Scorched               |
| Lava            | 1.0        | 0              | 0            | Kills instantly to non-Scorched               |
| Water           | Impassable | —              | —            | Only aquatic/flying units                     |
| Mountain        | Impassable | —              | —            | Only flying units                             |
| Ravine          | Impassable | —              | —            | Only flying units. Kills instantly.           |
| Bush            | 1.0        | 0              | +1 DEF       | Provides camouflage for Puffball Scout        |
| Watersap Source | 1.0        | 0              | +1 DEF       | Doubles Watersap collection for the Rootborn. |
| Nexus           | 1.0        | 0              | +2 ATT       | Deals 1 dmg/sec to non-Allies                 |

### Faction-Terrain Interactions

| Faction                | Terrain Bonus                                                                 |
| ---------------------- | ----------------------------------------------------------------------------- |
| Mycelian Champions     | Immune to Forest/Swamp penalties. +1 Defense in Forest.                       |
| Golden Garden Charmers | +1 Move Speed on Grass. Immune to Grass/Path/Brambles penalties.              |
| Wild Fangs             | Immune to Sand/Rocky penalties. +1 Defense on Rocky.                          |
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

## Buildings

| Building      | Faction             | HP  | Defense      | Size | Construction             |
| ------------- | ------------------- | --- | ------------ | ---- | ------------------------ |
| Nexus         | All                 | 1   | Invulnerable | 1x1  | Starting building        |
| Great Oak     | Mycelian Champions  | 100 | 3            | 2x2  | 4 Rootborn on foundation |
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
