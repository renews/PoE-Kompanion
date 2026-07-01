#!/bin/sh
set -eu

project_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
helper="$project_root/scripts/poe-kompanion-kill-connection"

if [ ! -x "$helper" ]; then
    echo "Expected executable helper at $helper"
    exit 1
fi

assert_status_and_output() {
    expected_status="$1"
    expected_text="$2"
    shift 2

    set +e
    output="$("$@" 2>&1)"
    status="$?"
    set -e

    if [ "$status" -ne "$expected_status" ]; then
        echo "Expected exit $expected_status, got $status"
        echo "$output"
        exit 1
    fi

    case "$output" in
        *"$expected_text"*) ;;
        *)
            echo "Expected output to contain: $expected_text"
            echo "$output"
            exit 1
            ;;
    esac
}

assert_status_and_output 2 "Usage:" "$helper" abc 127.0.0.1 443
assert_status_and_output 1 "not a supported Path of Exile process" "$helper" "$$" 127.0.0.1 443

python3 -c 'import ctypes, time; ctypes.CDLL(None).prctl(15, b"Main", 0, 0, 0); time.sleep(30)' PathOfExileSteam.exe &
fake_poe_pid="$!"
trap 'kill "$fake_poe_pid" 2>/dev/null || true' EXIT

sleep 0.2
assert_status_and_output 1 "Connection is not owned by the supplied Path of Exile process" "$helper" "$fake_poe_pid" 127.0.0.1 9
