# Tunnerer.Desktop — Rendering Architecture

This document describes the current rendering model used by the desktop client, including backend selection, frame flow, post-processing, and profiling hooks.

## High-Level Layout

`Tunnerer.Desktop` has a backend-agnostic render contract and two active implementations:

- `OpenGlGameRenderBackend`
- `Dx11GameRenderBackend`

Core orchestration lives in `Game.cs`; platform/windowing lives in `Rendering/SdlRenderer.cs`.

## Backend Selection

Selection path:

1. CLI argument `--backend=opengl|dx11|dx12` is parsed in `Program.cs`.
2. `Game` chooses a graphics mode:
   - OpenGL backend -> SDL OpenGL window/context (`SdlGraphicsMode.OpenGl`)
   - DX11 backend -> native window (`SdlGraphicsMode.NativeWindow`)
3. `RenderBackendFactory` creates:
   - backend instance (`IGameRenderBackend`)
   - texture loader (`ITextureLoader`)

`Dx12` is wired in enum/arg parsing but intentionally not implemented.

## Backend Interface

Shared contract (`IGameRenderBackend`):

- `ProcessEvent(Event ev)`
- `UploadGamePixels(in GamePixelsUpload upload)`
- `ClearFrame(Size viewportSize, Color clearColor)`
- `NewFrame(int windowW, int windowH, float deltaTime)`
- `Render()`
- `GameTextureId` (used by HUD viewport `ImGui.Image`)
- `SupportsUi` (if false, HUD/ImGui path is skipped)

Upload payload (`GamePixelsUpload`) includes:

- scene pixels (`uint[]`)
- camera/view metadata (`RenderView`)
- quality level
- tank glow array
- terrain auxiliary mask texture data + dirty rect

## Per-Frame Flow (Game.cs)

Simplified frame pipeline:

1. Poll SDL input/events (`SdlRenderer.PollEvents`)
2. Advance simulation (`World.Advance`)
3. Build high-res terrain buffer:
   - full render, or
   - camera scroll copy + exposed strips, plus
   - dirty terrain updates
4. Build/update terrain aux texture payload
5. Compose entities over terrain
6. Build tank glow metadata
7. Upload composed frame to backend (`UploadGamePixels`)
8. Clear frame (`ClearFrame`)
9. If backend supports UI:
   - `NewFrame`
   - `GameHud.Draw` (`ImGui.Image` + overlays/panels)
10. `Render` backend output
11. `SdlRenderer.SwapWindow()` (OpenGL swap only; DX11 presents via swap chain)

## OpenGL Backend

OpenGL backend uses:

- game textures (double-buffer style)
- post-process source texture + FBO destination
- terrain aux texture with dirty-rect sub-updates
- large GLSL post-process pass (bloom, vignette, edge light, terrain mask, emissive, tank glow)
- OpenGL ImGui renderer (`ImGuiController`)

`SupportsUi = true`; `GameTextureId` is a GL texture handle.

## DX11 Backend

DX11 backend now has:

- native swap chain + backbuffer RTV
- scene texture + SRV (uploaded from CPU frame buffer)
- post-process texture + RTV + SRV
- terrain aux texture + SRV with dirty-rect uploads
- fullscreen VS + two PS variants:
  - post-process PS
  - final blit PS
- DX11 ImGui renderer/controller (`Dx11ImGuiController`)
- DX11 texture manager for HUD sprites (returns `ID3D11ShaderResourceView*` encoded as `nint`)

`SupportsUi = true` when native DX11 init succeeds; `GameTextureId` is an SRV pointer for ImGui.

### DX11 Render Stages

`UploadGamePixels` (native path):

1. upload scene texture (`UpdateSubresource`)
2. update terrain aux texture (full or dirty rect)
3. run post-process pass into post RTV

`Render`:

1. fullscreen blit of display SRV to backbuffer RTV
2. ImGui draw on top (HUD + overlays)
3. `swapChain->Present`

### DX11 Safety/Debug Toggles

- `TUNNERER_DX11_CPU_FALLBACK=1`
  - bypasses DX11 post shader and uses CPU fallback effects path for comparison.
- `TUNNERER_DX11_PROFILE=1`
  - enables detailed DX11 stage timing printouts every 240 profiled frames and on dispose.

## Current Profiling Output

`DrawProfile` (logged every 100 frames) includes:

- top-level: `[Draw]`
- screen-level: `[Screen]`
- decomposition: `[Screen+]`
- finer decomposition: `[Screen++]`

`[Screen++]` currently includes:

- `fullTerrain`
- `terrainCopy`
- `entities`
- `tankGlowBuild`
- `qualityAdjust`
- `clear`
- `newFrame`
- `hud`

DX11 backend detailed profile (`TUNNERER_DX11_PROFILE=1`) includes:

- `sceneUpload`
- `auxUpload`
- `postTotal`
- `postSetup`
- `postCb`
- `postDraw`
- `blit`
- `ui`

## Notes on Known Behavior

- Performance spikes observed in recent traces are usually terrain-side (`dirtyTerrain`, `fullTerrain`, `exposedStrips`) rather than DX11 post/blit.
- Remaining visual parity work is focused on fine-tuning look consistency (e.g. heat/emissive tint) and stability edge cases.
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
