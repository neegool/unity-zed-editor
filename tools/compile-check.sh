#!/usr/bin/env bash
# Compiles Editor/ against Unity's assemblies using the Roslyn compiler Unity ships.
# Verifies the package builds without launching Unity or a host project.
# Usage: tools/compile-check.sh [unity-editor-data-dir]
set -euo pipefail

UNITY_DATA="${1:-C:/Program Files/Unity/Hub/Editor/6000.0.79f1/Editor/Data}"
CSC="$UNITY_DATA/DotNetSdkRoslyn/csc.dll"
# pwd -W emits a Windows path; csc.dll is a Windows binary and cannot read /d/... form.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -W 2>/dev/null || pwd)"
RSP="$ROOT/tools/.compile-check.rsp"

[ -f "$CSC" ] || { echo "No Roslyn at: $CSC" >&2; exit 1; }

{
  echo "-nostdlib"
  echo "-target:library"
  echo "-langversion:9"
  echo "-warnaserror-"
  echo "-out:$ROOT/tools/.compile-check.dll"
  for d in UNITY_EDITOR UNITY_EDITOR_WIN UNITY_2021_3_OR_NEWER UNITY_2020_2_OR_NEWER; do echo "-define:$d"; done
  # Response file needed: Unity's install path contains spaces.
  ls "$UNITY_DATA/Managed/UnityEngine.dll" \
     "$UNITY_DATA/Managed/UnityEditor.dll" \
     "$UNITY_DATA/Managed/UnityEngine/"*.dll \
     "$UNITY_DATA/NetStandard/ref/2.1.0/netstandard.dll" | sed 's|.*|-r:"&"|'
  find "$ROOT/Editor" -name '*.cs' | sed 's|.*|"&"|'
} > "$RSP"

dotnet "$CSC" "@$RSP"
echo "compile-check: OK"
