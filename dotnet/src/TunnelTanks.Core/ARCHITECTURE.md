# TunnelTanks.Core — Simulation Engine

Platform-independent game simulation. Operates entirely on `uint[]` pixel buffers and `ControllerOutput` structs — no SDL, no window, no I/O.

## Module map

```
TunnelTanks.Core/
├── World.cs                    # Simulation orchestrator
├── RepetitiveTimer.cs          # Interval timer
├── Config/
│   ├── Tweaks.cs               # All tuning constants
│   └── RandomGenerator.cs      # Seeded RNG wrapper
├── Terrain/
│   ├── Terrain.cs              # TerrainGrid: pixel ops, change list, drawing
│   └── TerrainPixel.cs         # Pixel enum, TerrainBehavior lookup, classification
├── LevelGen/
│   ├── ToastGenerator.cs       # Procedural cavern generator
│   └── GeneratorUtils.cs       # Shared helpers (lines, fills, borders)
├── Entities/
│   ├── Tank.cs                 # Player tank (movement, dig, shoot, respawn)
│   ├── TankBase.cs             # Fixed bases (reactor, materials)
│   ├── TankBases.cs            # Base collection
│   ├── TankTurret.cs           # Turret aim, fire, cooldown
│   ├── TankList.cs             # Tank collection
│   ├── MachineMaterializer.cs  # Build-mode for machines
│   ├── Sprite.cs               # Short-lived visual effects
│   ├── Projectiles/
│   │   ├── Projectile.cs       # Projectile types (Bullet, Shrapnel, Foam)
│   │   ├── ProjectileList.cs   # Projectile container + ProjectileBehavior strategies
│   │   └── ExplosionDesc.cs    # Explosion factories
│   ├── Machines/
│   │   ├── Machine.cs          # Machine entity (uses MachineBehavior)
│   │   ├── MachineBehavior.cs  # Per-type machine strategy records
│   │   └── MachineList.cs      # Machine collection
│   └── Links/
│       ├── Link.cs             # Power connection (Live/Blocked/Theoretical)
│       ├── LinkPoint.cs        # Network node
│       └── LinkMap.cs          # Power grid graph and solver
├── Collision/
│   ├── CollisionSolver.cs      # Unified collision API (visitor pattern + sectors)
│   ├── Raycaster.cs            # Bresenham line and ray helpers
│   └── WorldSectors.cs         # Spatial partitioning (64px buckets)
├── Input/
│   ├── Controller.cs           # ControllerOutput struct
│   └── TwitchAI.cs             # AI state machine
├── Gui/
│   ├── Screen.cs               # Screen layout, viewports, GUI widgets, crosshair
│   ├── TankView.cs             # Tank-centered world viewport
│   ├── StatusBar.cs            # Energy/health bar
│   ├── FontRenderer.cs         # 4×5 bitmap font
│   └── GuiColors.cs            # GUI color constants
├── Rendering/
│   └── TankSprites.cs          # 9-direction tank bitmaps and color palettes
├── Resources/
│   └── Resources.cs            # Reactor, MaterialContainer, MaterialAmount
└── Types/
    └── Position.cs             # Position, Offset, Size, PositionF, VectorF, DirectionF, Color, Rect, BoundingBox
```

## Extensibility patterns

Three strategy-based patterns make adding new types a single-place change:

### Adding a new projectile type

1. Add enum value to `ProjectileType` in `Projectile.cs`
2. Add one entry to `ProjectileList.BuildBehaviors()` with advance logic and draw color
3. Add a factory method to `Projectile` (optional convenience)

```csharp
// In ProjectileList.BuildBehaviors():
behaviors[(int)ProjectileType.Laser] = new(AdvanceLaser, Tweaks.Colors.LaserBeam);
```

### Adding a new machine type

1. Add enum value to `MachineType` in `Machine.cs`
2. Add one entry to `MachineBehaviors` with interval, action, color, and build cost

