"""Image-grounded visible-surface reconstruction; no invented complete room layout."""
import hashlib
import json
import os
import struct
import sys
import time
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageOps
from scipy.spatial.transform import Rotation

ROOT = Path(__file__).resolve().parents[2]
MOGE_COMMIT = "74fbce054ebed49800de42d0ad0e83495065719a"
UTILS_COMMIT = "62f09d58509485564e24d5d9f6aac9ee9ebc0c37"
DEFAULT_MODEL = "Ruicheng/moge-2-vitb-normal"
DEFAULT_MODEL_REVISION = "ca5f0e07ff01d3e5a364c1d954ed12ee1814b368"


class GeometryBackend:
    def __init__(self, model="Ruicheng/moge-2-vitb-normal", device="cpu", revision=None):
        os.environ.setdefault("HF_HOME", str(ROOT / ".cache/huggingface"))
        for path in (ROOT / "external/moge", ROOT / "external/utils3d-moge"):
            if not path.exists():
                raise RuntimeError("Run scripts/setup_reconstruction.ps1 first")
            sys.path.insert(0, str(path))
        import torch
        from huggingface_hub import HfApi
        from moge.model.v2 import MoGeModel
        local = ROOT / "models" / (model.split("/")[-1] + ".pt")
        metadata = json.loads(local.with_suffix(".json").read_text()) if local.with_suffix(".json").exists() else {}
        self.revision = revision or metadata.get("revision") or (DEFAULT_MODEL_REVISION if model==DEFAULT_MODEL else HfApi().model_info(model).sha)
        self.model_id, self.device = model, device
        checkpoint = str(local) if local.exists() and metadata.get("revision") == self.revision else model
        print("Loading MoGe-2 checkpoint",flush=True)
        self.model = MoGeModel.from_pretrained(checkpoint, revision=self.revision).eval()
        print("Moving geometry model to " + device,flush=True)
        self.model = self.model.to(device)
        print("Geometry model ready",flush=True)
        self.torch = torch

    def predict(self, image, fov_x=None, level=5):
        torch = self.torch
        tensor = torch.from_numpy(np.asarray(image).copy()).permute(2, 0, 1).to(self.device).float() / 255
        if "cuda" in self.device:
            torch.cuda.synchronize(self.device)
        start = time.perf_counter()
        with torch.inference_mode():
            out = self.model.infer(tensor, fov_x=fov_x, resolution_level=level, use_fp16="cuda" in self.device)
        if "cuda" in self.device:
            torch.cuda.synchronize(self.device)
        elapsed = (time.perf_counter() - start) * 1000
        maps = {k: v.detach().float().cpu().numpy() for k, v in out.items() if torch.is_tensor(v)}
        maps["mask"] = maps["mask"] > .5
        return maps, elapsed


def floor_frame(points, valid, seed=0):
    """Robust floor candidate from a broad lower-image region, with an explicit fit score."""
    h, w = valid.shape
    yy, xx = np.indices((h, w))
    candidate = valid & (yy > h * .68) & (xx < w * .72) & (xx > w * .04)
    sample = points[candidate]
    sample = sample[np.isfinite(sample).all(1)]
    if len(sample) < 30:
        return np.eye(3), np.array([0., 0., 0.]), {"accepted": False, "reason": "insufficient floor evidence"}
    rng = np.random.default_rng(seed)
    sample = sample[rng.choice(len(sample), min(4000, len(sample)), replace=False)]
    best, plane = None, None
    threshold = max(.015, np.median(sample[:, 2]) * .008)
    for _ in range(180):
        p = sample[rng.choice(len(sample), 3, replace=False)]
        n = np.cross(p[1] - p[0], p[2] - p[0])
        n /= max(np.linalg.norm(n), 1e-9)
        if n[1] > 0: n = -n
        if n[1] > -.65: continue
        d = -p[0] @ n
        support = np.abs(sample @ n + d) < threshold
        if best is None or support.sum() > best.sum(): best, plane = support, (n, d)
    if plane is None or best.mean() < .25:
        return np.eye(3), np.zeros(3), {"accepted": False, "reason": "no dominant upward floor plane"}
    centered = sample[best] - sample[best].mean(0)
    _, _, vt = np.linalg.svd(centered, full_matrices=False)
    n = vt[-1]
    if n[1] > 0: n = -n
    d = -sample[best].mean(0) @ n
    # OpenCV camera coordinates -> Unity camera coordinates, then align floor up.
    n_unity = n * [1, -1, 1]
    axis = np.cross(n_unity, [0, 1, 0])
    angle = np.arctan2(np.linalg.norm(axis), n_unity[1])
    rot = Rotation.from_rotvec(axis / max(np.linalg.norm(axis), 1e-9) * angle).as_matrix()
    offset = np.array([0., d, 0.])
    return rot, offset, {"accepted": True, "support_fraction": float(best.mean()),
                         "residual_median_m": float(np.median(np.abs(sample[best] @ n + d))),
                         "camera_height_estimate_m": float(d), "scale": "model estimate, uncalibrated"}


