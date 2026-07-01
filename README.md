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
- .NET 9.0 SDK (for manual builds)

### Build and Publish (Recommended)

Build and publish release artifacts using Docker (ensures maximum compatibility across Linux distributions):

```bash
./publish.sh
```

This creates portable distribution packages in `publish/{VERSION}/`:
- `PoE-Kompanion-{VERSION}-x86_64.AppImage` - Portable single-file executable
- `PoE-Kompanion-{VERSION}-x86_64.tar.gz` - Traditional archive with all binaries

**What it does:**
- Builds in an isolated Debian 11 container for maximum compatibility
- Automatically bundles all required dependencies using linuxdeploy
- Produces both AppImage and tar.gz distribution formats
  - Output in ./publish/$VERSION/

### Build Manually

For development or if you don't have Docker:

```bash
dotnet publish -r linux-x64 -c Release --self-contained
```

The compiled binary will be in `bin/Release/net9.0/linux-x64/publish/`

**Note:** Manual builds may not be portable across different Linux distributions due to glibc version differences. Use the Docker build for distribution.
