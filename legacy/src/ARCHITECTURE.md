# C++ Engine Architecture (`src/`)

The original C++ implementation. Built with MSVC (VS 2022/2026), C++20, SDL2, and the `ecs.hpp` library.

## Entry point and game loop

`main.cpp` → `GameMain()`:
1. Parse CLI (`--level`, `--seed`, `--single`, `--large`, `--fullscreen`, `--no-ai`, etc.)
2. `gamelib_init()` → SDL init
3. `CreateGameSystem(video_config)` → SDL Window, Renderer, Cursor, BmpDecoder, FontRenderer
4. `global_game = make_unique<Game>(config)`
5. Level generation → `world` + bases
6. `gamelib_main_loop(advance_func)` — fixed-step loop with `smart_wait` for 24 FPS

`gamelib_main_loop` (in `gamelib/sdl/control.h`) calls `advance_func()` then `RenderFrame()` each tick.

### Game::AdvanceStep (`game.cpp`)

1. Drain SDL events (Resize, ToggleFullscreen, Exit)
2. `world->Advance()`
3. `terrain.DrawChangesToSurface(terrain_surface)`
4. `world->Draw(objects_surface)`
5. `screen->DrawCurrentMode()` (composites to screen surface)
6. Profile report every 100 frames

## Subsystem execution order (`world.cpp`)

```
World::Advance():
  1. RegrowPass          — parallel dirt recovery
  2. projectile_list     — bullets, shrapnel, foam
  3. tank_list           — player/AI tanks (move, dig, shoot)
  4. harvester_list      — energy collection machines
  5. sprite_list         — transient visual effects
  6. tank_bases          — base reactor charging
  7. link_map            — power grid solve
  8. terrain.Advance     — change list commit
  9. entity_system       — ECS step
```

## Entity–Component System (ECS)

Uses `ecs.hpp` (submodule `libs/ecs`). Global registry: `entity_system.registry`.

### Components

| Component | Description |
|-----------|-------------|
| `Position`, `PositionF` | World coordinates (int or float) |
| `BoundingBox`, `BoundingBoxF` | Axis-aligned bounding box |
| `BitmapCollision` | Pixel-level collision mask |
| `BoundingBoxCollision` | Box collision flag |
| `OccupiedSector` | Spatial bucket membership |
| `ColorLookup` | Palette index → ARGB mapping |
| `IndexedBitmap` | Sprite data as palette indices |

### Aspects (component bundles)

- `BitmapCollidable = aspect<Position, BitmapCollision>`
- `BoundingBoxCollidable = aspect<Position, BoundingBox, BoundingBoxCollision>`
- `PaletteRenderable = aspect<ColorLookup, IndexedBitmap>`

### Systems

- `UpdateSectorPositions` — recomputes `OccupiedSector` from position/bbox
- `CollisionSystem` — stub (collision done manually via `CollisionSolver`)

## Tank and movement

### Class hierarchy

```
Controllable (controllable.h)
  ├── LinkPointSource, Reactor, ecs::entity
  ├── HandleMove(terrain, speed, torchHeading, torchUse)
  └── DigTankTunnel(terrain, center)
Tank : Controllable (tank.h)
  ├── TankTurret, MachineMaterializer
  ├── Advance() → poll controller → turret → HandleMove → shoot
  └── Draw()
```

### HandleMove logic

1. Compute `newPos = position + speed`
2. `TestCollision(terrain, newPos, direction)` — pixel walk in 7×7 sprite footprint
3. If collision:
   - `DigTankTunnel(newPos)` — clear 7×7 minus corners; rock only if `torchUse` + random chance
   - If NOT torching in movement direction → **return without moving** (dig-pause)
   - If torching in movement direction → retest collision → move if clear
4. If no collision → move freely

### DigTankTunnel shape

Fixed 7×7 area centered on tank, minus the 4 corners:
```
 .xxxxx.
 xxxxxxx
 xxxxxxx
 xxxxxxx
 xxxxxxx
 xxxxxxx
 .xxxxx.
```

## Terrain

`terrain.h` wraps `Container2D<TerrainPixel>`:

