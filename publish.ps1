<#
.SYNOPSIS
  Veröffentlicht die KI-Zentrale auf GitHub (einmalig ausführen).
.DESCRIPTION
  1. Ersetzt den Platzhalter OWNER in README, Changelog, Vorlagen durch deinen GitHub-Namen.
  2. Verbindet das lokale Repository mit https://github.com/<Name>/ki-zentrale und lädt main hoch.
  3. Optional: setzt den Versions-Tag (z. B. v1.1.0) – GitHub Actions baut dann das Setup und legt das Release an.
  Vorher auf github.com ein LEERES öffentliches Repository "ki-zentrale" anlegen (ohne README/Lizenz).
.EXAMPLE
  ./publish.ps1 -GitHubName meinname -Release
#>
param(
    [Parameter(Mandatory)][string]$GitHubName,
    [string]$Repo = 'ki-zentrale',
    [switch]$Release
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$files = Get-ChildItem -Recurse -File -Include *.md, *.yml | Where-Object { $_.FullName -notmatch '\\(artifacts|bin|obj)\\' }
foreach ($f in $files) {
    $t = [IO.File]::ReadAllText($f.FullName)
    if ($t -match 'OWNER/') {
        [IO.File]::WriteAllText($f.FullName, $t.Replace('OWNER/', "$GitHubName/"), [Text.UTF8Encoding]::new($false))
        Write-Host "angepasst: $($f.Name)"
    }
}
if (git status --porcelain) {
    git add -A
    git commit -m "Links auf github.com/$GitHubName/$Repo gesetzt"
}

$url = "https://github.com/$GitHubName/$Repo.git"
if (git remote) { git remote set-url origin $url } else { git remote add origin $url }
git push -u origin main

if ($Release) {
    $v = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version
    git tag "v$v"
    git push origin "v$v"
    Write-Host "Release v$v angestoßen: https://github.com/$GitHubName/$Repo/actions" -ForegroundColor Green
}
Write-Host "Fertig: https://github.com/$GitHubName/$Repo" -ForegroundColor Green
