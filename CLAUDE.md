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
- **UnitMover**: Complex movement system with persistent movement attempts, collision resolution, and chase mechanics
- **UnitSelector**: Handles unit selection with visual feedback via halo effects
- **GameManager**: Central singleton managing unit selection state
- **UnitAnimatorController**: Manages directional movement animations
- **Rootborn**: Specific unit type implementation

### Key Design Patterns
- Singleton pattern for managers (GameManager, GridManager, Pathfinding)
- Component-based architecture for units
- Grid-based spatial partitioning for movement and pathfinding
- Coroutine-based animation and movement systems

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

### Movement System
- Units use persistent movement with retry logic (max 20 attempts to prevent infinite loops)
- **Distance-based priority system** for real-time collision resolution when multiple units target the same cell
- Bridge/obstacle safety checks during movement with fallback mechanisms
- Chase system for dynamic target following with built-in timeout protection
- **Coroutine-based concurrent movement** allowing multiple units to move simultaneously without blocking

### Grid Coordinate System
- Grid is centered around world origin with configurable offset
- Uses Vector2Int for grid positions and Vector3 for world positions
- **Dual cell state management**: blocked cells (terrain) and occupied cells (units) tracked separately
- Real-time cell registration/unregistration during movement to prevent conflicts

### Performance Considerations
- PriorityQueue implementation for efficient pathfinding
- **Coroutine-based movement** to avoid frame rate issues and enable concurrent unit movement
- Spatial partitioning through grid system for O(1) collision detection
- **Movement frame counting** and timeout systems to prevent infinite movement loops
- **Dynamic pathfinding** with maximum radius limits to bound computation time

## Current Development Focus

The project appears to be in active development with focus on:
- Unit movement and pathfinding systems (current branch: units-move)
- Grid-based tactical gameplay mechanics
- Animation system integration

## Notes for Development

- All core systems use French comments but code is in English
- Movement system includes debugging logs with emoji indicators
- Safety checks for null references throughout movement code
- Extensive use of coroutines for smooth movement animations