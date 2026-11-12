# Baut den Installer CyberWallpaper-Setup.exe. Er enthält Programm, Bilder und Masken als eingebettete Dateien.
# Vorher muss CyberWallpaper.exe mit build.ps1 gebaut worden sein.
#   .\build-setup.ps1                 Version = git describe (oder "dev")
#   .\build-setup.ps1 -Version v1.2.0
param([string]$Version = "")
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

if (-not $Version) {
    $Version = (git describe --tags --always 2>$null)
    if (-not $Version) { $Version = "dev" }
}

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) { throw "csc.exe nicht gefunden." }

$payload = 'CyberWallpaper.exe', 'background.png', 'city_mask.png', 'cloud_mask.png', 'LIESMICH.md'
foreach ($f in $payload) { if (-not (Test-Path $f)) { throw "Datei fehlt: $f (zuerst .\build.ps1 ausführen)" } }

$verFile = Join-Path ([System.IO.Path]::GetTempPath()) "CyberWallpaper-version.txt"
[System.IO.File]::WriteAllText($verFile, $Version)
$res = @($payload | ForEach-Object { "/resource:$_,payload.$_" }) + "/resource:$verFile,version.txt"

& $csc /nologo /target:winexe /optimize+ /codepage:65001 /out:CyberWallpaper-Setup.exe `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    @res Setup.cs Shared.cs

if ($LASTEXITCODE -eq 0) { Write-Host "OK -> CyberWallpaper-Setup.exe gebaut (Version $Version)." }
else { Write-Host "Installer-Build fehlgeschlagen (Exit $LASTEXITCODE)."; exit $LASTEXITCODE }
