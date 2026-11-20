#!/usr/bin/env bash
# Lance Quiz Party sur ce PC (macOS / Linux) : compile l'appli Angular puis démarre le serveur.
set -euo pipefail
cd "$(dirname "$0")"

(cd client && { [ -d node_modules ] || npm ci; } && npm run build)
dotnet run --project server/QuizParty.Server
