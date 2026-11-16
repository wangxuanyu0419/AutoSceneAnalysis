"""Send simulated decoder commands over USB to this app's Android file adapter."""
import argparse
import json
import re
import shutil
import subprocess
import time

PACKAGE = "org.autoscene.roomlab"
BASE = f"/sdcard/Android/data/{PACKAGE}/files"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", default=shutil.which("adb"), help="Path to adb when it is not on PATH")
    parser.add_argument("--serial", help="Device serial; required when several devices are connected")
    parser.add_argument("--command", choices=["select", "act", "velocity", "stop", "resume", "reset"], required=True)
    parser.add_argument("--target", default="")
    parser.add_argument("--action", default="")
    parser.add_argument("--velocity", type=float, nargs=3, default=[0, 0, 0])
    parser.add_argument("--seconds", type=float, default=0)
    parser.add_argument("--source", default="simulated_usb_decoder")
    args = parser.parse_args()
    if not args.adb:
        parser.error("Provide --adb or place the Android platform-tools directory on PATH")
    if not 0 <= args.seconds <= 60 or (args.seconds and args.command != "velocity"):
        parser.error("A 0–60 second stream is available for velocity commands only")
    adb = [args.adb] + (["-s", args.serial] if args.serial else [])

    def read(*tail):
        return subprocess.check_output(adb + list(tail), text=True, encoding="utf-8", timeout=15).strip()

    active = json.loads(read("shell", "cat", BASE + "/active-session.json"))
    session = active["session_directory"]
    allowed_prefix = f"/storage/emulated/0/Android/data/{PACKAGE}/files/Sessions/"
    if not session.startswith(allowed_prefix) or not re.fullmatch(r"[A-Za-z0-9_/.-]+", session) or ".." in session:
        raise ValueError("Refusing to write outside this app's own session directory")
    catalog = json.loads(read("shell", "cat", session + "/catalog.json"))
    if args.target and args.target not in {t["id"] for t in catalog["targets"]}:
        parser.error("The target is absent from the active phone scene")
    start = time.time_ns() / 1e6
    phone_ms = int(read("shell", "date", "+%s%N")) / 1e6
    end = time.time_ns() / 1e6
    offset = phone_ms - (start + end) / 2
    # Only validated app-owned paths enter this shell program. Messages are stdin data.
    shell_program = ("while IFS= read -r line; do printf '%s' \"$line\" > "
                     + session + "/inbox.usb.tmp; mv " + session + "/inbox.usb.tmp " + session + "/inbox.json; done")
    process = subprocess.Popen(adb + ["shell", shell_program], stdin=subprocess.PIPE,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8")
    sequence = time.time_ns()
    deadline = time.monotonic() + args.seconds
    count = 0
    try:
        while True:
            sequence += 1
            message = {
                "schema_version": 1, "scene_version": catalog["scene_version"], "source": args.source,
                "sequence": sequence, "timestamp_unix_ms": time.time_ns() / 1e6 + offset,
                "command": args.command, "target_id": args.target, "action": args.action,
                "velocity": dict(zip(["x", "y", "z"], args.velocity)), "ttl_ms": 250
            }
            process.stdin.write(json.dumps(message, allow_nan=False) + "\n")
            process.stdin.flush()
            count += 1
            if not args.seconds or time.monotonic() >= deadline:
                break
            time.sleep(.05)
        process.stdin.close()
        if process.wait(timeout=10) != 0:
            raise RuntimeError(process.stderr.read())
    finally:
        if process.poll() is None:
            process.terminate()
    print(json.dumps({"simulated_inputs_sent": count, "clock_probe_round_trip_ms": round(end - start, 2),
                      "clock_offset_ms": round(offset, 2), "scene_version": catalog["scene_version"]}, indent=2))


if __name__ == "__main__":
    main()
