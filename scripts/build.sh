#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
task_dotnet="${DOTNET:-dotnet}"
task_game="${VALHEIM_PATH:-/home/deck/.steam/steam/steamapps/common/Valheim}"
"$task_dotnet" build "$task_root/WildGlow/WildGlow.csproj" -c Release -p:GamePath="$task_game" "$@"
