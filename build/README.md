# build

> [!WARNING]
> The files under the `build` folder are used by GitHub Actions only, **NOT** for end users.

## Local release builds

All publish/package scripts live in `build/scripts`.

```sh
# Framework-dependent (requires the .NET 10 Desktop Runtime)
build/scripts/publish.sh framework osx-arm64 /tmp/sourcefork-framework
build/scripts/package-macos.sh zip /tmp/sourcefork-framework /tmp/sourcefork-macos-arm64-framework.zip

# NativeAOT (requires a C/C++ toolchain)
build/scripts/publish.sh aot linux-x64 /tmp/sourcefork-aot
build/scripts/package-linux.sh tar /tmp/sourcefork-aot /tmp/sourcefork-linux-x64-aot.tar.gz

# NativeAOT with static Avalonia/Skia linking
build/scripts/publish.sh aot-static linux-x64 /tmp/sourcefork-aot-static
```

Supported modes:

- `framework` — framework-dependent build
- `aot` — NativeAOT self-contained build
- `aot-static` — NativeAOT with the `StaticLink.Avalonia` packages

`aot-static` also needs `-p:UseStaticLinkAvalonia=true`, which the publish script sets. On macOS, the `StaticLink.Avalonia.Native` package is restored automatically.

`aot-static` statically links Avalonia, SkiaSharp, HarfBuzz, and TextMateSharp's `libonigwrap`, so the output is a single executable.

## Release assets

The release workflow creates these assets:

- `sourcefork-<version>-linux-x64-framework.tar.gz`
- `sourcefork-<version>-linux-x64-aot.AppImage`
- `sourcefork-<version>-linux-x64-aot-static.tar.gz`
- `sourcefork_<version>_amd64.deb`
- `sourcefork-<version>-macos-arm64-framework.zip`
- `sourcefork-<version>-macos-arm64-aot.dmg`
- `sourcefork-<version>-win-x64-framework.zip`
- `sourcefork-<version>-win-x64-aot.zip`
- `sourcefork-<version>-win-x64-aot-static.zip`