- **SetPixel / GetPixel**: Direct voxel access
- **CommitPixel**: Records position in `change_list` for incremental rendering
- **MaterializeLevelTerrain**: Converts generator markers (LevelGenDirt → DirtHigh/DirtLow randomly)
- **DigTankTunnel**: Inline clearing with torch/rock logic
- **DrawChangesToSurface / DrawAllToSurface**: Writes ARGB to surface from terrain pixels
- **ForEachVoxelParallel**: Slices terrain by X-columns for parallel processing

### TerrainPixel values

Blank, DirtHigh, DirtLow, DirtGrow, Rock, DecalHigh, DecalLow, ConcreteLow, ConcreteHigh, EnergyLow/Medium/High, Base, LevelGenDirt, LevelGenRock, LevelGenMark, Scorched.

## Level generation

All generators implement `LevelGenerator::Generate(Size)` → `GeneratedLevel` (terrain + spawns).

| Generator | Algorithm |
|-----------|-----------|
| Simple | Random dirt fill |
| Maze | Recursive maze carving |
| Braid | Looping maze |
| **Toast** | MST + Bresenham + expansion + cellular automata (default) |

The Toast algorithm:
1. Place N random points
2. Build MST via Prim's algorithm
3. Draw Bresenham lines between connected points
4. Random directional expansion passes
5. Cellular automata smoothing (configurable steps)
6. BFS to ensure connectivity

## Rendering pipeline

```
terrain_surface (WorldRenderSurface)    ← DrawChangesToSurface (incremental)
objects_surface (WorldRenderSurface)    ← world->Draw (projectiles, tanks, bases, machines, sprites, links)
    ↓
Screen::DrawCurrentMode():
  ├── terrain_surface.OverlaySurface(&objects_surface)
  ├── TankView::Draw (blit viewport from terrain_surface into screen)
  ├── StatusBar, Crosshair, LivesLeft, ResourceDisplay
  ├── terrain.CommitPixels(objects_surface.GetChangeList())
  └── objects_surface.Clear()
    ↓
ScreenRenderSurface
    ↓
SdlRenderer::RenderFrame:
  SDL_UpdateTexture → SDL_RenderCopy → SDL_RenderPresent
```

### Surface types

- `Surface` — base: raw `uint32_t*` array
- `ScreenRenderSurface` — screen-sized, no change tracking
- `WorldRenderSurface` — world-sized, maintains `change_list` for dirty rects

## Input system

### Controller hierarchy

- `Controller` (abstract) → `ApplyControls(PublicTankInfo)` → `ControllerOutput`
- `KeyboardController` — WASD+Ctrl / Arrows+Slash
- `KeyboardWithMouseController` — keyboard + `SDL_GetMouseState` for aim/shoot
- `GamePadController` — joystick axes + buttons (Xbox360, PS4 mappings)
- `AiController<Strategy>` — template wrapping `TwitchAI`, `SwarmAI`

### ControllerOutput

```cpp
struct ControllerOutput {
    MoveControls move;     // speed offset
    ShootControls shoot;   // primary, secondary
    BuildControls build;   // primary
    CrosshairControls crosshair; // direction
};
```

### Attachment

`gamelib_tank_attach(tank, tank_num, num_players)` in `control.cpp` — prefers gamepad, falls back to keyboard+mouse.

## Parallelism

- `parallel_for(min, max, func, threadLocals[])` in `parallelism.h`
- Uses `std::async(std::launch::async)` splitting range across `perf::parallelism_degree` workers
- `ThreadLocal`: per-thread `RandomGenerator`, `staged_writes` vector, counters
- **Staged writes pattern**: parallel pass pushes `(index, value)` pairs into thread-local buffers; main thread commits sequentially to avoid races

Used by: `RegrowPass`, `MaterializeLevelTerrain`, `ForEachVoxelParallel`.

## Configuration (`tweak.h`)

All tuning constants in `namespace tweak`:

| Namespace | Key values |
|-----------|------------|
| `perf` | `TargetFps` (24), `SectorSize` (64), `parallelism_degree` |
| `screen` | `RenderSurfaceSize` (320×200), `WindowSize` |
| `world` | `AdvanceStep`, `DirtRecoverInterval`, `DirtRegrowSpeed` |
| `base` | `MinDistance`, `BaseSize`, `DoorSize` |
| `tank` | `MaxLives`, `RespawnDelay`, `TurretDelay`, `BulletMax` |
| `weapon` | `CannonBulletSpeed`, `CannonCooldown`, barrel speeds |
| `explosion` | `ShrapnelCount`, `Speed`, `Frames`, `ChanceToDestroy*` |

