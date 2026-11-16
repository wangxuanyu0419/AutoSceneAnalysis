"""Turn detections into an explicit object/action interface, without invented 3D positions."""
import math
from datetime import datetime, timezone

import numpy as np


def box_iou(a, b):
    x0, y0 = max(a[0], b[0]), max(a[1], b[1])
    x1, y1 = min(a[2], b[2]), min(a[3], b[3])
    intersection = max(0, x1 - x0) * max(0, y1 - y0)
    union = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - intersection
    return intersection / union if union > 0 else 0.0


def clean_detections(detections, width, height, nms_iou):
    """Clip boxes and suppress duplicates only within the same text label."""
    valid = []
    for det in detections:
        label = str(det["label"]).strip().lower().strip(". ")
        score = float(det["score"])
        box = list(map(float, det["bbox"]))
        if len(box) != 4 or not label or not all(math.isfinite(x) for x in box + [score]):
            continue
        box = [max(0, min(width, box[0])), max(0, min(height, box[1])),
               max(0, min(width, box[2])), max(0, min(height, box[3]))]
        if box[2] <= box[0] or box[3] <= box[1] or not 0 <= score <= 1:
            continue
        valid.append({"label": label, "score": score, "bbox": box})
    kept = []
    for det in sorted(valid, key=lambda x: -x["score"]):
        if not any(det["label"] == other["label"] and box_iou(det["bbox"], other["bbox"]) > nms_iou for other in kept):
            kept.append(det)
    return sorted(kept, key=lambda x: (x["label"], x["bbox"][0], x["bbox"][1]))


def build_scene(detections, inverse_depth, width, height, image_hash, model_info, config, action_config):
    if inverse_depth.shape != (height, width):
        raise ValueError("Depth must match the upright input image dimensions")
    objects = []
    vocabulary = action_config["actions"]
    matrix = []
    for index, det in enumerate(clean_detections(detections, width, height, config["nms_iou"]), 1):
        x0, y0, x1, y1 = det["bbox"]
        # The inner box reduces edge contamination but is not an instance mask.
        xa, xb = int(x0 + (x1 - x0) * .25), max(int(x0 + (x1 - x0) * .75), int(x0 + (x1 - x0) * .25) + 1)
        ya, yb = int(y0 + (y1 - y0) * .25), max(int(y0 + (y1 - y0) * .75), int(y0 + (y1 - y0) * .25) + 1)
        values = inverse_depth[ya:yb, xa:xb]
        values = values[np.isfinite(values)]
        depth_value = float(np.median(values)) if values.size else None
        suggestions = action_config["rules"].get(det["label"], ["inspect"])
        objects.append({
            "id": f"obj_{index:04d}", "label": det["label"],
            "detector_score": det["score"], "bbox_xyxy_px": det["bbox"],
            "center_xy_normalized": [(x0 + x1) / (2 * width), (y0 + y1) / (2 * height)],
            "bbox_area_fraction": (x1 - x0) * (y1 - y0) / (width * height),
            "relative_inverse_depth": depth_value,
            "depth_support": "inner_bbox_without_mask",
            "position_m": None, "parent_id": None, "state": "unknown",
            "action_candidates": [{"action": a, "evidence": "hand_written_category_rule", "feasibility": "unknown"} for a in suggestions]
        })
        matrix.append([int(a in suggestions) for a in vocabulary])
    return {
        "schema_version": "0.1.0", "created_at_utc": datetime.now(timezone.utc).isoformat(),
        "frame_id": "image_" + image_hash[:16], "image_sha256": image_hash,
        "image_size_px": [width, height], "id_scope": "snapshot_only",
        "coordinate_system": "image_x_right_y_down",
        "geometry": {"type": "relative_inverse_depth", "units": "arbitrary", "larger_means": "nearer",
                     "metric_scale_available": False, "coverage": "visible_surfaces_only"},
        "models": model_info, "config": config, "objects": objects, "relations": [],
        "decoder_interface": {
            "object_ids": [o["id"] for o in objects], "action_vocabulary": vocabulary,
            "suggestion_mask": matrix,
            "mask_semantics": "1 means suggested by a category rule; 0 means not suggested, not physically impossible",
            "controls": ["idle", "cancel", "rescan"], "neural_decoder_connected": False
        }
    }


def encode_selection(scene, object_id, action):
    """Encode a mouse/test selection; never an estimate of neural decoding accuracy."""
    interface = scene["decoder_interface"]
    i = interface["object_ids"].index(object_id)
    j = interface["action_vocabulary"].index(action)
    if interface["suggestion_mask"][i][j] != 1:
        raise ValueError("Action is not in this object's demonstration menu")
    return {
        "frame_id": scene["frame_id"], "object_id": object_id, "action": action,
        "object_one_hot": [int(k == i) for k in range(len(interface["object_ids"]))],
        "action_one_hot": [int(k == j) for k in range(len(interface["action_vocabulary"]))],
        "input_source": "simulated_selection", "executes_robot_motion": False
    }
