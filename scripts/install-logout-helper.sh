#!/bin/sh
set -eu

helper_source="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)/poe-kompanion-kill-connection"
helper_target="/usr/local/libexec/poe-kompanion-kill-connection"
sudoers_target="/etc/sudoers.d/poe-kompanion"

if [ "$(id -u)" -ne 0 ]; then
    echo "Run this installer with sudo." >&2
    exit 1
fi

target_user="${1:-${SUDO_USER:-}}"
if [ -z "$target_user" ] || [ "$target_user" = "root" ]; then
    echo "Usage: sudo $0 <username>" >&2
    exit 2
fi

case "$target_user" in
    *[!A-Za-z0-9_.-]*)
        echo "Refusing invalid username: $target_user" >&2
        exit 2
        ;;
esac

if ! /usr/bin/id -u "$target_user" >/dev/null 2>&1; then
    echo "User does not exist: $target_user" >&2
    exit 2
fi

if [ ! -f "$helper_source" ]; then
    echo "Helper source not found: $helper_source" >&2
    exit 1
fi

visudo_path="$(command -v visudo)"
tmp_sudoers="$(mktemp)"
cleanup() {
    rm -f "$tmp_sudoers"
}
trap cleanup EXIT

/usr/bin/install -d -m 755 /usr/local/libexec
/usr/bin/install -o root -g root -m 755 "$helper_source" "$helper_target"

cat > "$tmp_sudoers" <<EOF
# Managed by PoE Kompanion.
$target_user ALL=(root) NOPASSWD: $helper_target *
EOF

"$visudo_path" -c -f "$tmp_sudoers" >/dev/null
/usr/bin/install -o root -g root -m 440 "$tmp_sudoers" "$sudoers_target"
"$visudo_path" -c -f "$sudoers_target" >/dev/null

echo "Installed PoE Kompanion logout helper for $target_user."
