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
  $0 tar <publish-dir> <output.tar.gz>
  $0 deb <publish-dir> <version> <output.deb>
  $0 appimage <publish-dir> <output.AppImage>

appimage requires APPIMAGETOOL to point at appimagetool.
EOF
}

copy_license() {
    local destination="$1"
    if [[ -f "$ROOT/LICENSE" ]]; then
        cp "$ROOT/LICENSE" "$destination/LICENSE"
    fi
}

package_tar() {
    local source="$1"
    local output="$2"
    [[ -d "$source" ]] || fail "publish directory does not exist: $source"
    mkdir -p "$(dirname "$output")"

    local stage="$TMP/tar/sourcefork"
    mkdir -p "$stage"
    cp -a "$source/." "$stage/"
    copy_license "$stage"
    tar -czf "$output" -C "$stage" .
}

package_deb() {
    local source="$1"
    local version="${2#v}"
    local output="$3"
    [[ -d "$source" ]] || fail "publish directory does not exist: $source"
    [[ -n "$version" ]] || fail "missing version"
    command -v dpkg-deb >/dev/null 2>&1 || fail "dpkg-deb not found"
    mkdir -p "$(dirname "$output")"

    local stage="$TMP/deb"
    mkdir -p "$stage/DEBIAN" \
        "$stage/opt/sourcefork" \
        "$stage/usr/bin" \
        "$stage/usr/share/applications" \
        "$stage/usr/share/icons/hicolor/256x256/apps" \
        "$stage/usr/share/metainfo" \
        "$stage/usr/share/doc/sourcefork"

    cp -a "$source/." "$stage/opt/sourcefork/"
    copy_license "$stage/usr/share/doc/sourcefork"
    ln -s ../opt/sourcefork/SourceFork "$stage/usr/bin/sourcefork"
    install -m 644 "$ROOT/build/resources/_common/applications/sourcefork.desktop" \
        "$stage/usr/share/applications/sourcefork.desktop"
    install -m 644 "$ROOT/build/resources/_common/icons/sourcefork.png" \
        "$stage/usr/share/icons/hicolor/256x256/apps/sourcefork.png"
    install -m 755 "$ROOT/build/resources/deb/DEBIAN/postinst" "$stage/DEBIAN/postinst"
    install -m 755 "$ROOT/build/resources/deb/DEBIAN/preinst" "$stage/DEBIAN/preinst"
    install -m 755 "$ROOT/build/resources/deb/DEBIAN/prerm" "$stage/DEBIAN/prerm"

    local installed_size
    installed_size="$(du -sk "$stage" | cut -f1)"
    local icu_deps="libicu"
    for v in 78 77 76 74 72 71 70 69 68 67 66 65 63; do
        icu_deps="$icu_deps | libicu$v"
    done

    sed -e "s/^Version:.*/Version: $version/" \
        -e "s/^Architecture:.*/Architecture: amd64/" \
        -e "s/^Installed-Size:.*/Installed-Size: $installed_size/" \
        -e "s/@ICU_DEPS@/$icu_deps/" \
        "$ROOT/build/resources/deb/DEBIAN/control" > "$stage/DEBIAN/control"

    dpkg-deb -Zgzip --root-owner-group --build "$stage" "$output" >/dev/null
}

package_appimage() {
    local source="$1"
    local output="$2"
    [[ -d "$source" ]] || fail "publish directory does not exist: $source"

    local tool="${APPIMAGETOOL:-}"
    if [[ -z "$tool" ]]; then
        tool="$(command -v appimagetool || true)"
    fi
    [[ -n "$tool" && -x "$tool" ]] || fail "appimagetool not found; set APPIMAGETOOL"

    local appdir="$TMP/SourceFork.AppDir"
    mkdir -p "$appdir/usr/bin" \
        "$appdir/usr/share/applications" \
        "$appdir/usr/share/icons/hicolor/256x256/apps" \
        "$appdir/usr/share/metainfo" \
        "$appdir/usr/share/doc/sourcefork"

    cp -a "$source/." "$appdir/usr/bin/"
    copy_license "$appdir/usr/share/doc/sourcefork"
    install -m 644 "$ROOT/build/resources/_common/icons/sourcefork.png" \
        "$appdir/usr/share/icons/hicolor/256x256/apps/com.whynbnb.sourcefork.png"
    cp "$appdir/usr/share/icons/hicolor/256x256/apps/com.whynbnb.sourcefork.png" \
        "$appdir/com.whynbnb.sourcefork.png"
    install -m 644 "$ROOT/build/resources/appimage/sourcefork.appdata.xml" \
        "$appdir/usr/share/metainfo/com.whynbnb.sourcefork.appdata.xml"

    sed -e 's|^Exec=.*|Exec=AppRun %u|' \
        -e 's|^Icon=.*|Icon=com.whynbnb.sourcefork|' \
        "$ROOT/build/resources/_common/applications/sourcefork.desktop" > "$appdir/com.whynbnb.sourcefork.desktop"
    install -m 644 "$appdir/com.whynbnb.sourcefork.desktop" \
        "$appdir/usr/share/applications/com.whynbnb.sourcefork.desktop"

    cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/SourceFork" "$@"
EOF
    chmod +x "$appdir/AppRun"

    mkdir -p "$(dirname "$output")"
    ARCH=x86_64 "$tool" --appimage-extract-and-run --no-appstream "$appdir" "$output"
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

case "$COMMAND" in
    tar)
        [[ $# -eq 3 ]] || { usage; exit 1; }
        package_tar "$2" "$3"
        ;;
    deb)
        [[ $# -eq 4 ]] || { usage; exit 1; }
        package_deb "$2" "$3" "$4"
        ;;
    appimage)
        [[ $# -eq 3 ]] || { usage; exit 1; }
        package_appimage "$2" "$3"
        ;;
    *)
        usage
        exit 1
        ;;
esac
