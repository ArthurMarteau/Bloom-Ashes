# Bloom & Ashes - Contexte Projet

**RTS 2D temps réel** · Unity 6000.0.31f1 (URP, 2D pixel perfect) · Multijoueur réseau jusqu'à 6 joueurs

## Architecture - Clean Architecture Unity

Séparation stricte **Data / Systems / Components** :

- **Data/** → ScriptableObjects (configuration, stats)
- **Systems/** → POCO (logique pure, testable sans Unity)
- **Components/** → MonoBehaviours (cycle de vie Unity, events)
- **Shared/** → Enums, constantes, extensions

### Règles

1. Testable sans Unity → POCO dans Systems/
2. Données configurables → ScriptableObject dans Data/
3. Cycle de vie Unity → MonoBehaviour dans Components/
4. Jamais de GameObject dans la logique pure
5. Interfaces uniquement quand c'est justifié pour respecter la Clean Architecture
6. Pas de singleton static (incompatible netcode) → MonoBehaviour singleton ou injection

### Patterns

- **Data-Driven** : ScriptableObjects pour toute config
- **Event-Driven** : UnityEvents, communication découplée
- **Registry Pattern** : Auto-enregistrement des entités
- **Broker Chain** : Modificateurs buffs/debuffs (à venir)

### Organisation dossiers

Hybride couche archi + sous-dossiers feature (Data/Units/, Systems/Grid/, Components/Managers/, etc.)

## Règles non négociables

1. **DRY** : avant de créer une méthode/classe/helper, chercher l'équivalent existant (voir ARCHITECTURE.md → points d'entrée canoniques) et le réutiliser ou l'étendre.
2. **YAGNI** : aucun champ, paramètre, API ou abstraction « pour plus tard » sans demande explicite.
3. **KISS** : la solution la plus simple qui respecte l'architecture ; pas de nouveau système si un existant couvre le besoin.
4. **Simulation lockstep** : décisions gameplay en SimTick, temps en ticks (`TICKS_PER_SECOND`), RNG via `SimulationRandom`, actions joueur via `GameCommand`, spawn/destroy via `EntitySpawnManager`, entités référencées par `entityID`, jamais `transform.position` dans la logique — détail : GDD.md section « Netcode (Lockstep) ».
5. **CreateAssetMenu** : `menuName` commence toujours par `"Bloom and Ashes/"` (graphie exacte, unique).
6. **Logs** : `GameLogger` + `Category` existante (en créer une = la justifier), interpolations gardées par `IsEnabled` en hot path. Jamais `Debug.Log` direct.
7. **Events** : tout `AddListener` a son `RemoveListener` symétrique (OnDestroy).
8. **Index** : si tu crées un point d'entrée, un manager ou un dossier → ajouter une ligne à ARCHITECTURE.md.
9. **Fichiers** : un fichier = une responsabilité ; pas de dossier pour un seul script sans famille prévue à court terme.

## Conventions

- **Variables** en anglais, noms complets sans abréviations
- **Commentaires** en français avec `//` devant chaque méthode
- **[ContextMenu]** pour tests rapides en éditeur
- **Migration progressive** : jeu fonctionnel à chaque étape

## Stack

- Unity 6000.0.31f1, URP, 2D pixel perfect
- UnityEvents pour communication
- Unity Input System (ancien système utilisé)

## Doc de Référence

- **GDD.md** → Game design, factions, mécaniques, unités, règles Netcode (Lockstep)
- **ARCHITECTURE.md** → Carte du projet et points d'entrée canoniques
