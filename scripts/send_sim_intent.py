"""Send a simulated decoder message to the Unity file adapter; no neural or robot hardware."""
import argparse
import json
import os
import time
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--session-dir", type=Path, required=True)
    parser.add_argument("--command", choices=["select", "act", "velocity", "stop", "resume", "reset"], required=True)
    parser.add_argument("--target", default="")
    parser.add_argument("--action", default="")
    parser.add_argument("--velocity", type=float, nargs=3, default=[0, 0, 0], metavar=("X", "Y", "Z"))
    parser.add_argument("--source", default="simulated_decoder")
    parser.add_argument("--seconds", type=float, default=0, help="Stream velocity at 20 Hz for this long")
    args = parser.parse_args()
    if args.seconds < 0 or args.seconds > 60:
        parser.error("Choose a duration between 0 and 60 seconds")
    if args.seconds and args.command != "velocity":
        parser.error("Streaming is available for velocity commands only")
    catalog = json.loads((args.session_dir / "catalog.json").read_text(encoding="utf-8"))
    target_ids = {target["id"] for target in catalog["targets"]}
    if args.target and args.target not in target_ids:
        parser.error("The target is absent from this session's catalogue")
    deadline = time.monotonic() + args.seconds
    sequence = time.time_ns()
    count = 0
    while True:
        sequence += 1
        message = {
            "schema_version": 1, "scene_version": catalog["scene_version"],
            "source": args.source, "sequence": sequence, "timestamp_unix_ms": time.time_ns() / 1e6,
            "command": args.command, "target_id": args.target, "action": args.action,
            "velocity": dict(zip(["x", "y", "z"], args.velocity)), "ttl_ms": 250
        }
        temporary = args.session_dir / f".inbox-{os.getpid()}.tmp"
        temporary.write_text(json.dumps(message, allow_nan=False), encoding="utf-8")
        os.replace(temporary, args.session_dir / "inbox.json")
        count += 1
        if not args.seconds or time.monotonic() >= deadline:
            break
        time.sleep(.05)
    print(f"Sent {count} simulated input(s). Inspect events.jsonl for accepted or rejected results.")


if __name__ == "__main__":
    main()
