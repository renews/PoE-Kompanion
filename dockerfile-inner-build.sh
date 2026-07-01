#!/bin/bash
set -e

echo "Building PoE Kompanion in Docker container..."

# Extract version from csproj
VERSION=$(grep -oP '<AppVersion>\K[^<]+' PoEKompanion.csproj)
echo "Version: $VERSION"

# Build the application
echo "Running dotnet publish..."
dotnet publish -c Release -r linux-x64 --self-contained

# Build linuxdeploy arguments for all .so files
echo "Preparing linuxdeploy arguments..."
LINUXDEPLOY_ARGS=(
    --appdir AppDir
    --executable bin/Release/net9.0/linux-x64/publish/PoEKompanion
    --desktop-file poe-kompanion.desktop
    --icon-file poe-kompanion.png
)

# Add all .so files as libraries
for lib in bin/Release/net9.0/linux-x64/publish/*.so; do
    if [ -f "$lib" ]; then
        LINUXDEPLOY_ARGS+=(--library "$lib")
    fi
done

# Stage AppDir
echo "Staging AppDir with linuxdeploy..."
/usr/local/linuxdeploy/AppRun "${LINUXDEPLOY_ARGS[@]}"

# NativeAOT executables can crash with SIGBUS when run from the AppImage FUSE mount.
# Replace linuxdeploy's AppRun symlink with a wrapper that copies the payload to a
# real filesystem before executing it.
echo "Installing AppImage AppRun wrapper..."
sh ./scripts/install-appimage-wrapper.sh AppDir

# Create AppImage
echo "Creating AppImage with appimagetool..."
OUTPUT_NAME="PoE_Kompanion-x86_64.AppImage" \
ARCH=x86_64 \
/usr/local/linuxdeploy/plugins/linuxdeploy-plugin-appimage/usr/bin/appimagetool AppDir

# Rename AppImage to versioned name
APPIMAGE_NAME="PoE-Kompanion-${VERSION}-x86_64.AppImage"
mv PoE_Kompanion-x86_64.AppImage "$APPIMAGE_NAME"
echo "Created: $APPIMAGE_NAME"

# Create tar.gz archive from published directory
echo "Creating tar.gz archive..."
TARBALL_NAME="PoE-Kompanion-${VERSION}-x86_64.tar.gz"
cd bin/Release/net9.0/linux-x64/publish
tar -czf "/build/$TARBALL_NAME" .
cd /build
echo "Created: $TARBALL_NAME"

# Create output directory for export
mkdir -p /output
mv "$APPIMAGE_NAME" /output/
mv "$TARBALL_NAME" /output/

echo ""
echo "Build artifacts ready in /output:"
ls -lh /output/