def instance_masks(image, observations):
    """Polygons are reviewed image evidence; automatic boxes use GrabCut, not oracle IDs."""
    rgb = np.asarray(image)
    h, w = rgb.shape[:2]
    masks = []
    cv2.setRNGSeed(0)
    for obs in observations:
        mask = np.zeros((h, w), np.uint8)
        if "polygon_px" in obs:
            cv2.fillPoly(mask, [np.round(obs["polygon_px"]).astype(np.int32)], 1)
        else:
            x0, y0, x1, y1 = obs["bbox_xyxy_px"]
            x0, y0 = max(1, int(x0)), max(1, int(y0))
            x1, y1 = min(w - 1, int(x1)), min(h - 1, int(y1))
            if x1 <= x0 or y1 <= y0: continue
            bg, fg = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
            cv2.grabCut(rgb, mask, (x0, y0, x1-x0, y1-y0), bg, fg, 3, cv2.GC_INIT_WITH_RECT)
            mask = ((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD)).astype(np.uint8)
        masks.append((obs, mask.astype(bool)))
    # Small parts override enclosing furniture; no simulator truth enters this step.
    masks.sort(key=lambda pair: -pair[1].sum())
    labels = np.zeros((h, w), np.int32)
    for index, (_, mask) in enumerate(masks, 1): labels[mask] = index
    return labels, [obs for obs, _ in masks]


def reviewed_observations(image, record):
    """A review belongs to exact image pixels, not any image of the same size."""
    digest=hashlib.sha256(np.asarray(image.convert("RGB")).tobytes()).hexdigest()
    if record.get("image_size_px")!=list(image.size) or record.get("image_rgb_sha256")!=digest:
        raise ValueError("Reviewed observations belong to a different image; check the RGB hash and size")
    return record["observations"]


def calibrate_camera_height(maps, camera_height_m):
    """Optional measured input fixes global scale; it is not inferred ground truth."""
    _, _, fit = floor_frame(maps["points"], maps["mask"])
    estimated = fit.get("camera_height_estimate_m", 0)
    if not fit["accepted"] or estimated < .2:
        return maps, {"applied": False, "reason": "floor or height estimate rejected"}
    scale = camera_height_m / estimated
    result = dict(maps)
    result["points"] = maps["points"] * scale
    result["depth"] = maps["depth"] * scale
    return result, {"applied": True, "measured_camera_height_m":camera_height_m,
                    "estimated_camera_height_before_m":estimated,"scale_factor":scale}


def triangulate(points, valid, labels, step=2, edge_ratio=.08, split_instances=True):
    """Keep triangles local to one instance and reject bridges across depth jumps."""
    h, w = valid.shape
    ys, xs = np.arange(0, h, step), np.arange(0, w, step)
    yy, xx = np.meshgrid(ys, xs, indexing="ij")
    p = points[yy, xx].reshape(-1, 3)
    v = valid[yy, xx].ravel() & np.isfinite(p).all(1)
    p = np.where(v[:,None],p,0)
    ids = labels[yy, xx].ravel()
    grid = np.arange(len(p)).reshape(len(ys), len(xs))
    a, b, c, d = grid[:-1,:-1].ravel(), grid[:-1,1:].ravel(), grid[1:,:-1].ravel(), grid[1:,1:].ravel()
    tri = np.concatenate([np.stack([a,c,b],1), np.stack([b,c,d],1)])
    good = v[tri].all(1)
    if split_instances:
        good &= (ids[tri] == ids[tri[:, :1]]).all(1)
    for i, j in ((0,1),(1,2),(2,0)):
        good &= np.linalg.norm(p[tri[:, i]] - p[tri[:, j]],axis=1) < np.minimum(p[tri[:, i],2],p[tri[:, j],2]) * edge_ratio
    uv = np.stack([(xx.ravel()+.5)/w, 1-(yy.ravel()+.5)/h],1)
    return p, uv, ids, tri[good]


