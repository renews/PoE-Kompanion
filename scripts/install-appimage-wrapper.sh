#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    echo "Usage: $0 <AppDir>" >&2
    exit 2
fi

appdir="$1"
payload="$appdir/usr/bin/PoEKompanion"

if [ ! -x "$payload" ]; then
    echo "Expected executable not found: $payload" >&2
    exit 1
fi

rm -f "$appdir/AppRun"

cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
set -eu

if [ -n "${APPDIR:-}" ]; then
    appdir_path="$APPDIR"
else
    appdir_path="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
fi

user_id="$(id -u 2>/dev/null || printf '%s' "${USER:-user}")"
runtime_base="${XDG_RUNTIME_DIR:-${TMPDIR:-/tmp}}"
payload_dir="$runtime_base/poe-kompanion-appimage-$user_id"

mkdir -p "$payload_dir/usr/bin" "$payload_dir/usr/lib"

cp -f "$appdir_path/usr/bin/PoEKompanion" "$payload_dir/usr/bin/PoEKompanion"
chmod 755 "$payload_dir/usr/bin/PoEKompanion"

for lib in "$appdir_path"/usr/lib/*; do
    [ -f "$lib" ] || continue
    cp -f "$lib" "$payload_dir/usr/lib/"
done

unset APPIMAGE

if [ -n "${SHELL:-}" ] && [ "${SHELL#/}" = "$SHELL" ]; then
    shell_path="$(command -v "$SHELL" 2>/dev/null || true)"
    if [ -n "$shell_path" ]; then
        export SHELL="$shell_path"
    else
        unset SHELL
    fi
fi

if [ -n "${LD_LIBRARY_PATH:-}" ]; then
    export LD_LIBRARY_PATH="$payload_dir/usr/lib:$LD_LIBRARY_PATH"
else
    export LD_LIBRARY_PATH="$payload_dir/usr/lib"
fi

exec "$payload_dir/usr/bin/PoEKompanion" "$@"
EOF

chmod +x "$appdir/AppRun"
