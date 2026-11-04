# PoE Kompanion
A small utility for PoE on Linux, currently only containing a Logout Macro.
Aim to be a replacement to Lutcikaur's amazing [LutBot](http://lutbot.com).

## Screenshots
![tray-screenshot.png](Doc/tray-screenshot.png)

![settings-screenshot.png](Doc/settings-screenshot.png)

## Why It Asks for a Password

PoE Kompanion needs elevated permission to close the game's TCP connection for instant logout on Linux.

On first launch, the AppImage installs a small logout helper with administrator permission. After that, the app can trigger logout without asking for a password during gameplay.

The helper is limited to the logout action and checks that the target process is Path of Exile before closing its connection.

## Building from Source

### Prerequisites

- Docker (for AppImage builds)
- .NET 10.0.403 SDK (for manual builds; see `global.json`)

### Build and Publish (Recommended)

Build and publish release artifacts using Docker (ensures maximum compatibility across Linux distributions):

```bash
./publish.sh
```

This creates portable distribution packages in `publish/{VERSION}/`:
- `PoE-Kompanion-{VERSION}-x86_64.AppImage` - Portable single-file executable
- `PoE-Kompanion-{VERSION}-x86_64.tar.gz` - Traditional archive with all binaries

**What it does:**
- Builds in an isolated Ubuntu 22.04 container (glibc 2.35 baseline)
- Automatically bundles all required dependencies using linuxdeploy
- Produces both AppImage and tar.gz distribution formats
  - Output in ./publish/$VERSION/

### Build Manually

For development or if you don't have Docker:

```bash
dotnet publish PoE-Kompanion/PoEKompanion.csproj -r linux-x64 -c Release --self-contained
```

The compiled binary will be in `PoE-Kompanion/bin/Release/net10.0/linux-x64/publish/`

**Note:** Manual builds may not be portable across different Linux distributions due to glibc version differences. Use the Docker build for distribution.

### Validation

Run the regression suite from the repository root:

```bash
dotnet run --project test/PoEKompanion.Tests
sh test/install-appimage-wrapper.test.sh
sh test/logout-helper.test.sh # Linux only
```

CI also runs real keyboard press/release checks under Xvfb (`--native-hook-smoke`),
builds the NativeAOT AppImage and archive, and compiles the existing Windows target.
SharpHook 8 is explicitly configured to retain the previous XRecord backend,
including under XWayland; native Wayland input is not enabled by this update.
Release packages require x86-64 Linux with glibc 2.35 or newer (Ubuntu 22.04 or
newer, or an equivalent distribution). Older glibc 2.31 systems are no longer
supported: SharpHook 8 itself requires glibc 2.34, and Ubuntu 22.04 supplies the
supported packaging baseline.
Full in-game logout, desktop tray integration and compositor-specific behavior require
manual testing on the target Linux desktop.
