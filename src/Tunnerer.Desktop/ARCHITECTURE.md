# Tunnerer.Desktop — Platform Layer

Thin SDL2-based shell that wires the platform-independent `Tunnerer.Core` to a real window, keyboard, and mouse. Contains 4 files.

## File overview

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point, CLI argument parsing |
| `Game.cs` | Main loop, input polling, rendering orchestration, profiling |
| `Input/KeyboardController.cs` | SDL keyboard → `ControllerOutput` |
| `Rendering/SdlRenderer.cs` | SDL window, renderer, texture, events, mouse |

## Program.cs — entry point

Parses command-line arguments:

| Argument | Effect |
|----------|--------|
| `--large` | Sets terrain size to 1500×750 |
| `--size=WxH` | Sets terrain size to custom W×H |
| *(none)* | Terrain size matches render surface (320×200) |

Creates and runs `Game(terrainSize?)`.

## Game.cs — main loop

### Initialization

1. Create `SdlRenderer` with `Tweaks.Screen.WindowSize`
2. Set terrain size (defaults to render surface size if not overridden)
3. Generate level: `ToastGenerator.Generate(terrainSize, seed: 42)`
4. Initialize `World` with terrain, spawns, materialization seed
5. Create `Screen`, `KeyboardController`, `BotTankAI(seed: 44)`
6. Allocate pixel buffers: `worldPixels`, `compositePixels`, `screenPixels`
7. Initial `Terrain.DrawAllToSurface(worldPixels)`

### Per-frame flow

```
loop:
  SdlRenderer.PollEvents()
    → sets _quit on SDL_QUIT
    → handles window resize

  KeyboardController.Update(sdl)
    → reads SDL_GetKeyboardState
    → returns ControllerOutput (WASD + Space)

  SdlRenderer.GetMouseState()
    → returns (x, y, buttons) in logical render coordinates
    → left-click sets ShootPrimary

  Screen.SetCrosshairScreenPos(mx, my, 0)
    → converts screen coords to world aim direction
    → returns DirectionF for turret

  World.Advance(getInput)
    → runs full simulation step
    → player 0: merged keyboard + mouse input
    → player 1: BotTankAI output

  Terrain.DrawChangesToSurface(worldPixels)
    → incremental terrain render

  Array.Copy(worldPixels → compositePixels)
  Draw entities → compositePixels:
    LinkMap → Machines → Projectiles → Sprites → TankList

  Screen.Draw(compositePixels → screenPixels)
    → viewports, GUI widgets, crosshair

  SdlRenderer.RenderFrame(screenPixels)
    → SDL_UpdateTexture → SDL_RenderCopy → SDL_RenderPresent

  Profile every 100 frames:
    [Draw] terrain=X.XXX objects=X.XXX screen=X.XXX | total=X.XXX ms

  Frame pacing:
    Thread.Sleep to hit 24 FPS target
```

### Profiling

Two profile objects:
- `SimulationProfile` (inside `World.Advance`) — per-subsystem simulation timings
- `DrawProfile` (inside `Game.Run`) — terrain draw, entity draw, screen composite

Both print to `Console.WriteLine` every 100 frames.

### Seeds

All random sources use deterministic seeds for reproducible behavior:

| Component | Seed |
|-----------|------|
| `ToastGenerator` | `DefaultSeed` (42) |
| `Terrain.MaterializeTerrain` | `DefaultSeed + 1` (43) |
| `BotTankAI` | `DefaultSeed + 2` (44) |

## KeyboardController.cs — keyboard input

Reads `SDL_GetKeyboardState` each frame and maps:

| Key | Output |
|-----|--------|
| A / Left | MoveSpeed.X = -1 |
| D / Right | MoveSpeed.X = +1 |
| W / Up | MoveSpeed.Y = -1 |
| S / Down | MoveSpeed.Y = +1 |
| Space | ShootPrimary = true |

Returns a `ControllerOutput` with `AimDirection = default` (aim comes from mouse via `Screen.SetCrosshairScreenPos`).

## SdlRenderer.cs — SDL platform

### Initialization

1. `SDL_Init(SDL_INIT_VIDEO)`
2. `SDL_SetHint("SDL_RENDER_SCALE_QUALITY", "0")` — nearest-neighbor scaling (crisp pixels)
3. `SDL_CreateWindow` at configured size, resizable
4. `SDL_CreateRenderer` with `SDL_RENDERER_ACCELERATED`
5. `SDL_CreateTexture` at render surface size (320×200), `ARGB8888`, streaming

### RenderFrame(uint[] pixels)

```
SDL_UpdateTexture(texture, pixels)
SDL_RenderClear(renderer)
SDL_RenderCopy(renderer, texture, src=null, dst=null)  ← stretches to window
SDL_RenderPresent(renderer)
```

The nearest-neighbor hint ensures the 320×200 texture scales crisply to any window size.

### PollEvents

Handles `SDL_QUIT` and `SDL_WINDOWEVENT_RESIZED`. Signals quit via public `bool ShouldQuit`.

### GetMouseState

```csharp
(int x, int y, uint buttons) GetMouseState()
```

1. `SDL_GetMouseState(&rawX, &rawY)` — window pixel coordinates
2. `SDL_GetWindowSize` — current window dimensions
3. Scale to logical render surface: `x = rawX * renderW / windowW`
4. Returns logical coordinates and button mask (bit 0 = left click)

This conversion allows `Screen.SetCrosshairScreenPos` to work correctly regardless of window size.
