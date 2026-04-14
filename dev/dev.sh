#!/bin/bash
# dev.sh - one-shot: build + deploy (elevated) + run tests
# Usage: ./dev.sh [--no-test] [--no-deploy]

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

DO_TEST=1
DO_DEPLOY=1
for a in "$@"; do
    case "$a" in
        --no-test)   DO_TEST=0 ;;
        --no-deploy) DO_DEPLOY=0 ;;
    esac
done

echo "=== [1/3] Build ==="
dotnet build -c Release 2>&1 | grep -E "error|Build succeeded|Errors" | tail -5
if ! dotnet build -c Release 2>&1 | tail -1 | grep -q "Build succeeded\|Génération réussie"; then
    echo "BUILD FAILED — aborting"
    exit 1
fi

if [ "$DO_DEPLOY" = "1" ]; then
    echo ""
    echo "=== [2/3] Deploy (UAC prompt) ==="
    powershell.exe -Command "Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','$ROOT\\dev\\deploy.ps1'"
    echo "Waiting 4s for Paint.NET to load..."
    sleep 4
fi

if [ "$DO_TEST" = "1" ]; then
    echo ""
    echo "=== [3/3] API tests ==="
    echo "(Make sure you've clicked Effects -> Tools -> Macro API in Paint.NET)"
    echo "Press Enter when ready, or Ctrl+C to skip..."
    read -r
    python "$ROOT/dev/test_api.py"
fi

echo ""
echo "Done."
