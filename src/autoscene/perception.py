"""Small baseline models; heavier research backends can implement the same output contract."""
import gc
import time

import numpy as np
import torch
from transformers import AutoImageProcessor, AutoModelForDepthEstimation, AutoModelForZeroShotObjectDetection, AutoProcessor


def sync(device):
    if torch.device(device).type == "cuda":
        torch.cuda.synchronize(device)


class Baseline:
    def __init__(self, config, device):
        self.config, self.device = config, device
        local = bool(config.get("local_files_only", False))
        self.detector_processor = AutoProcessor.from_pretrained(config["detector"], revision=config["detector_revision"], local_files_only=local)
        self.detector = AutoModelForZeroShotObjectDetection.from_pretrained(
            config["detector"], revision=config["detector_revision"], use_safetensors=True, local_files_only=local
        ).eval().to(device)
        self.depth_processor = AutoImageProcessor.from_pretrained(config["depth_model"], revision=config["depth_revision"], use_fast=False, local_files_only=local)
        self.depth = AutoModelForDepthEstimation.from_pretrained(
            config["depth_model"], revision=config["depth_revision"], use_safetensors=True, local_files_only=local
        ).eval().to(device)
        self.model_info = {
            "detector": {"id": config["detector"], "revision": self.detector.config._commit_hash},
            "depth": {"id": config["depth_model"], "revision": self.depth.config._commit_hash},
            "precision": "float32", "segmentation": "not_implemented"
        }

    @torch.inference_mode()
    def predict(self, image):
        cfg = self.config
        sync(self.device)
        start = time.perf_counter()
        text = ". ".join(cfg["prompts"]) + "."
        inputs = self.detector_processor(
            images=image, text=text, return_tensors="pt",
            size={"shortest_edge": cfg["shortest_edge"], "longest_edge": cfg["longest_edge"]}
        ).to(self.device)
        raw = self.detector(**inputs)
        result = self.detector_processor.post_process_grounded_object_detection(
            raw, inputs.input_ids, threshold=cfg["box_threshold"], text_threshold=cfg["text_threshold"],
            target_sizes=[(image.height, image.width)]
        )[0]
        labels = result["text_labels"] if "text_labels" in result else result["labels"]
        detections = [{"label": label, "score": score.item(), "bbox": box.tolist()}
                      for label, score, box in zip(labels, result["scores"], result["boxes"])]
        del raw, result, inputs
        sync(self.device)
        detection_end = time.perf_counter()
        inputs = self.depth_processor(images=image, return_tensors="pt",
                                      size={"height": cfg["depth_size"], "width": cfg["depth_size"]}).to(self.device)
        raw = self.depth(**inputs)
        depth = torch.nn.functional.interpolate(raw.predicted_depth.unsqueeze(1),
                    size=(image.height, image.width), mode="bicubic", align_corners=False)[0, 0].cpu().numpy()
        del raw, inputs
        sync(self.device)
        end = time.perf_counter()
        return detections, depth.astype(np.float32), {
            "detection_ms": (detection_end - start) * 1000,
            "depth_ms": (end - detection_end) * 1000,
            "perception_ms": (end - start) * 1000
        }

    def close(self):
        del self.detector, self.depth
        gc.collect()
        if torch.device(self.device).type == "cuda":
            torch.cuda.empty_cache()
