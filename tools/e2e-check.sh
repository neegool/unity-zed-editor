#!/usr/bin/env bash
# Runs Unity headless against the scratch project and regenerates project files.
# Verifies the package loads, registers, and generates - which compile-check cannot.
#
# Usage: tools/e2e-check.sh [unity-exe]
#
# Exit codes - three outcomes that must not be confused for one another:
#   0  Unity ran and generated .csproj/.sln
#   1  Unity ran cleanly but generated nothing (no editor installation discovered)
#   2  Unity could not run at all (project locked by another instance, or crashed)
#
# Troubleshooting: if Unity crashes at startup with
#   "Baselib_BinarySemaphore ... Destruction is not allowed when there are still
#    threads waiting on the semaphore"
# preceded by "IPCStream (Upm-NNNN): IPC stream failed to read (Not connected)",
# a stale Unity.Licensing.Client process is wedging package-manager IPC. Kill it:
#   powershell -NoProfile -Command "Stop-Process -Name Unity.Licensing.Client -Force"
set -euo pipefail

UNITY="${1:-C:/Program Files/Unity/Hub/Editor/6000.0.79f1/Editor/Unity.exe}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -W 2>/dev/null || pwd)"
PROJECT="$ROOT/.scratch/ZedPackageTest"
LOG="$ROOT/tools/.e2e-check.log"

[ -d "$PROJECT" ] || { echo "No scratch project at $PROJECT - see Task 4 Step 1" >&2; exit 2; }

# Unity refuses to open a project another instance holds. Fail fast with a clear
# message rather than letting the abort look like a run that generated nothing.
# The lockfile is 0 bytes and lingers after a crash, so probe writability rather
# than mere existence.
LOCK="$PROJECT/Temp/UnityLockfile"
if [ -f "$LOCK" ] && ! : > "$LOCK" 2>/dev/null; then
  echo "e2e-check: ABORTED - Unity already has $PROJECT open" >&2
  exit 2
fi

"$UNITY" -batchmode -quit -projectPath "$PROJECT" \
  -executeMethod Neegool.Unity.Zed.Editor.Cli.GenerateSolution \
  -logFile - > "$LOG" 2>&1 || true

grep -iE "zed|error|exception|\.csproj|\.sln" "$LOG" || true

if grep -qi "another Unity instance is running\|Aborting batchmode" "$LOG"; then
  echo "e2e-check: ABORTED - Unity exited before running (see $LOG)" >&2
  exit 2
fi

echo "--- generated files ---"
# Unity 6000's SDK-style generator emits .slnx, not .sln. Accept either: the
# generator picks one and actively deletes the other.
if compgen -G "$PROJECT/*.csproj" > /dev/null \
   && { compgen -G "$PROJECT/*.sln" > /dev/null || compgen -G "$PROJECT/*.slnx" > /dev/null; }; then
  ls "$PROJECT"/*.csproj "$PROJECT"/*.sln "$PROJECT"/*.slnx "$PROJECT"/.zed/* 2>/dev/null || true
  echo "e2e-check: OK"
  exit 0
fi

echo "(none)"
echo "e2e-check: RAN, but generated no .csproj/.sln[x] - no editor installation discovered (see $LOG)" >&2
exit 1
