"""Re-segment a cached geometry estimate without re-running the neural model."""
import argparse
import json
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"src"))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input",type=Path,required=True)
    parser.add_argument("--out",type=Path,required=True)
    parser.add_argument("--review",type=Path,help="Omit to use automatic detector and GrabCut instead")
    parser.add_argument("--step",type=int,default=1)
    args=parser.parse_args()
    import numpy as np
    from PIL import Image
    from autoscene.reconstruction import export_scene,reviewed_observations
    image=Image.open(args.input/"texture.png").convert("RGB")
    original=json.loads((args.input/"scene.json").read_text())
    if args.review:
        review=json.loads(args.review.read_text())
        observations=reviewed_observations(image,review)
    else:
        import os
        os.environ.setdefault("HF_HOME",str(ROOT/".cache/huggingface"))
        import torch
        from autoscene.perception import Baseline
        from autoscene.scene import clean_detections
        torch.set_num_threads(4)
        cfg=json.loads((ROOT/"configs/room.json").read_text())
        cfg["prompts"]=["television","cabinet","chair","table","window","lamp","vase","sofa","bed"]
        cfg["local_files_only"]=True
        detector=Baseline(cfg,"cpu")
        detections,_,_=detector.predict(image)
        observations=[{"label":d["label"],"bbox_xyxy_px":d["bbox"],"score":d["score"]}
                      for d in clean_detections(detections,*image.size,.6)]
        detector.close()
    args.out.mkdir(parents=True,exist_ok=False)
    with np.load(args.input/"geometry.npz") as data:maps={k:data[k] for k in data.files}
    provenance=dict(original["provenance"])
    provenance["refinement"]="Reused the same predicted geometry; refined image regions and mesh sampling only"
    provenance["mode"]="reviewed 2D observations; automatic geometry" if args.review else "automatic detector + GrabCut; reused automatic geometry"
    scene=export_scene(image,maps,observations,args.out,provenance,args.step)
    print(json.dumps({"scene":str((args.out/"scene.json").resolve()),"triangles":sum(o["triangles"] for o in scene["objects"])},indent=2))


if __name__=="__main__":main()
