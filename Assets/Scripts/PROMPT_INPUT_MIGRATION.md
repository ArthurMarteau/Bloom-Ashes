# Migration New Input System — InputManager.cs

## Contexte

Lis `claude.md`. Migration de l'ancien `Input` system vers le **New Input System** Unity dans `InputManager.cs` uniquement.

**Règle d'or** : Seul `InputManager.cs` change. Tous les autres scripts (`SelectionManager`, `OrderManager`, etc.) écoutent les `UnityEvents` de `InputManager` — ils ne doivent PAS être modifiés.

---

## État actuel

`InputManager.cs` utilise l'ancien système via polling dans `Update()` :
- `Input.GetMouseButtonDown(0)` / `Input.GetMouseButton(0)` / `Input.GetMouseButtonUp(0)`
- `Input.GetMouseButtonDown(1)`
- `Input.GetKey(KeyCode.LeftShift)` / `Input.GetKey(KeyCode.RightShift)`
- `Input.mousePosition`

Il expose ces `UnityEvents` consommés par le reste du code :
- `OnLeftClickDown` — position monde
- `OnLeftClickUp` — position écran
- `OnDragStart` — position écran
- `OnDragUpdate` — position écran
- `OnDragEnd` — void
- `OnRightClickDown` — position monde
- `IsShiftHeld` — propriété booléenne

---

## Ce qu'il faut faire

### 1. Créer un Input Actions Asset
Créer `Assets/Input/BloomAndAshesInputActions.inputactions` avec les actions suivantes :

**Action Map : `Gameplay`**
| Action | Type | Binding |
|--------|------|---------|
| `LeftClick` | Button | Mouse/leftButton |
| `RightClick` | Button | Mouse/rightButton |
| `MousePosition` | Value (Vector2) | Mouse/position |
| `Shift` | Button | Keyboard/leftShift + Keyboard/rightShift |

### 2. Réécrire InputManager.cs

Remplacer le polling `Update()` par des callbacks du New Input System.

**Points importants :**
- Utiliser `InputAction` directement (pas `PlayerInput` component — trop couplé)
- Garder **exactement les mêmes UnityEvents publics** (même noms, mêmes types)
- Garder **exactement la même propriété `IsShiftHeld`**
- Garder la logique de drag (seuil `dragThreshold`, `OnDragStart`, `OnDragUpdate`, `OnDragEnd`)
- La conversion `ScreenToWorld` reste identique
- Activer/désactiver les actions dans `OnEnable`/`OnDisable`
- Disposer proprement dans `OnDestroy`

**Structure des callbacks :**
```csharp
// LeftClick.started → équivalent GetMouseButtonDown(0)
// LeftClick.canceled → équivalent GetMouseButtonUp(0)
// RightClick.started → équivalent GetMouseButtonDown(1)
// MousePosition.performed → mis à jour en continu
// La logique drag reste dans Update() mais lit la position depuis le cache MousePosition
```

**`IsShiftHeld`** — deux options, choisir la plus propre :
- Option A : `Shift.IsPressed()` via InputAction
- Option B : garder `Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed` (plus lisible)

### 3. Vérifier la compatibilité

S'assurer que `SelectionManager` et `OrderManager` continuent de fonctionner sans modification — leurs abonnements aux UnityEvents ne changent pas.

---

## Règles

- Commentaires en français avec `//`
- `Debug.Log` informatifs avec emojis
- Pas de `PlayerInput` component — utiliser `InputAction` directement
- `[SerializeField] private InputActionAsset inputActions` pour référencer l'asset
- Activer les actions dans `OnEnable`, désactiver dans `OnDisable`, disposer dans `OnDestroy`
- Tester en Play Mode : clic gauche, clic droit, drag rectangle, shift+clic

---

## Fichiers concernés

- **Modifier** : `Assets/Scripts/Components/Managers/InputManager.cs`
- **Créer** : `Assets/Input/BloomAndAshesInputActions.inputactions`
- **Ne pas toucher** : `SelectionManager.cs`, `OrderManager.cs`, tout le reste
