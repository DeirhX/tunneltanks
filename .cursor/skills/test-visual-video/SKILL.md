---
name: test-visual-video
description: Generate visual frame traces from tests and convert them to MP4 videos. Use when the user asks to visualize test behavior, heat transfer, or create a video from test output.
---

# Test Visual Video

Use this workflow to capture per-frame test visuals and convert them to `.mp4`.
Always finalize output by moving timestamped MP4 files to the parent `test-visual` folder and deleting `.ppm` files.

## Default (use every time)

Use the helper script so record + convert + finalize always happen:

```powershell
& ".cursor/skills/test-visual-video/record-and-convert.ps1" -Filter "FullyQualifiedName~TankHeatBehaviorTests" -CaptureEvery "2" -Framerate 12
```

Script behavior:
- runs tests with visual capture enabled
- converts recent `.ppm` traces to `trace.mp4`
- moves final MP4 files to parent `test-visual` folder with appended timestamp
- deletes `.ppm` files from subfolders

## 1) Run tests with visual capture enabled

From repo root (`e:\Projects\New\tunnerer`):

```powershell
$env:TUNNERER_TEST_VISUAL = "1"
$env:TUNNERER_TEST_VISUAL_EVERY = "2"   # 1 = every frame, 2/3 = smaller output
dotnet test "tests/Tunnerer.Tests/Tunnerer.Tests.csproj" --filter "FullyQualifiedName~TankHeatBehaviorTests"
```

Notes:
- Visual capture is opt-in and only active when `TUNNERER_TEST_VISUAL` is set.
- Frame output is written under:
  `tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual/<timestamp>_<testname>/`

## 2) Convert one trace folder to video

`ffmpeg` on this machine may not support `-pattern_type glob`, so use sequential copies:

```powershell
$d = "tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual/<folder>"
$files = Get-ChildItem $d -Filter *.ppm | Sort-Object Name
$i = 0
foreach ($f in $files) {
  $seqName = ("seq_{0:D6}.ppm" -f $i)
  Copy-Item $f.FullName (Join-Path $d $seqName) -Force
  $i++
}
ffmpeg -y -framerate 12 -i (Join-Path $d "seq_%06d.ppm") -c:v libx264 -pix_fmt yuv420p (Join-Path $d "trace.mp4")
Get-ChildItem $d -Filter "seq_*.ppm" | Remove-Item -Force
```

## 3) Convert recent trace folders in batch

```powershell
$dirs = Get-ChildItem "tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual" -Directory |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 4 -ExpandProperty FullName

foreach ($d in $dirs) {
  $files = Get-ChildItem $d -Filter *.ppm | Sort-Object Name
  $i = 0
  foreach ($f in $files) {
    $seqName = ("seq_{0:D6}.ppm" -f $i)
    Copy-Item $f.FullName (Join-Path $d $seqName) -Force
    $i++
  }
  ffmpeg -y -framerate 12 -i (Join-Path $d "seq_%06d.ppm") -c:v libx264 -pix_fmt yuv420p (Join-Path $d "trace.mp4")
  Get-ChildItem $d -Filter "seq_*.ppm" | Remove-Item -Force
}
```

## 4) Finalize location (parent folder + timestamp)

After conversion, move each `trace.mp4` to the parent `test-visual` folder and append a timestamp.
Also remove `.ppm` files from trace subfolders to keep output clean.

```powershell
$root = "tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
Get-ChildItem $root -Directory | ForEach-Object {
  $dir = $_.FullName
  $mp4 = Join-Path $dir "trace.mp4"
  if (Test-Path $mp4) {
    $target = Join-Path $root ("{0}_{1}.mp4" -f $_.Name, $stamp)
    Move-Item $mp4 $target -Force
  }
  Get-ChildItem $dir -Filter *.ppm -File -ErrorAction SilentlyContinue | Remove-Item -Force
}
```

Final file location pattern:
- `tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual/<trace-folder-name>_<timestamp>.mp4`

## 5) What to share back

- Absolute path(s) to generated timestamped MP4 files in parent `test-visual`.
- Which tests/folders were converted.
- If any tests failed, report that video generation still succeeded for captured frames.

## 6) Play latest video (mpv.net)

```powershell
$video = Get-ChildItem "tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual" -Filter *.mp4 |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
& "$env:LOCALAPPDATA/Programs/mpv.net/mpvnet.exe" "$($video.FullName)"
```

## 7) Register mpv.net as default for `.mp4` (current user)

```powershell
$mpv = "$env:LOCALAPPDATA/Programs/mpv.net/mpvnet.exe"
New-Item -Path "HKCU:\Software\Classes\mpvnet.mp4\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\mpvnet.mp4\shell\open\command" -Name "(default)" -Value "`"$mpv`" `"%1`""

New-Item -Path "HKCU:\Software\Classes\.mp4" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\.mp4" -Name "(default)" -Value "mpvnet.mp4"

# Remove per-user cached choice so Windows can use HKCU\Software\Classes\.mp4 mapping.
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.mp4\UserChoice" -Recurse -Force -ErrorAction SilentlyContinue
```
