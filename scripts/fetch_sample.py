"""Download a public indoor COCO image and record its provenance, without Git storage."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
URL = "http://images.cocodataset.org/val2017/000000000139.jpg"


def main():
    target = ROOT / "data" / "samples" / "coco_000000000139.jpg"
    if target.exists():
        print(f"Already present: {target}")
        return
    with urlopen(URL, timeout=45) as response:
        content = response.read(10_000_001)
    if len(content) > 10_000_000 or not content.startswith(b"\xff\xd8"):
        raise ValueError("Expected a JPEG under 10 MB; no output was written")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(content)
    target.with_suffix(".source.json").write_text(json.dumps({
        "dataset": "COCO val2017", "image_id": 139, "url": URL,
        "retrieved_at_utc": datetime.now(timezone.utc).isoformat(),
        "sha256": hashlib.sha256(content).hexdigest(),
        "rights": "Original photographer retains image rights. Not redistributed in this repository.",
        "terms": "https://cocodataset.org/#termsofuse"
    }, indent=2) + "\n", encoding="utf-8")
    print(target)


if __name__ == "__main__":
    main()
