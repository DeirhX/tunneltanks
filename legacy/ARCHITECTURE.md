# TunnelTanks Architecture

A 2D tank combat game where players dig through procedurally generated caverns, collect resources, build machines, and fight. Two parallel codebases implement the same game:

| Codebase | Path | Stack | Status |
|----------|------|-------|--------|
| C++ (original) | `legacy/src/`, `legacy/crust.sln` | C++20, MSVC, SDL2, ecs.hpp, NanoGUI | Complete, all features |
| C# (rewrite) | `src/`, `tests/` | .NET 10, Silk.NET (SDL2), xUnit | Core simulation + rendering |

## Game overview

- **Terrain**: A large grid of `TerrainPixel` values (dirt, rock, blank, concrete, energy, etc.)
- **Level generation**: "Toast" algorithm — MST of random points → Bresenham lines → random expansion → cellular automata smoothing
- **Tanks**: Move through caverns, dig through dirt (7×7 minus corners), shoot projectiles
- **Torch mechanic**: Shooting in the movement direction allows moving through dirt at full speed; otherwise movement pauses on dig frames. Rock is only destructible while shooting.
- **Bases**: Fixed structures that recharge tanks and absorb collected resources
- **Machines**: Harvesters (collect energy) and Chargers (provide power) built by tanks
- **Links**: Power network connecting bases, machines, and tanks via line-of-sight
- **Regrow**: Blank/scorched terrain slowly recovers to dirt via parallel cellular automata

## Frame loop

Both codebases follow the same structure:

```
1. Poll input (keyboard/mouse/gamepad → ControllerOutput)
2. World.Advance():
   a. RegrowPass (parallel, staged writes)
   b. Projectiles.Advance
   c. Tanks.Advance (movement, digging, shooting, turret)
   d. Machines.Advance
   e. Sprites.Advance
   f. Bases.Advance
   g. LinkMap.Advance
3. Render:
   a. Terrain.DrawChangesToSurface (incremental via change list)
   b. Copy terrain → composite buffer
   c. Draw entities onto composite (links, machines, projectiles, sprites, tanks)
   d. Screen.Draw: viewport blitting, GUI widgets → screen buffer
   e. SDL_UpdateTexture → SDL_RenderCopy → SDL_RenderPresent
```

## Key design decisions

- **Software rendering**: All pixel operations in CPU arrays; one `SDL_UpdateTexture` per frame uploads to GPU. No shaders or GPU drawing.
- **Terrain as source of truth**: Terrain pixels determine collision, digging, resource collection. Entities don't own collision shapes — they test against terrain.
- **Change list**: Only modified terrain pixels are redrawn each frame, making terrain rendering O(changes) not O(width × height).
- **Staged writes for parallelism**: Parallel passes (regrow, smoothing) write to thread-local buffers, then commit sequentially. This avoids data races without locks.
- **Deterministic seeds**: Generator, materialization, and AI accept optional seeds for reproducible testing.

## Profiling

Both versions print `[Profile]` (simulation) and `[Draw]` (rendering) timings every 100 frames:

```
[Profile] regrow=X.XXX proj=X.XXX tanks=X.XXX harv=X.XXX spr=X.XXX bases=X.XXX links=X.XXX | total=X.XXX ms
[Draw]    terrain=X.XXX objects=X.XXX screen=X.XXX | total=X.XXX ms
```

C++ additionally profiles `terrain_advance` (change list processing) and `ecs` (entity system overhead).

## Directory structure

```
tunnerer/
├── ARCHITECTURE.md               # Root C# architecture overview
├── Tunnerer.slnx                 # C# solution
├── src/                          # C# projects
│   ├── Tunnerer.Core/
│   └── Tunnerer.Desktop/
├── tests/
│   └── Tunnerer.Tests/
└── legacy/                       # C++ codebase
    ├── ARCHITECTURE.md           ← you are here
    ├── crust.sln
    ├── crust.vcxproj
    ├── src/
    ├── libs/
    ├── config/
    ├── resources/
    └── build/                    # Output (gitignored)
```

## Build

See `.cursor/skills/build-tunneltanks/SKILL.md` for exact commands. Quick reference:

- **C++ Release**: `MSBuild.exe legacy\crust.vcxproj /p:Configuration=Release /p:Platform=x64`
- **C# Release**: `dotnet build --configuration Release` (from repo root)
- **C# Tests**: `dotnet test --configuration Release` (from repo root)
- **C# Large map**: `dotnet run --project src\Tunnerer.Desktop -- --size=1000x500`
