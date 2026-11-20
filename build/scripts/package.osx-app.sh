#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

mkdir -p SourceFork.app/Contents/Resources
mv SourceFork SourceFork.app/Contents/MacOS
cp resources/app/App.icns SourceFork.app/Contents/Resources/App.icns
sed "s/SOURCE_FORK_VERSION/$VERSION/g" resources/app/App.plist > SourceFork.app/Contents/Info.plist
rm -rf SourceFork.app/Contents/MacOS/SourceFork.dsym
rm -f SourceFork.app/Contents/MacOS/*.pdb
clang ../tools/setsid-macos/setsid.c -o SourceFork.app/Contents/MacOS/setsid -mmacosx-version-min=13.0

zip "sourcefork_$VERSION.$RUNTIME.zip" -r SourceFork.app
