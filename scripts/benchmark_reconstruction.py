"""Reconstruct RGB renders first; open simulator truth only inside the evaluator."""
import argparse
import json
import os
import sys
import time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
os.environ.setdefault("HF_HOME",str(ROOT/".cache/huggingface"))
sys.path.insert(0,str(ROOT/"src"))


def predict_inputs(folder, geometry, detector):
    """Input boundary deliberately receives only RGB and camera calibration."""
    import torch
    from PIL import Image
    from autoscene.scene import clean_detections
    from autoscene.reconstruction import export_scene,calibrate_camera_height
    image=Image.open(folder/"rgb.png").convert("RGB")
    calibration=json.loads((folder/"camera.json").read_text())
    maps,elapsed=geometry.predict(image,calibration["fov_x_deg"],level=5)
    detections,_,times=detector.predict(image)
    objects=[{"label":d["label"],"bbox_xyxy_px":d["bbox"],"score":d["score"]}
             for d in clean_detections(detections,*image.size,.6)]
    export_scene(image,maps,objects,folder/"prediction",{
        "mode":"automatic RGB + known FOV", "ground_truth_scene_used":False,
        "model":geometry.model_id,"model_revision":geometry.revision,"geometry_ms":elapsed,
        "detector_and_relative_depth_ms":times["perception_ms"]},step=2)
    calibrated,calibration_record=calibrate_camera_height(maps,calibration["camera_height_m"])
    export_scene(image,calibrated,objects,folder/"prediction-height-calibrated",{
        "mode":"automatic RGB + known FOV + measured camera height", "ground_truth_scene_used":False,
        "height_calibration":calibration_record,"model":geometry.model_id,"model_revision":geometry.revision},step=2)
    return maps,calibrated,objects,elapsed,times["perception_ms"]


