param(
    [string]$Filter = "FullyQualifiedName~TankHeatBehaviorTests",
    [string]$CaptureEvery = "2",
    [int]$Framerate = 12,
    [int]$ConvertRecent = 6
)

$ErrorActionPreference = "Stop"

$root = "tests/Tunnerer.Tests/bin/Debug/net10.0/artifacts/test-visual"

$env:TUNNERER_TEST_VISUAL = "1"
$env:TUNNERER_TEST_VISUAL_EVERY = $CaptureEvery

dotnet test "tests/Tunnerer.Tests/Tunnerer.Tests.csproj" --filter $Filter

if (-not (Test-Path $root)) {
    Write-Output "No visual output directory found: $root"
    exit 0
}

$dirs = Get-ChildItem $root -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First $ConvertRecent

foreach ($d in $dirs) {
    $files = Get-ChildItem $d.FullName -Filter *.ppm | Sort-Object Name
    if ($files.Count -eq 0) {
        continue
    }

    $i = 0
    foreach ($f in $files) {
        $seqName = ("seq_{0:D6}.ppm" -f $i)
        Copy-Item $f.FullName (Join-Path $d.FullName $seqName) -Force
        $i++
    }

    ffmpeg -y -framerate $Framerate -i (Join-Path $d.FullName "seq_%06d.ppm") -c:v libx264 -pix_fmt yuv420p (Join-Path $d.FullName "trace.mp4") | Out-Null
    Get-ChildItem $d.FullName -Filter "seq_*.ppm" | Remove-Item -Force
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$moved = @()
Get-ChildItem $root -Directory | ForEach-Object {
    $dir = $_.FullName
    $mp4 = Join-Path $dir "trace.mp4"
    $movedThisDir = $false
    if (Test-Path $mp4) {
        $target = Join-Path $root ("{0}_{1}.mp4" -f $_.Name, $stamp)
        Move-Item $mp4 $target -Force
        $moved += $target
        $movedThisDir = $true
    }
    Get-ChildItem $dir -Filter *.ppm -File -ErrorAction SilentlyContinue | Remove-Item -Force
    if ($movedThisDir -and (Test-Path $dir)) {
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Output "Final videos:"
$moved | ForEach-Object { Write-Output $_ }
