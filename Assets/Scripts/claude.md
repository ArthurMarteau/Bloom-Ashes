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

## Conventions

- **Variables** en anglais, noms complets sans abréviations
- **Commentaires** en français avec `//` devant chaque méthode
- **Debug.Log** informatifs avec contexte et emojis
- **[ContextMenu]** pour tests rapides en éditeur
- **Proposer plan avant code**, valider l'approche
- **Migration progressive** : jeu fonctionnel à chaque étape

## Stack

- Unity 6000.0.31f1, URP, 2D pixel perfect
- Blender v5.0
- UnityEvents pour communication
- Unity Input System (ancien système utilisé)

## Doc de Référence

- **GDD.md** → Game design, factions, mécaniques, unités
