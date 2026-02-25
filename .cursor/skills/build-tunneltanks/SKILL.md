---
name: build-tunneltanks
description: Build and run the Tunnerer project (C++ and C# codebases). Use when the user asks to build, compile, run, test, or launch the game, or when build errors occur.
---

# Build Tunnerer

Two codebases live side-by-side. Always build the one the user is working on; if unclear, ask.

## C++ Build (MSBuild)

### Build command

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" legacy\crust.sln /p:Configuration=Release /p:Platform=x64 /m /verbosity:minimal
```

- **Debug**: output is `legacy\build\bin\Debug\crust.exe`
- **Release**: output is `legacy\build\bin\Release\tunneltanks.exe`
- Build only the main project (not the full solution) to skip the broken nanogui custom build step:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" legacy\crust.vcxproj /p:Configuration=Release /p:Platform=x64 /m /verbosity:minimal
```

### Runtime DLLs (Debug only)

Copy these into `legacy\build\bin\Debug\` before running:
- `legacy\libs\nanogui\out\build\x64-Debug (default)\nanogui.dll`
- `legacy\libs\nanogui\out\build\x64-Debug (default)\ext_build\glfw\src\glfw3.dll`
- `legacy\resources\fonts\broddmin_5x10.bmp` → `legacy\build\bin\Debug\resources\fonts\`

Release does not require nanogui/glfw DLLs.

### Known issues

- **nanogui.vcxproj**: Has a broken custom build step referencing an old absolute path. Building the full solution may show 1 error from nanogui — the main game still links. Build `legacy\crust.vcxproj` directly to avoid it.
- **VS version**: Must use VS 2026 (version 18). VS 2022 MSBuild path does not work.
- **Toolset**: v145, `/std:c++latest`, warnings-as-errors (`/WX /W4`).

### Run

```powershell
Start-Process "E:\Projects\New\tunnerer\legacy\build\bin\Release\tunneltanks.exe"
```

## C# Build (.NET)

Working directory: `e:\Projects\New\tunnerer`

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
dotnet run --configuration Release --project src\Tunnerer.Desktop
```

### Known issues

- **File lock**: If a previous game instance is still running, the build fails with `MSB3027` (file locked). Kill it first:
  ```powershell
  Get-Process Tunnerer.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
  ```
- **Target framework**: Uses `net10.0` (.NET 10). Do not downgrade to net9.0.
- **NuGet source**: If restore fails with `NU1101`, add the source:
  ```powershell
  dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
  ```

## Project structure

```
tunnerer/
├── legacy/
│   ├── crust.sln           # C++ solution
│   ├── crust.vcxproj       # C++ main project
│   ├── src/                # C++ source
│   ├── libs/               # C++ submodules (ecs, nanogui, sdl2)
│   ├── config/             # C++ build props
│   └── build/              # C++ output directory
├── Tunnerer.slnx           # C# solution
├── src/
│   ├── Tunnerer.Core/      # Simulation logic (no SDL dependency)
│   └── Tunnerer.Desktop/   # SDL rendering + input
├── tests/
│   └── Tunnerer.Tests/     # xUnit tests
└── .gitmodules             # Submodules now rooted under legacy/libs
```

## Quick reference

| Task | Command |
|------|---------|
| Build C++ Release | `& "...\MSBuild.exe" legacy\crust.vcxproj /p:Configuration=Release /p:Platform=x64 /m` |
| Build C# | `dotnet build Tunnerer.slnx --configuration Release` |
| Test C# | `dotnet test Tunnerer.slnx --configuration Release` |
| Run C++ | `Start-Process "legacy\build\bin\Release\tunneltanks.exe"` |
| Run C# | `dotnet run --configuration Release --project src\Tunnerer.Desktop` |
| Kill C# | `Get-Process Tunnerer.Desktop -ErrorAction SilentlyContinue \| Stop-Process -Force` |
