"""Reconstruct camera-aligned surfaces and object candidates from a single RGB image."""
import argparse
import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
os.environ.setdefault("HF_HOME", str(ROOT / ".cache/huggingface"))
sys.path.insert(0, str(ROOT / "src"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--review", type=Path, help="Optional reviewed 2D observations; never 3D simulator truth")
    parser.add_argument("--model", default="Ruicheng/moge-2-vitb-normal")
    parser.add_argument("--revision")
    parser.add_argument("--gpu-name", default="RTX 2060")
    parser.add_argument("--device",choices=["cpu"])
    parser.add_argument("--fov-x", type=float)
    parser.add_argument("--camera-height",type=float,help="Optional measured camera height in metres")
    parser.add_argument("--level", type=int, default=5)
    parser.add_argument("--step", type=int, default=2)
    args = parser.parse_args()
    import torch
    from PIL import Image, ImageOps
    from autoscene.reconstruction import GeometryBackend, export_scene, calibrate_camera_height, reviewed_observations, MOGE_COMMIT, UTILS_COMMIT
    torch.set_num_threads(4)
    if args.device=="cpu":
        device="cpu"
    else:
        indices=[i for i in range(torch.cuda.device_count()) if args.gpu_name.lower() in torch.cuda.get_device_name(i).lower()]
        if len(indices)!=1: parser.error("--gpu-name must select exactly one available GPU")
        device=f"cuda:{indices[0]}"
        torch.cuda.set_device(device)
    device_name=torch.cuda.get_device_name(device) if device!="cpu" else "CPU / 4 PyTorch threads"
    args.out.mkdir(parents=True,exist_ok=False)
    image=ImageOps.exif_transpose(Image.open(args.image)).convert("RGB")
    print("Loading geometry backend on " + device_name,flush=True)
    start=time.perf_counter()
    backend=GeometryBackend(args.model,device,args.revision)
    load_ms=(time.perf_counter()-start)*1000
    maps,inference_ms=backend.predict(image,args.fov_x,args.level)
    height_calibration = {"applied":False}
    if args.camera_height is not None:
        if args.camera_height <= 0: parser.error("Camera height must be positive")
        maps,height_calibration=calibrate_camera_height(maps,args.camera_height)
    if args.review:
        review=json.loads(args.review.read_text(encoding="utf-8"))
        observations=reviewed_observations(image,review)
        mode="reviewed 2D observations; automatic geometry"
    else:
        # Run detector after releasing geometry weights to avoid doubling GPU residency.
        del backend.model
        torch.cuda.empty_cache()
        from autoscene.perception import Baseline
        from autoscene.scene import clean_detections
        cfg=json.loads((ROOT/"configs/room.json").read_text())
        cfg["prompts"]=["television","cabinet","chair","table","window","lamp","vase","sofa","bed"]
        detector=Baseline(cfg,device)
        detections,_,_=detector.predict(image)
        observations=[{"label":d["label"],"bbox_xyxy_px":d["bbox"],"score":d["score"]}
                      for d in clean_detections(detections,*image.size,.6)]
        detector.close()
        mode="automatic detector + GrabCut; automatic geometry"
    provenance={"mode":mode,"model":args.model,"model_revision":backend.revision,
                "moge_code_revision":MOGE_COMMIT,"utils_revision":UTILS_COMMIT,
                "resolution_level":args.level,"known_camera_fov":args.fov_x,
                "height_calibration":height_calibration,
                "geometry_inference_ms":inference_ms,"cold_model_load_and_download_ms":load_ms,
                "device":device_name,"ground_truth_scene_used":False}
    scene=export_scene(image,maps,observations,args.out,provenance,args.step)
    print(json.dumps({"scene":str((args.out/"scene.json").resolve()),"objects":len(scene["objects"])-1,
                      "geometry_ms":inference_ms,"mode":mode},indent=2),flush=True)


if __name__=="__main__": main()
