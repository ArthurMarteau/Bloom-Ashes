# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

"Bloom and Ashes" is a Unity 2D tactical strategy game built with Unity 6000.0.31f1. The project uses Universal Render Pipeline (URP) for rendering and features a grid-based movement system in real-time with pathfinding for tactical unit combat.

## Core Architecture

### Grid System
- **GridManager**: Central singleton managing a 128x128 grid with 1x1 cell size, handles world-to-grid conversions and cell state management
- **GridObstacle**: Manages terrain obstacles that block movement
- **Pathfinding**: A* pathfinding implementation with dynamic obstacle avoidance

### Unit System
- **MovementCore**: Central movement system orchestrating all unit movement with pending order management
- **PathfinderFollower**: Handles path execution and dynamic target chasing with coroutine-based movement
- **CollisionHandler**: Manages collision detection, pushback mechanics, and smooth cell-to-cell movement
- **UnitSelector**: Handles unit selection with visual feedback via halo effects
- **UnitClickMover**: Mouse input handler for right-click movement and chase commands
- **GameManager**: Central singleton managing unit selection state
- **UnitAnimatorController**: Manages directional movement animations with idle states
- **Rootborn**: Specific unit type implementation with unique movement delays

### Key Design Patterns
- Singleton pattern for managers (GameManager, GridManager, Pathfinder)
- Component-based architecture for units with composition over inheritance
- Observer pattern for unit state coordination between MovementCore, PathfinderFollower, and CollisionHandler
- Grid-based spatial partitioning for movement and pathfinding
- Coroutine-based animation and movement systems with concurrent execution
- State management with pending order queuing for interrupt handling

## Development Workflow

### Building and Testing
This is a Unity project - use Unity Editor for building and testing:
- Open project in Unity 6000.0.31f1 or later
- Use Unity's built-in Play mode for testing
- Build through Unity's File > Build Settings

### Project Structure
```
Assets/Scripts/
├── Core/           # Core game systems (managers, grid, pathfinding)
├── Units/          # Unit-related components (movement, selection, behaviors)
├── Animation/      # Animation controllers and systems
├── Utilities/      # Utility classes (PathNode, PriorityQueue)
├── AI/             # AI systems (currently empty)
├── Behaviors/      # Unit behaviors (currently empty)
├── Conflict/       # Combat systems (currently empty)
├── FX/             # Visual effects (currently empty)
├── Input/          # Input handling (currently empty)
└── UI/             # User interface (currently empty)
```

### Key Dependencies
- Unity Input System (1.11.2)
- Universal Render Pipeline (17.0.3)
- Unity Test Framework (1.4.5)
- 2D Feature Set with pixel perfect rendering

### Third-Party Tools
- Code Assist package for enhanced IDE integration with Visual Studio/VS Code

## Important Implementation Details

### Real-Time Strategy Requirements
This is a **real-time tactical strategy game** where multiple units can move simultaneously. Key constraints:
- **Concurrent Movement**: All units can move at the same time, requiring robust conflict resolution
- **Real-Time Conflict Management**: Collision detection and resolution must handle multiple units competing for the same cells
- **Performance Optimization**: Movement requests must be limited to prevent infinite loops and system crashes
- **Retry Logic**: Maximum 20 attempts per movement to prevent endless pathfinding loops

### Movement System Architecture
- **Three-layer movement system**: MovementCore (orchestrator) → PathfinderFollower (path execution) → CollisionHandler (movement mechanics)
- **Pending order queue**: New movement commands are queued when units are mid-movement, preventing teleportation
- **Smooth interpolation**: MoveToPositionSmoothly prevents jarring position jumps during movement interrupts
- **Chase system**: Dynamic target following with automatic path recalculation when targets move
- **Rootborn delays**: Special handling for unit-specific movement delays (1-second pause before movement)
- **Collision resolution**: Pushback mechanics and closest-cell fallback for blocked movement

### Grid Coordinate System
- Grid is centered around world origin with configurable offset
- Uses Vector2Int for grid positions and Vector3 for world positions
- **Dual cell state management**: blocked cells (terrain) and occupied cells (units) tracked separately
- Real-time cell registration/unregistration during movement to prevent conflicts

### Performance Considerations
- **Cached pathfinding**: Dictionary-based path caching with invalidation system to reduce A* recalculations
- **Coroutine-based movement**: All movement operations use coroutines to prevent frame drops and enable concurrency
- **Spatial partitioning**: Grid system provides O(1) collision detection and cell state queries
- **BFS pathfinding optimization**: FindClosestFreeCell uses breadth-first search with radius limits (default: 10 cells)
- **Movement state tracking**: Efficient state queries (IsMoving(), GetCurrentState()) prevent redundant operations
- **Anti-teleportation**: Distance checks prevent units from jumping more than 1.5 grid units during movement interrupts

## Current Development Focus

The project is in active development with recent architectural improvements:
- **Completed**: Core movement system refactor with three-layer architecture
- **Completed**: Advanced pathfinding with caching and dynamic target following
- **Active**: Animation system integration with directional movement states
- **Planned**: AI systems (AI/ folder exists but empty)
- **Planned**: Combat mechanics (Conflict/ folder exists but empty)
- **Planned**: UI systems for unit selection and commands

## Notes for Development

- **Mixed language codebase**: French comments but English code structure and variables
- **Comprehensive debugging**: Extensive Debug.Log statements with emoji indicators (🗺️, 🎯, ⚠️, ✅) for system identification
- **Safety-first design**: Null reference checks, bounds validation, and distance verification throughout movement code
- **Input handling**: Right-click for movement commands, left-click for unit selection
- **Animation states**: Idle, walking with 8-directional movement (N, NW, W, SW, S, etc.)
- **Component dependencies**: Units require MovementCore + PathfinderFollower + CollisionHandler + UnitAnimatorController for full functionality