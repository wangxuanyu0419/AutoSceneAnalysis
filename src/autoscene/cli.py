import argparse
import hashlib
import importlib.metadata
import json
import os
import platform
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps

from .report import save_artifacts
from .scene import build_scene

ROOT = Path(__file__).resolve().parents[2]


def positive(value):
    number = int(value)
    if number < 1:
        raise argparse.ArgumentTypeError("Must be at least 1")
    return number


def main():
    parser = argparse.ArgumentParser(description="Build a visible object/action menu from one image")
    parser.add_argument("--image", type=Path)
    parser.add_argument("--config", type=Path, default=ROOT / "configs" / "room.json")
    parser.add_argument("--actions", type=Path, default=ROOT / "configs" / "actions.json")
    parser.add_argument("--out", type=Path)
    device_group = parser.add_mutually_exclusive_group()
    device_group.add_argument("--device", help="cpu or a PyTorch CUDA index, e.g. cuda:0")
    device_group.add_argument("--gpu-name", help="Unique substring of the GPU name, e.g. 'RTX 2060'")
    parser.add_argument("--warmups", type=int, default=1)
    parser.add_argument("--repeats", type=positive, default=5)
    parser.add_argument("--threads", type=positive, default=4)
    parser.add_argument("--offline", action="store_true", help="Use cached model files only")
    parser.add_argument("--fixture", action="store_true", help="Synthetic UI example; does not run any models")
    args = parser.parse_args()
    args.device = args.device or "cpu"
    if args.warmups < 0:
        parser.error("--warmups must be zero or greater")
    if not args.fixture and args.image is None:
        parser.error("Provide --image, or use --fixture for the synthetic interface example")
    if args.fixture and args.image is not None:
        parser.error("--fixture generates its own image; omit --image")
    config = json.loads(args.config.read_text(encoding="utf-8"))
    action_config = json.loads(args.actions.read_text(encoding="utf-8"))
    if not config["prompts"] or any(not isinstance(p, str) or not p.strip() for p in config["prompts"]):
        parser.error("The prompt list must contain nonempty strings")
    if not (0 <= config["box_threshold"] <= 1 and 0 <= config["text_threshold"] <= 1 and 0 <= config["nms_iou"] <= 1):
        parser.error("Thresholds must be between 0 and 1")
    output = args.out or ROOT / "outputs" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    if output.exists():
        parser.error(f"Output already exists; choose a new run directory: {output}")
    if args.fixture:
        image = Image.new("RGB", (960, 600), (228, 231, 226))
        draw = ImageDraw.Draw(image)
        draw.rectangle((0, 380, 960, 600), fill=(149, 120, 94))
        draw.rectangle((100, 280, 600, 400), fill=(85, 100, 125))
        draw.rectangle((300, 220, 350, 280), fill=(210, 119, 91))
        detections = [{"label": "table", "score": 1.0, "bbox": [100, 280, 600, 400]},
                      {"label": "cup", "score": 1.0, "bbox": [300, 220, 350, 280]}]
        depth = np.broadcast_to(np.linspace(0, 1, 600, dtype=np.float32)[:, None], (600, 960)).copy()
        model_info = {"detector": {"id": "synthetic_fixture", "revision": None},
                      "depth": {"id": "synthetic_fixture", "revision": None}, "precision": "none", "segmentation": "not_implemented"}
        metrics = {"mode": "fixture", "perception_ms": None, "repeats": 0, "warmups": 0, "device_name": "none"}
    else:
        image = ImageOps.exif_transpose(Image.open(args.image)).convert("RGB")
        os.environ["HF_HOME"] = str(ROOT / ".cache" / "huggingface")
        os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"
        if args.offline:
            os.environ["HF_HUB_OFFLINE"] = "1"
            os.environ["TRANSFORMERS_OFFLINE"] = "1"
        import torch
        from .perception import Baseline, sync
        torch.set_num_threads(args.threads)
        if args.gpu_name:
            matches = [i for i in range(torch.cuda.device_count())
                       if args.gpu_name.lower() in torch.cuda.get_device_name(i).lower()]
            if len(matches) != 1:
                parser.error("--gpu-name must match exactly one available PyTorch CUDA device")
            args.device = f"cuda:{matches[0]}"
        if torch.device(args.device).type == "cuda":
            if not torch.cuda.is_available():
                parser.error("CUDA is unavailable in this environment; use --device cpu or install the CUDA build")
            torch.cuda.set_device(args.device)
            torch.cuda.reset_peak_memory_stats(args.device)
            print(f"Selected {args.device}: {torch.cuda.get_device_name(args.device)}", flush=True)
        print("Loading baseline models. The first run downloads weights.", flush=True)
        started = time.perf_counter()
        backend = Baseline(config, args.device)
        sync(args.device)
        load_ms = (time.perf_counter() - started) * 1000
        samples, cold_sample = [], None
        for i in range(args.warmups + args.repeats):
            detections, depth, sample = backend.predict(image)
            if cold_sample is None:
                cold_sample = sample
            if i >= args.warmups:
                samples.append(sample)
            print(f"{'Warmup' if i < args.warmups else 'Sample'} {i + 1}: {sample['perception_ms']:.0f} ms", flush=True)
        model_info = backend.model_info
        metrics = {
            "mode": "model_inference", "device": args.device,
            "device_name": torch.cuda.get_device_name(args.device) if torch.device(args.device).type == "cuda" else platform.processor() or "CPU",
            "precision": "float32", "repeats": args.repeats, "warmups": args.warmups,
            "cpu_threads": args.threads, "model_load_and_download_ms": load_ms,
            "first_perception_ms": cold_sample["perception_ms"], "samples": samples,
            "peak_torch_allocated_mib": torch.cuda.max_memory_allocated(args.device) / 1024 ** 2 if torch.device(args.device).type == "cuda" else None,
            "peak_torch_reserved_mib": torch.cuda.max_memory_reserved(args.device) / 1024 ** 2 if torch.device(args.device).type == "cuda" else None,
            "perception_ms": {"p50": float(np.percentile([s["perception_ms"] for s in samples], 50)),
                              "p95": float(np.percentile([s["perception_ms"] for s in samples], 95))},
            "scope": "CPU preprocessing, transfer, detection, postprocessing and relative depth; synchronized CUDA; excludes camera, report, neural decoder and robot",
            "package_versions": {n: importlib.metadata.version(n) for n in ["torch", "torchvision", "transformers", "numpy", "Pillow"]},
            "python": sys.version, "platform": platform.platform()
        }
        backend.close()
    upright = io_image_bytes(image)
    image_hash = hashlib.sha256(upright).hexdigest()
    started = time.perf_counter()
    scene = build_scene(detections, depth, image.width, image.height, image_hash, model_info, config, action_config)
    metrics["scene_assembly_ms"] = (time.perf_counter() - started) * 1000
    metrics["image_size_px"] = list(image.size)
    metrics["input_file_sha256"] = hashlib.sha256(args.image.read_bytes()).hexdigest() if args.image else None
    metrics["hash_definition"] = "scene hash covers upright decoded RGB pixels; input_file_sha256 covers original file bytes"
    from jsonschema import Draft202012Validator
    schema = json.loads((ROOT / "schemas" / "scene.schema.json").read_text(encoding="utf-8"))
    Draft202012Validator(schema).validate(scene)
    output.mkdir(parents=True)
    save_artifacts(image, depth, scene, metrics, output)
    print(f"\n{len(scene['objects'])} object candidates\nReport: {output.resolve() / 'report.html'}")


def io_image_bytes(image):
    # Include image dimensions to distinguish differently shaped pixel buffers.
    return f"{image.width}x{image.height}:RGB:".encode() + image.tobytes()


if __name__ == "__main__":
    main()
