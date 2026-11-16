"""Check synthetic decoder inputs on the running Android room demo over USB."""
import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = "org.autoscene.roomlab"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", default=shutil.which("adb"))
    parser.add_argument("--serial")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    if not args.adb:
        parser.error("Provide --adb or place adb on PATH")
    adb = [args.adb] + (["-s", args.serial] if args.serial else [])
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)

    def shell(*tail):
        return subprocess.check_output(adb + ["shell", *tail], text=True, encoding="utf-8", timeout=20)

    focus = shell("dumpsys", "window")
    if not any(PACKAGE in line for line in focus.splitlines() if "mCurrentFocus" in line):
        raise RuntimeError("Open Room Intention Lab on the phone before running this check")
    active = json.loads(shell("cat", f"/sdcard/Android/data/{PACKAGE}/files/active-session.json"))
    session = active["session_directory"]
    if not session.startswith(f"/storage/emulated/0/Android/data/{PACKAGE}/files/Sessions/"):
        raise ValueError("Unexpected app session path")
    source = "phone_integration_test"
    probes = []

    def send(command, action="", stream=False):
        command_line = [sys.executable, str(ROOT / "scripts/send_phone_intent.py"), "--adb", args.adb,
                        "--source", source, "--command", command]
        if args.serial:
            command_line += ["--serial", args.serial]
        if command in ("select", "act"):
            command_line += ["--target", "mug_01", "--action", action]
        if stream:
            command_line += ["--velocity", ".1", ".05", "0", "--seconds", "1"]
        result = subprocess.run(command_line, check=True, capture_output=True, text=True, timeout=30)
        probes.append(json.loads(result.stdout))
        time.sleep(.3)

    start_ms = int(shell("date", "+%s%N")) / 1e6
    send("reset")
    send("select")
    send("act", "reach")
    time.sleep(2)
    send("act", "grasp")
    send("velocity", stream=True)
    time.sleep(.4)
    send("act", "release")
    send("reset")
    raw = shell("cat", session + "/events.jsonl")
    (output / "events.jsonl").write_text(raw, encoding="utf-8")
    events = []
    for line in raw.splitlines():
        try:
            event = json.loads(line)
            if event["unix_ms"] >= start_ms:
                events.append(event)
        except json.JSONDecodeError:
            pass  # The player may still be appending its final line.
    external = [e for e in events if e["source"] == source]
    accepted = [e for e in external if e["event_type"] == "accepted"]
    checks = {
        "external target selection": any(e["detail"].startswith("Selected ") for e in accepted),
        "external assisted reach": any(e["action"] == "reach" for e in accepted),
        "external grasp creates contact": any(e["event_type"] == "contact" for e in external),
        "streamed velocity accepted": any("Velocity applied" in e["detail"] for e in accepted),
        "external release": any(e["event_type"] == "release" for e in external),
        "no external messages rejected": not any(e["event_type"] == "rejected" for e in external),
    }
    poses = [e for e in events if e["event_type"] == "pose"]
    interval = (poses[-1]["unix_ms"] - poses[0]["unix_ms"]) / 1000 if len(poses) > 1 else 0
    cadence = (poses[-1]["frame"] - poses[0]["frame"]) / interval if interval else None
    result = {
        "passed": all(checks.values()), "checks": checks, "scene_version": active["scene_version"],
        "input_source": "synthetic USB messages", "neural_data_used": False,
        "accepted_messages": len(accepted), "messages_sent": sum(p["simulated_inputs_sent"] for p in probes),
        "clock_probes": probes,
        "observed_frame_cadence_fps": round(cadence, 2) if cadence is not None else None,
        "observation_seconds": round(interval, 2),
        "unique_head_rotations_logged": len({tuple(e["head_rotation"].values()) for e in poses}),
        "timing_scope": "Application frame counter divided by elapsed phone UTC; not motion-to-photon latency",
        "wearer_and_optical_calibration_verified": False,
    }
    (output / "input-validation.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    if not result["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