```csharp
public static readonly MachineBehavior Turret = new(
    TimeSpan.FromMilliseconds(Tweaks.Machine.TurretIntervalMs),
    FireAtNearbyEnemies,
    Tweaks.Colors.Turret,
    new MaterialAmount(Tweaks.Machine.TurretCost, 0));
```

### Adding a new terrain type

1. Add enum value to `TerrainPixel`
2. Add one entry to `Pixel.BuildBehaviorTable()` with all classification flags and display color
3. All `Pixel.Is*` methods automatically work via the lookup table

```csharp
Set(TerrainPixel.Lava, new(true, false, false, false, false, false, false, false, new Color(0xff, 0x40, 0x00)));
```

### Collision queries for new entity types

`CollisionSolver.TestPoint` accepts optional visitor callbacks per entity type.
To add a new collidable entity type, add a new optional parameter:

```csharp
public bool TestPoint(Position pos,
    Func<Tank, bool>? onTank = null,
    Func<Machine, bool>? onMachine = null,
    Func<Turret, bool>? onTurret = null,      // new
    Func<TerrainPixel, bool>? onTerrain = null)
```

## World — simulation orchestrator

`World.cs` owns all subsystems and drives `Advance(Func<int, ControllerOutput> getInput)`:

```
CollisionSolver    Rebuild sector indices for broad-phase queries
RegrowPass         Cellular automata: Blank→DirtGrow→DirtHigh/Low
Projectiles        Movement, terrain impact, explosions, spawn shrapnel
Tanks              Input → turret → HandleMove → DigTunnel → shoot
Machines           Harvest energy, charge reactor, link-powered
Sprites            Decrement lifetime, remove expired
Bases              Reactor recharge from materials
LinkMap            Solve power graph via Bresenham visibility
```

### Profiling

`SimulationProfile` accumulates `Stopwatch` readings per subsystem and prints every 100 frames:
```
[Profile] regrow=X.XXX proj=X.XXX tanks=X.XXX harv=X.XXX spr=X.XXX bases=X.XXX links=X.XXX | total=X.XXX ms
```

## Terrain

### Data model

- `_data: TerrainPixel[]` — flat array, row-major, `width × height`
- `_changeList: List<Position>` — positions modified since last `DrawChangesToSurface`
- `GetPixel(x, y)` / `SetPixel(Position, TerrainPixel)` — SetPixel appends to change list

### TerrainPixel enum and TerrainBehavior

Each `TerrainPixel` value maps to a `TerrainBehavior` record via `Pixel.GetBehavior()`:

| Flag | Meaning |
|------|---------|
| `BlocksMovement` | Impassable (Rock, Concrete, Base walls) |
| `SoftCollision` | Passable but counts as collision (Dirt) |
| `Diggable` | Cleared by tank dig (Dirt, DirtGrow) |
| `Mineral` | Yields minerals (Rock, Concrete) |
| `Concrete` | Is concrete specifically |
| `Energy` | Collectible energy |
| `Base` | Player base wall |
| `Scorched` | Explosion decal |
| `DisplayColor` | Render color |

Derived properties: `IsAnyCollision`, `Torchable`, `Rock`, `Dirt`.

All `Pixel.Is*()` methods are single-lookup into the behavior table (256-element array indexed by byte value).

### Materialization

`MaterializeTerrain(int? seed)` converts generator markers to final values:
- `LevelGenDirt` → `DirtHigh` or `DirtLow` (50/50 random)
- `LevelGenRock` → `Rock`
- `LevelGenMark` → `Blank`

### Rendering to pixel buffer

- `DrawAllToSurface(uint[] surface)` — full redraw, used at init
- `DrawChangesToSurface(uint[] surface)` — only changed positions (via `_changeList`)
- Color mapping: `Pixel.GetColor(TerrainPixel)` returns exact C++ hex values

## Level generation (Toast algorithm)

