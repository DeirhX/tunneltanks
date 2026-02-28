# C# Rewrite Architecture (`root`)

A port of the C++ Tunnerer engine to C# / .NET 10. Uses Silk.NET for SDL2 bindings. The simulation logic is platform-independent (`Tunnerer.Core`); the platform layer is isolated (`Tunnerer.Desktop`).

The desktop renderer currently supports multiple backends (`OpenGL`, `DX11`) behind `IGameRenderBackend`. For backend-specific details, see `src/Tunnerer.Desktop/ARCHITECTURE.md`.

## Solution structure

```
tunnerer/
├── ARCHITECTURE.md              ← you are here
├── Tunnerer.slnx
├── src/
│   ├── Tunnerer.Core/        # Simulation, entities, terrain, GUI (no SDL dependency)
│   │   └── ARCHITECTURE.md      # Detailed design
│   └── Tunnerer.Desktop/     # SDL window, rendering, keyboard input, game loop
│       └── ARCHITECTURE.md      # Detailed design
└── tests/
    └── Tunnerer.Tests/       # xUnit tests (141 tests)
```

## Design principles

- **Core has no platform dependencies**: `Tunnerer.Core` operates on `uint[]` pixel buffers and `ControllerOutput` structs. It knows nothing about SDL, windows, or real input devices.
- **Desktop is a thin shell**: `Tunnerer.Desktop` creates the SDL window, polls events, maps keyboard/mouse to `ControllerOutput`, and uploads the final pixel buffer to a texture.
- **Faithful C++ port**: Subsystem execution order, terrain pixel types, movement mechanics, dig shapes, torch logic, color palette, and profiling categories all match the C++ original.
- **Deterministic seeds**: `ToastGenerator`, `Terrain.MaterializeTerrain`, and `TwitchAI` accept optional `int? seed` parameters for reproducible maps and AI behavior.

## Frame loop (`Game.Run`)

```
while (!quit):
  1. SdlRenderer.PollEvents()          → resize, close
  2. KeyboardController.Update()       → ControllerOutput (keyboard)
  3. SdlRenderer.GetMouseState()       → aim direction + mouse shoot
  4. TwitchAI.GetOutput()              → ControllerOutput (AI)
  5. World.Advance(getInput)           → full simulation step
  6. Terrain.DrawChangesToSurface()    → incremental terrain render
  7. Copy worldPixels → compositePixels
  8. Draw entities → compositePixels   (links, machines, projectiles, sprites, tanks)
  9. Screen.Draw(composite → screen)   → viewports, GUI widgets
  10. SdlRenderer.RenderFrame(screen)  → SDL_UpdateTexture → present
  11. Profile report every 100 frames
  12. Frame pacing (24 FPS target)
```

## Simulation order (`World.Advance`)

Matches the C++ exactly:

1. **RegrowPass** — dirt recovery via cellular automata
2. **Projectiles** — movement, terrain impact, explosions
3. **Tanks** — input → turret → movement → dig → shoot
4. **Machines** — harvest energy, charge reactor
5. **Sprites** — decrement lifetime, remove expired
6. **Bases** — reactor recharge
7. **LinkMap** — power grid solve (Bresenham visibility)

## Rendering pipeline (conceptual)

```
Terrain._data
    ↓ DrawChangesToSurface (change list)
worldPixels[w×h]
    ↓ Array.Copy
compositePixels[w×h]
    ↓ Draw entities (in painter's order)
compositePixels[w×h]
    ↓ Screen.Draw (viewport blit + GUI)
screenPixels[320×200]
    ↓ IGameRenderBackend.UploadGamePixels
Backend-managed GPU resources
    ↓ IGameRenderBackend.Render
Window
```

The composite buffer prevents entity rendering from corrupting the terrain surface, which would cause ghosting artifacts. Concrete GPU implementation differs per backend and is documented in `src/Tunnerer.Desktop/ARCHITECTURE.md`.

## Key differences from C++

| Area | C++ | C# |
|------|-----|-----|
| ECS | `ecs.hpp` library with components/aspects | No ECS; entities are plain classes |
| Parallelism | `parallel_for` + staged writes | `Parallel.For` in `RegrowPass` and level gen |
| Surface types | `WorldRenderSurface` with change lists | `uint[]` arrays; change list on `Terrain` only |
| Input | Keyboard, mouse, gamepad controllers | Keyboard + mouse (no gamepad yet) |
| Level generators | 4 (Simple, Maze, Braid, Toast) | 1 (Toast only) |
| GUI framework | Widgets via `gui_widgets.h` | Inline in `Screen.cs` |
| Memory | Custom allocators, `Container2D` | Managed arrays, `Span<T>` |

## Dependencies

- **Silk.NET.SDL** — SDL2 P/Invoke bindings (window, renderer, texture, input)
- **xUnit** — test framework
- **.NET 10** — target framework

## Tests

141 tests across 10 files covering:

| File | Coverage |
|------|----------|
| `CollisionTests` | CollisionSolver, pixel blocking queries |
| `TerrainTests` | Terrain grid, change list, materialization |
| `TerrainPixelTests` | Pixel classification (IsDirt, IsBlocking, etc.) |
| `LevelGenTests` | Toast generator, connectivity, border walls |
| `TypeTests` | Position, Offset, Size, VectorF, Rect |
| `ResourceTests` | Reactor, MaterialContainer |
| `GuiLayoutTests` | Screen layout for 1P and 2P modes |
| `WorldIntegrationTests` | World.Advance, level init, draw pipeline |
| `ReplayTests` | Deterministic seeded replays (map + simulation) |
| `TankMovementTests` | Movement, digging, torch, corridor, crosshair |
