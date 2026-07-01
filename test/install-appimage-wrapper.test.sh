#!/bin/sh
set -eu

project_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
tmp_dir="$(mktemp -d)"

cleanup() {
    rm -rf "$tmp_dir"
}
trap cleanup EXIT

appdir="$tmp_dir/AppDir"
runtime_dir="$tmp_dir/runtime"
mkdir -p "$appdir/usr/bin" "$appdir/usr/lib" "$runtime_dir"

cat > "$appdir/usr/bin/PoEKompanion" <<'EOF'
#!/bin/sh
printf 'exe=%s\n' "$0"
printf 'appimage=%s\n' "${APPIMAGE:-}"
printf 'args=%s\n' "$*"
printf 'ld=%s\n' "${LD_LIBRARY_PATH:-}"
printf 'shell=%s\n' "${SHELL:-}"
printf 'appdir=%s\n' "${POE_KOMPANION_APPDIR:-}"
printf 'appimage_path=%s\n' "${POE_KOMPANION_APPIMAGE:-}"
EOF
chmod +x "$appdir/usr/bin/PoEKompanion"
printf 'fake library\n' > "$appdir/usr/lib/libfake.so"
ln -s usr/bin/PoEKompanion "$appdir/AppRun"

sh "$project_root/scripts/install-appimage-wrapper.sh" "$appdir"

if [ -L "$appdir/AppRun" ]; then
    echo "Expected AppRun to be a wrapper script, got symlink"
    exit 1
fi

expected_shell="$(command -v sh)"
output="$(APPIMAGE=/tmp/PoE-Kompanion.AppImage SHELL=sh XDG_RUNTIME_DIR="$runtime_dir" "$appdir/AppRun" alpha beta)"

case "$output" in
    *"exe=$runtime_dir/poe-kompanion-appimage-"*"/usr/bin/PoEKompanion"*) ;;
    *)
        echo "Expected copied executable to run from runtime dir"
        echo "$output"
        exit 1
        ;;
esac

case "$output" in
    *"appimage="*) ;;
    *)
        echo "Expected wrapper to clear APPIMAGE before executing payload"
        echo "$output"
        exit 1
        ;;
esac

case "$output" in
    *"args=alpha beta"*) ;;
    *)
        echo "Expected wrapper to forward arguments"
        echo "$output"
        exit 1
        ;;
esac

case "$output" in
    *"ld=$runtime_dir/poe-kompanion-appimage-"*"/usr/lib"*) ;;
    *)
        echo "Expected wrapper to prepend copied usr/lib to LD_LIBRARY_PATH"
        echo "$output"
        exit 1
        ;;
esac

case "$output" in
    *"shell=$expected_shell"*) ;;
    *)
        echo "Expected wrapper to normalize SHELL to an absolute path for pkexec"
        echo "$output"
        exit 1
        ;;
esac

case "$output" in
    *"appdir=$appdir"*) ;;
    *)
        echo "Expected wrapper to expose original AppDir for bundled helper setup"
        echo "$output"
        exit 1
        ;;
esac

case "$output" in
    *"appimage_path=/tmp/PoE-Kompanion.AppImage"*) ;;
    *)
        echo "Expected wrapper to expose original AppImage path for automatic helper setup"
        echo "$output"
        exit 1
        ;;
esac
