# Baut CyberWallpaper.exe mit dem in Windows eingebauten .NET-Framework-Compiler.
# Keine Installation noetig.
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) { throw "csc.exe nicht gefunden." }

& $csc /nologo /target:winexe /optimize+ /out:CyberWallpaper.exe `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    CyberWallpaperFX.cs

if ($LASTEXITCODE -eq 0) { Write-Host "OK -> CyberWallpaper.exe gebaut." }
else { Write-Host "Build fehlgeschlagen (Exit $LASTEXITCODE)."; exit $LASTEXITCODE }