def evaluate(folder,maps,objects):
    """Truth is used for scores only, never for correction, scaling, or mesh export."""
    import cv2
    import numpy as np
    from autoscene.scene import box_iou
    truth=json.loads((folder/"truth.json").read_text())
    with (folder/truth["depth_file"]).open("rb") as f:
        w,h=np.fromfile(f,dtype="<i4",count=2)
        depth=np.fromfile(f,dtype="<f4").reshape(h,w)
    ih,iw=maps["depth"].shape
    xx,yy=np.meshgrid((np.arange(w)+.5)*iw/w-.5,(np.arange(h)+.5)*ih/h-.5)
    pred=cv2.remap(maps["depth"],xx.astype("float32"),yy.astype("float32"),cv2.INTER_NEAREST)
    valid=cv2.remap(maps["mask"].astype("uint8"),xx.astype("float32"),yy.astype("float32"),cv2.INTER_NEAREST)>0
    keep=(depth>0)&valid&np.isfinite(pred)&(pred>0)
    gt,p=depth[keep],pred[keep]
    ratio=np.maximum(gt/p,p/gt)
    scale=float(np.median(gt/p))
    def label(s):
        return next((c for c in ["television","cabinet","chair","table","lamp"] if c in s.lower()),s.lower())
    matched=[];used=set()
    for actual in truth["objects"]:
        candidates=[(box_iou(actual["bbox_xyxy_px"],o["bbox_xyxy_px"]),i,o) for i,o in enumerate(objects)
                    if label(o["label"])==actual["label"] and i not in used]
        best=max(candidates,key=lambda x:x[0]) if candidates else None
        if best and best[0]>=.25: used.add(best[1]);matched.append(actual["id"])
    tv=next((o for o in objects if label(o["label"])=="television"),None)
    cabinet=next((o for o in objects if label(o["label"])=="cabinet"),None)
    actual_tv=next(o for o in truth["objects"] if o["label"]=="television")
    actual_cab=next(o for o in truth["objects"] if o["label"]=="cabinet")
    center=lambda o:(o["bbox_xyxy_px"][0]+o["bbox_xyxy_px"][2])/2
    ordering=None if tv is None or cabinet is None else bool((center(tv)<center(cabinet))==(center(actual_tv)<center(actual_cab)))
    return {"seed":truth["seed"],"split":truth["split"],"valid_depth_samples":int(keep.sum()),
        "metric_abs_rel":float(np.mean(np.abs(p-gt)/gt)),"metric_rmse_m":float(np.sqrt(np.mean((p-gt)**2))),
        "delta_1_25":float(np.mean(ratio<1.25)),
        "scale_aligned_abs_rel_diagnostic":float(np.mean(np.abs(p*scale-gt)/gt)),
        "evaluation_only_scale_factor":scale,
        "constant_5m_abs_rel_baseline":float(np.mean(np.abs(5-gt)/gt)),
        "target_recall_iou_025":len(matched)/len(truth["objects"]),
        "matched_target_ids":matched,"ground_truth_target_count":len(truth["objects"]),
        "automatic_candidate_count":len(objects),"tv_cabinet_ordering_correct_if_both_detected":ordering}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset",type=Path,required=True)
    parser.add_argument("--gpu-name",default="RTX 2060")
    parser.add_argument("--device",choices=["cpu"],help="CPU fallback for independent verification")
    args=parser.parse_args()
    import torch,numpy as np
    from autoscene.reconstruction import GeometryBackend
    from autoscene.perception import Baseline
    torch.set_num_threads(4)
    if args.device=="cpu":
        device="cpu"
    else:
        devices=[i for i in range(torch.cuda.device_count()) if args.gpu_name.lower() in torch.cuda.get_device_name(i).lower()]
        if len(devices)!=1: parser.error("Select exactly one GPU by name")
        device=f"cuda:{devices[0]}";torch.cuda.set_device(device)
    print("Loading benchmark models",flush=True)
    geometry=GeometryBackend(device=device)
    cfg=json.loads((ROOT/"configs/room.json").read_text())
    cfg["prompts"]=["television","cabinet","chair","table","lamp"]
    cfg["local_files_only"]=True
    print("Loading cached detector",flush=True)
    detector=Baseline(cfg,device)
    results=[]
    for folder in sorted(args.dataset.glob("scene-*")):
        print("Reconstructing " + folder.name,flush=True)
        maps,calibrated,objects,geometry_ms,detector_ms=predict_inputs(folder,geometry,detector)
        score=evaluate(folder,maps,objects)
        corrected=evaluate(folder,calibrated,objects)
        score["height_calibrated_abs_rel"]=corrected["metric_abs_rel"]
        score["height_calibrated_rmse_m"]=corrected["metric_rmse_m"]
        score.update(geometry_ms=geometry_ms,detector_and_relative_depth_ms=detector_ms)
        results.append(score)
        (folder/"score.json").write_text(json.dumps(score,indent=2)+"\n")
        print(json.dumps(score),flush=True)
    summaries={}
    for split in ["development","held_out"]:
        rows=[r for r in results if r["split"]==split]
        summaries[split]={key:float(np.mean([r[key] for r in rows])) for key in [
            "metric_abs_rel","metric_rmse_m","delta_1_25","scale_aligned_abs_rel_diagnostic",
            "constant_5m_abs_rel_baseline","height_calibrated_abs_rel","height_calibrated_rmse_m","target_recall_iou_025","geometry_ms"]}
        observed=[r["tv_cabinet_ordering_correct_if_both_detected"] for r in rows if r["tv_cabinet_ordering_correct_if_both_detected"] is not None]
        summaries[split]["tv_cabinet_ordering"]={"correct":sum(observed),"eligible":len(observed),"total_scenes":len(rows)}
    result={"model":geometry.model_id,"model_revision":geometry.revision,"device":torch.cuda.get_device_name(device) if device!="cpu" else "CPU / 4 PyTorch threads",
            "input":"RGB + public camera FOV; separate optional measured-height variant; no reviewed outlines","truth_boundary":"evaluator only",
            "metric_scope":"Visible depth at ray-sampled pixels. No complete-object 3D IoU or hidden-geometry claim.",
            "summaries":summaries,"scenes":results}
    (args.dataset/"benchmark-results.json").write_text(json.dumps(result,indent=2)+"\n")
    detector.close()
    print(json.dumps(summaries,indent=2),flush=True)


if __name__=="__main__":main()
