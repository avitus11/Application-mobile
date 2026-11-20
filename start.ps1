# Lance Quiz Party sur ce PC (Windows / PowerShell) : compile l'appli Angular puis démarre le serveur.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Push-Location client
if (-not (Test-Path node_modules)) { npm ci }
npm run build
Pop-Location

dotnet run --project server/QuizParty.Server
