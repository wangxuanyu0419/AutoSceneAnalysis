import json
import sys
import unittest
from pathlib import Path

import numpy as np
from jsonschema import Draft202012Validator, ValidationError

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))
from autoscene.scene import build_scene, clean_detections, encode_selection


class SceneTests(unittest.TestCase):
    def setUp(self):
        self.config = json.loads((ROOT / "configs/room.json").read_text())
        self.actions = json.loads((ROOT / "configs/actions.json").read_text())
        self.schema = Draft202012Validator(json.loads((ROOT / "schemas/scene.schema.json").read_text()))

    def scene(self, detections, depth=None):
        return build_scene(detections, np.ones((100, 200)) if depth is None else depth,
                           200, 100, "a" * 64, {}, self.config, self.actions)

    def test_duplicate_suppression_keeps_distinct_objects_and_parts(self):
        detections = [
            {"label": "cup", "score": .9, "bbox": [-5, 10, 40, 60]},
            {"label": "cup", "score": .5, "bbox": [0, 11, 41, 61]},
            {"label": "cup", "score": .8, "bbox": [100, 10, 140, 60]},
            {"label": "handle", "score": .7, "bbox": [0, 10, 40, 60]},
            {"label": "bad", "score": .8, "bbox": [0, 0, 0, 10]},
            {"label": "bad", "score": float("nan"), "bbox": [0, 0, 10, 10]}
        ]
        clean = clean_detections(detections, 200, 100, .6)
        self.assertEqual(len(clean), 3)
        self.assertEqual(clean[0]["bbox"][0], 0)

    def test_scene_cannot_claim_metric_coordinates(self):
        scene = self.scene([{"label": "cup", "score": .8, "bbox": [20, 20, 60, 60]}])
        self.schema.validate(scene)
        self.assertEqual(scene["objects"][0]["center_xy_normalized"], [.2, .4])
        self.assertIsNone(scene["objects"][0]["position_m"])
        scene["objects"][0]["position_m"] = [1, 2, 3]
        with self.assertRaises(ValidationError):
            self.schema.validate(scene)

    def test_actions_form_two_vectors_with_the_correct_object_condition(self):
        scene = self.scene([{"label": "cup", "score": .8, "bbox": [20, 20, 60, 60]},
                            {"label": "table", "score": .9, "bbox": [10, 60, 180, 90]}])
        result = encode_selection(scene, "obj_0001", "grasp")
        self.assertEqual(result["object_one_hot"], [1, 0])
        self.assertEqual(sum(result["action_one_hot"]), 1)
        self.assertEqual(result["action_one_hot"][scene["decoder_interface"]["action_vocabulary"].index("grasp")], 1)
        with self.assertRaises(ValueError):
            encode_selection(scene, "obj_0002", "grasp")

    def test_empty_and_unrecognised_categories_are_representable(self):
        empty = self.scene([])
        self.schema.validate(empty)
        self.assertEqual(empty["decoder_interface"]["suggestion_mask"], [])
        unknown = self.scene([{"label": "unfamiliar object", "score": .5, "bbox": [1, 1, 20, 20]}])
        self.assertEqual([a["action"] for a in unknown["objects"][0]["action_candidates"]], ["inspect"])

    def test_nonfinite_depth_does_not_leak_nan_into_json(self):
        scene = self.scene([{"label": "cup", "score": .8, "bbox": [20, 20, 60, 60]}], np.full((100, 200), np.nan))
        self.assertIsNone(scene["objects"][0]["relative_inverse_depth"])
        json.dumps(scene, allow_nan=False)

    def test_depth_alignment_is_required(self):
        with self.assertRaises(ValueError):
            self.scene([], np.ones((20, 20)))


if __name__ == "__main__":
    unittest.main()