`ToastGenerator.Generate(Size, int? seed)` → `(TerrainGrid, Position[] spawns)`:

1. **Random points**: Place `N` points within padded bounds
2. **MST**: Prim's algorithm to connect all points
3. **Bresenham paths**: Draw lines between MST-connected points
4. **Expansion**: Random directional fills to widen tunnels
5. **Smoothing**: Cellular automata passes (dirt neighbors ≥ threshold → fill)
6. **Border**: Solid walls around edges
7. **Connectivity check**: BFS to ensure all spawn points are reachable
8. **Spawn placement**: Distribute player spawn points along paths

## Tank movement and digging

### HandleMove flow

```
newPos = position + speed
collision = TestCollision(terrain, newPos, direction)
if collision:
    DigTunnel(newPos, torchUse)
    if NOT (torchUse AND shooting in movement direction):
        return                    ← pause movement (dig frame)
    retest collision after dig
    if still blocked: return
position = newPos
```

This creates the core "torch" mechanic: shooting ahead while moving digs and moves simultaneously; moving without shooting pauses on each dig frame.

### DigTunnel shape

Fixed 7×7 area minus 4 corners, centered on tank:
```
 .xxxxx.
 xxxxxxx
 xxxxxxx
 xxxxxxx
 xxxxxxx
 xxxxxxx
 .xxxxx.
```

For each pixel in the shape:
- **Diggable** (dirt, decal, energy, scorched): always cleared to `Blank`; minerals add to resources
- **Torchable** (rock): cleared only if `torchUse` AND `Random.Shared.Next(1000) < DigThroughRockChance`

### Turret

`TankTurret` manages:
- `Direction` (DirectionF) — aim heading, set from mouse or keyboard
- `IsShooting` — whether fire button is held
- `_cooldownRemaining` — frames until next shot
- Barrel tip position (pixel offset from center, used for projectile spawn)
- Visual: colored tip pixel drawn at barrel end

## Projectiles

### Strategy pattern

`ProjectileBehavior` records define per-type behavior (advance logic + draw color).
All behaviors are registered in `ProjectileList.Behaviors[]`, indexed by `(int)ProjectileType`.
The `Advance` loop dispatches via array lookup — no switch statements, no virtual calls.

### Types

| Type | Behavior |
|------|----------|
| `Bullet` | Fast, linear, explodes on terrain/tank hit (via CollisionSolver) |
| `Shrapnel` | Short-lived, spawned by explosions |
| `ConcreteFoam` | Spawns concrete terrain on impact |
| `DirtFoam` | Spawns dirt terrain on impact |

### ProjectileList

- `_projectiles`: active list
- `_pending`: newly spawned this frame (added at end of Advance to avoid mutation during iteration)
- `Advance(CollisionSolver)`: move each projectile, test collision via solver, handle impact
- `Draw`: render colored pixels at projectile positions using per-type DrawColor

### Explosions (`ExplosionDesc`)

Factory methods create explosion configs:
- `DirtExplosion` — small, few shrapnel
- `NormalExplosion` — medium, moderate shrapnel
- `DeathExplosion` — large, many shrapnel
- `Fan` — directional spray

## Machines

### Strategy pattern

`MachineBehavior` records define per-type behavior (action interval, action logic, color, build cost).
Adding a new machine type = one entry in `MachineBehaviors`.

- **Harvester**: Planted near energy deposits. Drains energy pixels within radius, adds to owner's reactor.
- **Charger**: Planted on power link. Charges owner's reactor from the grid.
- **States**: `Template` (carried by tank) → `Transporting` → `Planted` (active)
- `MachineMaterializer`: Build-mode input handler, manages placement preview

## Links (power grid)

