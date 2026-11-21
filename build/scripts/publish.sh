#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="${PROJECT:-$ROOT/src/SourceFork.csproj}"

MODE="${1:-}"
RID="${2:-}"
OUTPUT="${3:-}"

fail() {
    echo "error: $*" >&2
    exit 1
}

if [[ -z "$MODE" || -z "$RID" || -z "$OUTPUT" ]]; then
    fail "usage: $0 <framework|aot|aot-static> <rid> <output-dir>"
fi

case "$MODE" in
    framework|aot|aot-static) ;;
    *) fail "unknown mode: $MODE" ;;
esac

mkdir -p "$OUTPUT"
OUTPUT_ABS="$(cd "$OUTPUT" && pwd)"
case "$OUTPUT_ABS" in
    "/"|"$HOME"|"$ROOT") fail "refusing to clean dangerous directory: $OUTPUT_ABS" ;;
esac

find "$OUTPUT_ABS" -mindepth 1 -maxdepth 1 -exec rm -rf {} +

ARGS=(
    publish "$PROJECT"
    -c Release
    -r "$RID"
    -o "$OUTPUT_ABS"
    -p:DebugType=None
    -p:DebugSymbols=false
)

case "$MODE" in
    framework)
        ARGS+=(
            --self-contained false
            -p:DisableAOT=true
            -p:PublishAot=false
            -p:PublishTrimmed=false
            -p:PublishSingleFile=false
        )
        ;;
    aot)
        ARGS+=(
            --self-contained true
            -p:DisableAOT=false
            -p:PublishAot=true
            -p:PublishTrimmed=true
            -p:TrimMode=link
            -p:StripSymbols=true
            -p:PublishSingleFile=false
        )
        ;;
    aot-static)
        ARGS+=(
            --self-contained true
            -p:DisableAOT=false
            -p:PublishAot=true
            -p:PublishTrimmed=true
            -p:TrimMode=link
            -p:StripSymbols=true
            -p:PublishSingleFile=false
            -p:UseStaticLinkAvalonia=true
        )
        ;;
esac

echo "==> SourceFork publish"
echo "    mode:   $MODE"
echo "    rid:    $RID"
echo "    output: $OUTPUT_ABS"

dotnet "${ARGS[@]}"

find "$OUTPUT_ABS" -maxdepth 1 -type f -name '*.pdb' -delete
find "$OUTPUT_ABS" -maxdepth 1 -type d -name '*.dSYM' -exec rm -rf {} +

EXE="$OUTPUT_ABS/SourceFork"
if [[ "$RID" == win* ]]; then
    EXE="$OUTPUT_ABS/SourceFork.exe"
fi

if [[ "$MODE" != "framework" ]]; then
    [[ -f "$EXE" ]] || fail "AOT publish did not produce $EXE"
    [[ ! -f "$OUTPUT_ABS/SourceFork.dll" ]] || fail "AOT output still contains managed assemblies"
fi

if [[ "$MODE" == "aot-static" ]]; then
    count="$(find "$OUTPUT_ABS" -maxdepth 1 -type f | wc -l | tr -d ' ')"
    if [[ "$count" -ne 1 ]]; then
        find "$OUTPUT_ABS" -maxdepth 1 -type f -print >&2
        fail "aot-static output must contain a single executable"
    fi
fi

echo "==> done"