## File index

### Core
| File | Purpose |
|------|---------|
| `main.cpp` | Entry point, CLI parsing, game setup |
| `game.h/cpp` | Game lifecycle, mode setup, frame step |
| `world.h/cpp` | World container and Advance orchestration |
| `game_config.h` | VideoConfig, GameConfig structs |
| `game_system.h` | System abstraction (Window, Renderer, Cursor) |
| `tweak.h` | All tuning constants |
| `types.h` | Primitive types (Size, Position, Offset, Rect, Direction, etc.) |

### Entities
| File | Purpose |
|------|---------|
| `controllable.h/cpp` | Base movable entity (movement, digging) |
| `tank.h/cpp` | Player tank |
| `tank_turret.h/cpp` | Turret aim and fire |
| `tank_base.h/cpp` | Fixed base structures |
| `tank_list.h/cpp` | Tank collection |
| `tank_sprites.h/cpp` | 9-direction tank bitmaps |
| `projectiles.h/cpp` | Projectile types (Bullet, Shrapnel, Foam) |
| `projectile_list.h/cpp` | Projectile container with pending list |
| `link.h/cpp` | Power links and graph |
| `machine.h/cpp` | Harvester/Charger machines |
| `machine_list.h/cpp` | Machine collection |
| `machine_materializer.h/cpp` | Build-mode UI |
| `weapon.h/cpp` | Weapon definitions |
| `sprite.h/cpp` | Transient effects |
| `sprite_list.h/cpp` | Sprite collection |

### Terrain and levelgen
| File | Purpose |
|------|---------|
| `terrain.h/cpp` | Terrain grid, pixel ops, drawing |
| `terrain_pixel.h` | TerrainPixel enum |
| `levelgen.h/cpp` | Generator dispatch |
| `levelgen_toast.h/cpp` | Toast generator (default) |
| `levelgen_simple/maze/braid.h/cpp` | Alternative generators |
| `levelgenutil.h/cpp` | Shared helpers |

### Rendering
| File | Purpose |
|------|---------|
| `renderer.h/cpp` | Renderer interface, WorldRenderSurfaces |
| `render_surface.h/cpp` | Surface types (Screen, World, change list) |
| `screen.h/cpp` | Screen layout and widget composition |
| `gui_widgets.h/cpp` | TankView, StatusBar, Crosshair, etc. |
| `color_palette.h/cpp` | Color constants |
| `shape_renderer.h/cpp` | Rectangles, lines |
| `font_renderer.h/cpp` | Bitmap font |
| `bitmaps.h/cpp`, `image_data.h` | Bitmap loading |

### ECS and collision
| File | Purpose |
|------|---------|
| `entity.h/cpp` | EntitySystem wrapper, sector updates |
| `position_component.h` | Position/Velocity components |
| `collision_component.h/cpp` | Bitmap/BBox collision |
| `render_component.h` | Palette renderable aspect |
| `collision_solver.h/cpp` | TestTank, TestMachine, TestTerrain |
| `world_sector.h/cpp` | Spatial partitioning (64px sectors) |

### Platform (SDL)
| File | Purpose |
|------|---------|
| `gamelib/sdl/control.h/cpp` | Game loop, controller attachment |
| `gamelib/sdl/events.cpp` | SDL event pump |
| `gamelib/sdl/sdl_controller.h/cpp` | Keyboard, mouse, gamepad controllers |
| `gamelib/sdl/sdl_renderer.cpp` | SDL texture upload and present |
| `gamelib/sdl/bitmap.cpp` | SDL bitmap helpers |
| `gamelib/sdl/logging.cpp` | Debug output (`OutputDebugString` / stdout) |

### Utilities
| File | Purpose |
|------|---------|
| `parallelism.h` | `parallel_for`, `ThreadLocal` |
| `random.h/cpp` | RNG |
| `duration.h/cpp` | Timers |
| `mymath.h/cpp` | Math helpers |
| `raycaster.h` | Ray casting |
| `vector_2d.h/cpp` | 2D container |
| `containers.h`, `containers_queue.h` | Container utilities |
| `flat_set_map.h` | Flat set/map |
| `iterators.h` | Iterator helpers |
| `exceptions.h/cpp` | Error types |
