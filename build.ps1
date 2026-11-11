<#
.SYNOPSIS
  Baut die KI-Zentrale (Einzeldatei inkl. .NET-Laufzeit) und das Installationsprogramm.
.EXAMPLE
  ./build.ps1                 # App + Setup nach artifacts\
  ./build.ps1 -SkipSetup      # nur die App
  ./build.ps1 -Version 1.2.0  # Version fuer diesen Build ueberschreiben
#>
param(
    [string]$Configuration = 'Release',
    [string]$Version,
    [switch]$SkipSetup
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$art = Join-Path $root 'artifacts'
$verArg = if ($Version) { "-p:Version=$Version" } else { $null }

Get-Process KI-Zentrale -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$art*" } | Stop-Process -Force

Write-Host '==> App bauen (Einzeldatei inkl. .NET-Laufzeit)' -ForegroundColor Cyan
$appArgs = @('publish', "$root\src\KIZentrale\KIZentrale.csproj", '-c', $Configuration, '-o', "$art\app", '--nologo')
if ($verArg) { $appArgs += $verArg }
dotnet @appArgs
if ($LASTEXITCODE) { throw 'App-Build fehlgeschlagen' }
Remove-Item "$art\app\*.xml", "$art\app\*.pdb" -ErrorAction SilentlyContinue

if (-not $SkipSetup) {
    Write-Host '==> Setup bauen' -ForegroundColor Cyan
    $setupArgs = @('build', "$root\src\Setup\Setup.csproj", '-c', $Configuration, '-o', "$art\setup", '--nologo', "-p:AppPayload=$art\app\KI-Zentrale.exe")
    if ($verArg) { $setupArgs += $verArg }
    dotnet @setupArgs
    if ($LASTEXITCODE) { throw 'Setup-Build fehlgeschlagen' }
    Copy-Item "$art\setup\KI-Zentrale-Setup.exe" "$art\KI-Zentrale-Setup.exe" -Force
    $hash = (Get-FileHash "$art\KI-Zentrale-Setup.exe" -Algorithm SHA256).Hash.ToLower()
    "$hash  KI-Zentrale-Setup.exe" | Set-Content "$art\KI-Zentrale-Setup.exe.sha256" -Encoding ascii
    $mb = [math]::Round((Get-Item "$art\KI-Zentrale-Setup.exe").Length / 1MB, 1)
    Write-Host "Fertig: $art\KI-Zentrale-Setup.exe ($mb MB)" -ForegroundColor Green
    Write-Host "SHA-256: $hash"
} else {
    Write-Host "Fertig: $art\app\KI-Zentrale.exe" -ForegroundColor Green
}
