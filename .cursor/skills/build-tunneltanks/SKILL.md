---
name: build-tunneltanks
description: Build and run the TunnelTanks project (C++ and C# codebases). Use when the user asks to build, compile, run, test, or launch the game, or when build errors occur.
---

# Build TunnelTanks

Two codebases live side-by-side. Always build the one the user is working on; if unclear, ask.

## C++ Build (MSBuild)

### Build command

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" crust.sln /p:Configuration=Release /p:Platform=x64 /m /verbosity:minimal
```

- **Debug**: output is `build\bin\Debug\crust.exe`
- **Release**: output is `build\bin\Release\tunneltanks.exe`
- Build only the main project (not the full solution) to skip the broken nanogui custom build step:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" crust.vcxproj /p:Configuration=Release /p:Platform=x64 /m /verbosity:minimal
```

### Runtime DLLs (Debug only)

Copy these into `build\bin\Debug\` before running:
- `libs\nanogui\out\build\x64-Debug (default)\nanogui.dll`
- `libs\nanogui\out\build\x64-Debug (default)\ext_build\glfw\src\glfw3.dll`
- `resources\fonts\broddmin_5x10.bmp` → `build\bin\Debug\resources\fonts\`

Release does not require nanogui/glfw DLLs.

### Known issues

- **nanogui.vcxproj**: Has a broken custom build step referencing `E:\Projects\tunnerer\` (wrong path). Building the full solution will show 1 error from nanogui — the main game still links. Build `crust.vcxproj` directly to avoid it.
- **VS version**: Must use VS 2026 (version 18). VS 2022 MSBuild path does not work.
- **Toolset**: v145, `/std:c++latest`, warnings-as-errors (`/WX /W4`).

### Run

```powershell
Start-Process "E:\Projects\New\tunnerer\build\bin\Release\tunneltanks.exe"
```

## C# Build (.NET)

Working directory: `e:\Projects\New\tunnerer\dotnet`

### Build

```powershell
dotnet build --configuration Release
```

Output goes to `build\bin\Release\` (shared OutputPath configured in Directory.Build.props).

### Test

```powershell
dotnet test --configuration Release --verbosity minimal
```

### Run

```powershell
dotnet run --configuration Release --project src\TunnelTanks.Desktop
```

### Known issues

- **File lock**: If a previous game instance is still running, the build fails with `MSB3027` (file locked). Kill it first:
  ```powershell
  Get-Process TunnelTanks.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
  ```
- **Target framework**: Uses `net10.0` (.NET 10). Do not downgrade to net9.0.
- **NuGet source**: If restore fails with `NU1101`, add the source:
  ```powershell
  dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
  ```

## Project structure

```
tunnerer/
├── crust.sln              # C++ solution
├── crust.vcxproj           # C++ main project
├── src/                    # C++ source
├── libs/                   # C++ submodules (ecs, nanogui, sdl2)
├── config/                 # C++ build props
├── dotnet/
│   ├── dotnet.slnx         # C# solution
│   ├── src/
│   │   ├── TunnelTanks.Core/      # Simulation logic (no SDL dependency)
│   │   └── TunnelTanks.Desktop/   # SDL rendering + input
│   └── tests/
│       └── TunnelTanks.Tests/     # xUnit tests
└── build/                  # Shared output directory (gitignored)
```

## Quick reference

| Task | Command |
|------|---------|
| Build C++ Release | `& "...\MSBuild.exe" crust.vcxproj /p:Configuration=Release /p:Platform=x64 /m` |
| Build C# | `dotnet build --configuration Release` (from `dotnet/`) |
| Test C# | `dotnet test --configuration Release` (from `dotnet/`) |
| Run C++ | `Start-Process "build\bin\Release\tunneltanks.exe"` |
| Run C# | `dotnet run --configuration Release --project src\TunnelTanks.Desktop` (from `dotnet/`) |
| Kill C# | `Get-Process TunnelTanks.Desktop -ErrorAction SilentlyContinue \| Stop-Process -Force` |
