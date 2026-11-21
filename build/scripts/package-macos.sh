#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMMAND="${1:-}"

fail() {
    echo "error: $*" >&2
    exit 1
}

usage() {
    cat <<EOF
usage:
  $0 zip <publish-dir> <output.zip>
  $0 dmg <publish-dir> <version> <output.dmg>
EOF
}

package_zip() {
    local source="$1"
    local output="$2"
    [[ -d "$source" ]] || fail "publish directory does not exist: $source"
    mkdir -p "$(dirname "$output")"
    ditto -c -k --sequesterRsrc "$source" "$output"
}

package_dmg() {
    local source="$1"
    local version="${2#v}"
    local output="$3"
    [[ -d "$source" ]] || fail "publish directory does not exist: $source"
    [[ -n "$version" ]] || fail "missing version"
    command -v hdiutil >/dev/null 2>&1 || fail "hdiutil not found"
    command -v clang >/dev/null 2>&1 || fail "clang not found"

    local app="$TMP/SourceFork.app"
    local contents="$app/Contents"
    mkdir -p "$contents/MacOS" "$contents/Resources"

    cp -a "$source/." "$contents/MacOS/"
    cp "$ROOT/build/resources/app/App.icns" "$contents/Resources/App.icns"
    sed "s/SOURCE_FORK_VERSION/$version/g" \
        "$ROOT/build/resources/app/App.plist" > "$contents/Info.plist"
    if [[ -f "$ROOT/LICENSE" ]]; then
        cp "$ROOT/LICENSE" "$contents/Resources/LICENSE"
    fi

    rm -rf "$contents/MacOS/SourceFork.dSYM"
    find "$contents/MacOS" -maxdepth 1 -type f -name '*.pdb' -delete
    clang "$ROOT/tools/setsid-macos/setsid.c" \
        -o "$contents/MacOS/setsid" -mmacosx-version-min=13.0

    local dmgroot="$TMP/dmg"
    mkdir -p "$dmgroot"
    cp -R "$app" "$dmgroot/SourceFork.app"
    ln -s /Applications "$dmgroot/Applications"

    mkdir -p "$(dirname "$output")"
    hdiutil create -volname "SourceFork $version" -srcfolder "$dmgroot" \
        -ov -format UDZO "$output" >/dev/null
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

case "$COMMAND" in
    zip)
        [[ $# -eq 3 ]] || { usage; exit 1; }
        package_zip "$2" "$3"
        ;;
    dmg)
        [[ $# -eq 4 ]] || { usage; exit 1; }
        package_dmg "$2" "$3" "$4"
        ;;
    *)
        usage
        exit 1
        ;;
esac
