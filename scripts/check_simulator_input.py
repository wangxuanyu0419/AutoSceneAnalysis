"""Exercise the real external-input path against a hidden standalone simulator."""
import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def events(path):
    result = []
    if not path.exists():
        return result
    for line in path.read_text(encoding="utf-8").splitlines():
        try:
            result.append(json.loads(line))
        except json.JSONDecodeError:
            pass  # The last line may still be being appended by the player.
    return result


def wait_for(predicate, seconds=10):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        if predicate():
            return True
        time.sleep(.05)
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    exe = ROOT / "unity/AutoSceneVR/Builds/Windows/RoomIntentionLab.exe"
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 0
    player = subprocess.Popen([str(exe), "--session-dir", str(output), "-logFile", str(output / "player.log")], startupinfo=startup)
    log = output / "events.jsonl"
    checks = {}

    def send(command, action=""):
        subprocess.run([sys.executable, str(ROOT / "scripts/send_sim_intent.py"), "--session-dir", str(output),
                        "--source", "integration_test", "--command", command, "--target", "mug_01", "--action", action],
                       check=True, capture_output=True, text=True)

    def accepted(action):
        return any(e["event_type"] == "accepted" and e["source"] == "integration_test" and e["action"] == action for e in events(log))

    try:
        if not wait_for(lambda: (output / "catalog.json").exists(), 25):
            raise RuntimeError("Player did not write its target catalogue")
        send("select")
        checks["external target selection"] = wait_for(lambda: accepted(""))
        send("act", "reach")
        checks["external action selection"] = wait_for(lambda: accepted("reach"))
        time.sleep(2)
        send("act", "grasp")
        checks["external grasp creates contact"] = wait_for(lambda: any(e["event_type"] == "contact" and e["source"] == "integration_test" for e in events(log)))
        send("act", "release")
        checks["external release"] = wait_for(lambda: accepted("release"))
        subprocess.run([sys.executable, str(ROOT / "scripts/send_sim_intent.py"), "--session-dir", str(output),
                        "--source", "integration_test", "--command", "velocity", "--velocity", ".1", ".05", "0", "--seconds", "1"],
                       check=True, capture_output=True, text=True)
        checks["streamed velocity accepted"] = wait_for(lambda: any(e["event_type"] == "accepted" and "Velocity applied" in e["detail"] for e in events(log)))
        time.sleep(.5)
        checks["no external messages rejected"] = not any(e["event_type"] == "rejected" and e["source"] == "integration_test" for e in events(log))
        result = {"passed": all(checks.values()), "checks": checks, "input_source": "synthetic external messages", "neural_data_used": False}
        (output / "input-validation.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(result, indent=2))
        if not result["passed"]:
            raise SystemExit(1)
    finally:
        # This is the test player started above, never an editor or another project's job.
        if player.poll() is None:
            player.terminate()
            player.wait(timeout=10)


if __name__ == "__main__":
    main()
