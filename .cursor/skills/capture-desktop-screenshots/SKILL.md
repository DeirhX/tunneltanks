---
name: capture-desktop-screenshots
description: Capture game-window screenshots that include final composited content (scene plus ImGui/HUD). Use when validating font readability, overlays, or UI composition in Tunnerer.Desktop.
---

# Capture Final-Frame Screenshots

Use this workflow when the user wants screenshots of what is actually visible in the game window (including ImGui/HUD overlays).

## When To Use

- You need to validate font legibility or overlay readability.
- You need capture from the game window only (not the entire desktop).
- You need proof of final composited on-screen output.

## Why This Skill Exists

DX11 screenshot capture now reads the final backbuffer before `Present`, so it includes scene + ImGui/HUD and matches what users see in the game window.

## Standard Workflow

1. Stop any previous desktop process:

```powershell
Get-Process Tunnerer.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
```

2. Request a scripted in-engine screenshot on a known frame:

```powershell
$env:TUNNERER_SCRIPT_SCREENSHOT_FRAME = "20"
dotnet run --configuration Debug --project "src/Tunnerer.Desktop" -- --backend=dx11 --perf --perf-warmup=0 --perf-frames=80
Remove-Item Env:TUNNERER_SCRIPT_SCREENSHOT_FRAME -ErrorAction SilentlyContinue
```

3. Convert the generated `.ppm` to `.png` for easier viewing:

```powershell
python -c "from PIL import Image; Image.open(r'src/Tunnerer.Desktop/out/bin/Debug/net10.0/screenshots/<file>.ppm').save(r'artifacts/debug/final_frame_window_capture.png')"
```

4. Read the PNG:

```powershell
# Use ReadFile tool on artifacts/debug/final_frame_window_capture.png
```

## Notes

- Screenshot output path is printed in logs (`Queued final-frame screenshot capture`).
- Captures are written under `src/Tunnerer.Desktop/out/bin/.../screenshots/`.
- Keep runtime bounded with `--perf` so automation exits cleanly.
- Use `artifacts/debug/` for converted PNGs and comparisons.
- For base-exit scripts, remember base "doors" are `BaseBarrier` cells: movement-only scripts can get stuck. Use scripted shooting while exiting (see below).

## Scripted Movement With Shooting

`TUNNERER_SCRIPTED_INPUT` now supports optional shoot flag per segment:

```text
dx,dy,shoot:frames
```

- `shoot` is optional (`0` or `1`); omitted means `0`.
- Example bottom-center base exit:

```powershell
$env:TUNNERER_SCRIPTED_INPUT = "1,0,0:26;0,1,1:120;0,1,0:90"
```

## Automated Pass Toggle A/B

Use in-engine scripting (not OS key injection) for reliable ON/OFF comparisons:

```powershell
$env:TUNNERER_COMMAND_SCRIPT = "40:TogglePostBloom,80:TogglePostBloom,120:TogglePostEdgeLift,160:TogglePostEdgeLift,200:TogglePostTankGlow,240:TogglePostTankGlow"
$env:TUNNERER_SCRIPT_SCREENSHOT_FRAMES = "41,81,121,161,201,241"
dotnet run --configuration Debug --project "src/Tunnerer.Desktop" -- --backend=dx11 --perf --perf-warmup=0 --perf-frames=260
Remove-Item Env:TUNNERER_COMMAND_SCRIPT -ErrorAction SilentlyContinue
Remove-Item Env:TUNNERER_SCRIPT_SCREENSHOT_FRAMES -ErrorAction SilentlyContinue
```

Format details:
- `TUNNERER_COMMAND_SCRIPT`: comma/semicolon list of `frame:GameCommand`.
- `GameCommand` token must match enum names (case-insensitive), e.g. `TogglePostBloom`, `TogglePostEdgeLift`, `RequestScreenshot`.
- `TUNNERER_SCRIPT_SCREENSHOT_FRAMES`: comma/semicolon/space-separated frame list.

## Optional: Desktop Grab Fallback

Use this only if the in-engine capture fails unexpectedly:

```powershell
python -c "import subprocess,time; from PIL import ImageGrab; cwd=r'E:/Projects/New/tunnerer'; out=r'E:/Projects/New/tunnerer/artifacts/debug/desktop_capture_fallback.png'; cmd=['dotnet','run','--configuration','Debug','--project','src/Tunnerer.Desktop','--','--backend=dx11','--perf','--perf-warmup=0','--perf-frames=240']; p=subprocess.Popen(cmd,cwd=cwd); time.sleep(2.8); ImageGrab.grab().save(out); print(out); p.wait(timeout=35)"
```

## Troubleshooting

- **No screenshot written**: verify `TUNNERER_SCRIPT_SCREENSHOT_FRAME` is set and run long enough to reach that frame.
- **Wrong frame**: adjust the frame value (`20`, `40`, etc.) and rerun.
- **Process hangs**: lower `--perf-frames` or kill stale process before rerun.
