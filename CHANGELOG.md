# Changelog

All notable changes to PoE Kompanion will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

- Updated to .NET 10 LTS and current stable Avalonia, MessagePack, SharpHook and D-Bus packages.
- Preserved XRecord keyboard handling when migrating to SharpHook 8.
- Added regression, NativeAOT, native keyboard, vulnerability and packaging checks in CI.
- Raised the Linux release baseline to Ubuntu 22.04 / glibc 2.35; older glibc 2.31 systems are no longer supported.

## [1.1.3] - 2026-09-17

### Fixed
- System tray integration now works with Quickshell status notifier implementations.

## [1.1.2] - 2026-07-17

### Fixed
- Intermittent stuck keyboard input caused by overlapping global hooks during shortcut capture.
- Known MessagePack and D-Bus dependency vulnerabilities reported by NuGet.

## [1.1.1] - 2026-07-04

### Fixed
- Auto weapon swap configuration now serializes correctly in NativeAOT release builds.
- Auto weapon swap focus detection now supports Niri, Hyprland, and Sway compositor APIs before falling back to X11.
- Path of Exile 2 Xwayland proxy windows on Niri are now recognized as the active game window.

## [1.1.0] - 2026-07-04

### Added
- Configurable chat command hotkeys with editable commands and shortcut pickers.
- Auto weapon swap with configurable toggle hotkey, swap key, and randomized interval timing.

### Changed
- Moved hideout into the configurable command list.
- Auto weapon swap only runs while the active window belongs to Path of Exile.

## [1.0.0] - 2026-07-01

### Added
- One-time privileged logout helper installation for Linux AppImage builds.
- Path of Exile 1 and Path of Exile 2 process detection for Proton and native process names.

### Fixed
- AppImage startup on CachyOS/Niri by running the NativeAOT payload from a real filesystem.
- Logout hotkey behavior on systems where killing TCP connections requires elevated privileges.
- IPC socket startup crashes caused by stale root-owned sockets in global `/tmp`.

### Changed
- IPC sockets now live in per-user runtime directories.
- Background process starts unprivileged; only the logout helper uses narrow passwordless sudo after setup.

## [0.2.2] - 2025-11-24

### Changed
- AppImage now built in Debian 11 container for maximum compatibility across Linux distributions
- Reduced glibc requirement from 2.38 to 2.29 for broader system support

### Technical
- Implemented Docker-based build system using Debian 11 (bullseye)
- Integrated linuxdeploy for automatic dependency discovery and bundling
- All required system libraries (X11, image processing, etc.) now bundled in AppImage
- Build process fully isolated in Docker container for reproducible builds
- AppImage now compatible with Ubuntu 20.04+, Debian 10+, and most modern Linux distributions

## [0.2.1] - 2025-11-16

### Fixed
- Fixed crash when opening and closing the settings window multiple times
- Tentative fix for crash on startup for some Arch Linux users (bus error in AppImage)

### Technical
- Improved memory alignment in native interop code for better compatibility
- Hook now stays alive during settings window operations instead of being disposed

## [0.2.0] - 2025-11-02

### Added
- Support for modifier keys (Ctrl, Shift, Alt) in hotkey combinations
- Hideout macro with `/hideout` command (default: F5)
- Exit to character selection macro with `/exit` command (default: Ctrl+Shift+Space)
- Virtual keyboard implementation using uinput for sending chat commands
- Keyboard layout detection via xmodmap for proper character mapping
- HotkeyCombo class for managing key + modifier combinations
- Deferred execution for hotkeys with modifiers (fires on modifier release)

### Changed
- HotkeyPickerButton now captures and displays full key combinations with modifiers
- Configuration system updated to store HotkeyCombo instead of simple KeyCode
- Background process now receives keyboard layout map at startup

### Technical
- Added KeyboardLayoutHelper for X11 keyboard layout mapping
- Expanded IPC with ChatCommandMessage and KeyboardLayoutMapMessage
- PoETracker creates virtual keyboard device for text input simulation

## [0.1.0] - 2025-11-02

### Added
- Initial release
- Instant logout macro functionality (default: Backtick key)
- Configuration window for customizing hotkeys (default: F10)
- System tray integration with notifications
- Automatic Path of Exile process detection
- TCP connection tracking and termination for instant logout
- AppImage packaging for easy distribution
- IPC communication between foreground and background processes

### Technical
- Built with .NET 9 and Avalonia UI
- Uses SharpHook for global hotkey capture
- Unix domain socket IPC for process communication
- Background process runs with CAP_NET_ADMIN capability for network operations

[1.1.3]: https://github.com/renews/PoE-Kompanion/compare/v1.1.2...v1.1.3
[1.1.2]: https://github.com/renews/PoE-Kompanion/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/renews/PoE-Kompanion/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/renews/PoE-Kompanion/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/Samuel-B-D/PoE-Kompanion/compare/v0.2.2...v1.0.0
[0.2.2]: https://github.com/Samuel-B-D/PoE-Kompanion/compare/v0.2.1...v0.2.2
[0.2.1]: https://github.com/Samuel-B-D/PoE-Kompanion/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Samuel-B-D/PoE-Kompanion/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Samuel-B-D/PoE-Kompanion/releases/tag/v0.1.0