def export_scene(image, maps, observations, out, provenance, step=2):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    image.save(out / "texture.png")
    points, valid = maps["points"], maps["mask"]
    labels, observations = instance_masks(image, observations)
    rot, offset, floor = floor_frame(points, valid)
    p, uv, ids, triangles = triangulate(points, valid, labels, step, split_instances=False)
    triangle_labels = ids[triangles]
    owners = np.where((triangle_labels[:,0]==triangle_labels[:,1]) | (triangle_labels[:,0]==triangle_labels[:,2]),
                      triangle_labels[:,0], np.where(triangle_labels[:,1]==triangle_labels[:,2], triangle_labels[:,1],triangle_labels[:,0]))
    world = (p * [1,-1,1]) @ rot.T + offset
    definitions = [{"id":"background", "label":"unassigned visible surfaces", "evidence":"unsegmented image pixels"}] + observations
    objects = []
    with (out / "surfaces.bin").open("wb") as f:
        f.write(b"ASR1")
        f.write(struct.pack("<i", len(definitions)))
        for i, obs in enumerate(definitions):
            faces = triangles[owners == i]
            used = np.unique(faces)
            local_faces = np.searchsorted(used, faces).astype("<i4")
            verts = world[used]
            center = np.median(verts,axis=0) if len(verts) else np.zeros(3)
            size = np.ptp(verts,axis=0) if len(verts) else np.zeros(3)
            name = obs.get("id", f"candidate_{i:03d}")
            encoded = name.encode("utf-8")
            f.write(struct.pack("<i", len(encoded))); f.write(encoded)
            f.write(struct.pack("<ii", len(verts), local_faces.size))
            np.column_stack([verts-center, uv[used]]).astype("<f4").tofile(f)
            local_faces.tofile(f)
            objects.append({"id":name, "label":obs["label"], "position":dict(zip("xyz",center.tolist())),
                "size":dict(zip("xyz",size.tolist())), "vertices":len(verts), "triangles":len(faces),
                "identity_evidence":obs.get("evidence", "automatic grounded detection + GrabCut"),
                "bbox_xyxy_px":obs.get("bbox_xyxy_px"),
                "geometry":{"method":"monocular visible surface", "hidden_surfaces":"not recovered", "metric_accuracy":"unvalidated model scale"},
                "action_feasibility":"unknown", "decoded_intention":None})
    k = maps["intrinsics"]
    scene = {"schema_version":1,"scene_version":"reconstruction_"+hashlib.sha256(np.asarray(image).tobytes()).hexdigest()[:12],
        "image_rgb_sha256":hashlib.sha256(np.asarray(image).tobytes()).hexdigest(),
        "image_size_px":list(image.size), "id_scope":"snapshot_only", "provenance":provenance,
        "coordinate_system":"Unity x right, y up, z forward; inferred floor alignment",
        "camera":{"position":dict(zip("xyz",offset.tolist())), "forward":dict(zip("xyz",rot[:,2].tolist())),
                  "up":dict(zip("xyz",rot[:,1].tolist())), "intrinsics_normalized":k.tolist(),
                  "fov_y_deg":float(np.degrees(2*np.arctan(.5/k[1,1])))},
        "floor_fit":floor,"texture":"texture.png","surfaces":"surfaces.bin","objects":objects,
        "limitations":["Visible-surface geometry only", "Photo texture contains the captured lighting", "Unseen sides are absent", "Image alignment does not prove metric depth accuracy"]}
    (out / "scene.json").write_text(json.dumps(scene,indent=2,allow_nan=False)+"\n",encoding="utf-8")
    np.savez_compressed(out / "geometry.npz", **maps)
    return scene