- **LinkPoint**: Node with type (Base, Machine, Transit, Controllable), position, enabled/powered state
- **Link**: Edge between two LinkPoints; type is Live (clear path), Blocked (obstructed), or Theoretical (possible)
- **LinkMap.Advance**: Re-solves graph each frame:
  1. For each possible link pair, Bresenham ray-test for obstructions
  2. Mark links Live or Blocked
  3. Propagate power from bases outward through Live links

## Collision

### CollisionSolver (visitor pattern)

`CollisionSolver.TestPoint(pos, onTank?, onMachine?, onTerrain?)`:
- Central collision API with optional visitor callbacks per entity type
- Callers pass only the callbacks they care about
- Tank queries use `WorldSectors` broad-phase (only nearby sectors checked)
- Machine queries use linear scan (few entities)
- Terrain queries are direct pixel lookups

Rebuilt each frame via `Update(TankList, MachineList)` which populates sector indices.

### WorldSectors

64px spatial partitioning buckets. Entities are indexed by sector position.
`ForEachNearbyEntity(pos, visitor)` iterates the 3×3 neighborhood around a position.

### Raycaster

Bresenham line helpers with early-exit (`BresenhamLineAny`) and full-walk (`BresenhamLine`) variants.

## Input

### ControllerOutput

```csharp
struct ControllerOutput
{
    Offset MoveSpeed;        // (-1..1, -1..1) cardinal movement
    bool ShootPrimary;       // fire button
    DirectionF AimDirection; // turret heading (from mouse or keyboard)
    bool BuildPrimary;       // place/cycle machine
}
```

### TwitchAI

State machine for AI-controlled tanks:
- **Start**: Initial delay at base
- **ExitBaseUp/Down**: Leave base through door
- **Twitch**: Random movement + shooting (main behavior)
- **Return**: Navigate back toward base
- **Recharge**: Wait at base to refill reactor

Accepts optional `int? seed` for deterministic behavior in tests.

## GUI

### Screen layout

`Screen.Draw(composite, compositeW, compositeH, screen, screenW, screenH)`:

- **1 player**: Single TankView filling most of the render surface (320×200)
- **2 players**: Two side-by-side TankViews with divider

Widgets drawn on top:
- `StatusBar` — horizontal bars for energy (yellow) and health (green)
- `LivesDots` — remaining lives as 2×2 colored dots
- `LetterBitmap` — E/H labels for status bars
- `ResourceOverlay` — dirt/mineral counts near base display
- `Crosshair` — 4-pixel cross at mouse world position

### TankView

- Centers viewport on owning tank's position
- `WorldToScreen(Position)` / `ScreenToWorld(int, int)` — coordinate transforms
- Blits a rect from the composite world surface into the screen buffer
- Shows static overlay when tank is dead/respawning

### FontRenderer

4×5 pixel bitmap font. Supports digits 0-9, letters A-Z, and a few symbols. Used for resource counts and labels.

## Configuration (`Tweaks.cs`)

Nested static classes mirror C++ `tweak::` namespaces:

| Section | Key constants |
|---------|---------------|
| `Perf` | `TargetFps` (24), `SectorSize` (64) |
| `Screen` | `WindowSize` (1920×1200), `RenderSurfaceSize` (320×200) |
| `World` | `AdvanceStep`, `DirtRecoverInterval`, `DirtRegrowSpeed`, `DigThroughRockChance` |
| `Base` | `MinDistance`, `BaseSize`, `DoorSize`, reactor and material capacities |
| `Tank` | `MaxLives`, `RespawnDelay`, `TurretDelay`, energy/health/resource capacities |
| `Weapon` | `CannonBulletSpeed`, barrel speeds, `BulletDamage` |
| `Machine` | Reactor caps, intervals, harvest range, build costs |
| `Explosion` | `Dirt/Normal/Death` (ShrapnelCount, Speed, Frames), `ChanceToDestroy*` |
| `Colors` | Centralized color constants for all entities and effects |
| `LevelGen` | `BorderWidth`, `DirtTargetPercent`, `TreeSize`, `SmoothingSteps` |
