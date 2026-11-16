# Image-grounded reconstruction workflow

Read [the reconstruction briefing](../briefings/07-reconstructing-the-room-faithfully.md) first for the geometric intuition. This workflow reconstructs visible surfaces and separate object candidates. It does not complete unseen sides or infer cabinet mechanisms.

## Setup

Use this repository's existing Python 3.11 environment. The setup preserves its NumPy and CUDA PyTorch versions and imports only MoGe-2 inference from pinned upstream source trees.

```powershell
.\scripts\setup_reconstruction.ps1
.\.venv\Scripts\python.exe scripts\fetch_geometry_model.py
```

The checkpoint downloader reads the official model record, checks byte ranges, and verifies the full published SHA256 before making the checkpoint available. Upstream source trees, weights, images, and generated results stay in ignored local folders. No other project's environment is used.

## Reconstruct a photograph

```powershell
.\.venv\Scripts\python.exe scripts\reconstruct_room.py --image "data\private\room.jpg" --out outputs\room-reconstruction
```

This mode uses automatic detection and GrabCut for object candidates. Supply `--fov-x 70` if horizontal camera field of view is known. Supply `--camera-height 1.5` only when that height was actually measured; it calibrates global scale through the fitted floor. `--gpu-name` selects a unique GPU name rather than assuming a device index.

Use `--device cpu` for the tested CPU path. The local GPU run encountered slow tensor transfers, so this experiment's repeatable benchmark uses four CPU threads. Height calibration is optional and is off by default: it did not consistently improve the development cases.

For the public reference image:

```powershell
.\.venv\Scripts\python.exe scripts\reconstruct_room.py --image data\samples\coco_000000000139.jpg --review resources\coco139-review.json --out outputs\reference-reconstruction
```

The review file contains assistant-reviewed image outlines. It contains no measured 3D furniture positions. This is the guided path for faithful target identity; it must not be reported as automatic segmentation accuracy. Remove `--review` to test the automatic path.

Each result contains `scene.json`, `geometry.npz`, `surfaces.bin`, and `texture.png`. The JSON preserves provenance and separate identity/geometry/action/intention fields. The binary mesh packet is a compact Unity input, with positions already converted to Unity's axes. Do not apply a second Y flip in the importer.

## Inspect and build in Unity

Keep the existing editor minimized and target this project explicitly:

```powershell
unity command script eval --project-path G:\AI\AutoSceneAnalysis\unity\AutoSceneVR --format json -- --code 'return AutoSceneVR.Editor.ReconstructionWorkbench.Import("G:/AI/AutoSceneAnalysis/outputs/reference-reconstruction/scene.json");'
unity command script eval --project-path G:\AI\AutoSceneAnalysis\unity\AutoSceneVR --timeout 600 --format json -- --code 'return AutoSceneVR.Editor.ReconstructionWorkbench.BuildWindows();' --timeout 600
```

The importer creates real mesh assets and a saved scene through Unity APIs under ignored `Assets/ReconstructionGenerated/`. It writes reference, offset, and geometry-overview renders alongside the reconstruction. Reimporting replaces those generated assets; put independent manual work in another folder.

The standalone build is `unity/AutoSceneVR/Builds/Reconstruction/RoomReconstruction.exe`. Keep the complete build folder together. Right mouse plus WASD explores the model; Q/E changes height, R restores the exact reference camera, and Tab shows the geometry overview. The old interaction demo remains under `Builds/Windows/`.

Use `scripts/launch_reconstruction.ps1` to launch it, `-Background` to keep it hidden, or `-Verify` to check the compiled scene and quit automatically. `scripts/refine_reconstruction.py --input "<result-folder>" --out "<new-result-folder>" --review resources/coco139-review.json` reuses saved geometry while refining image regions. Omit `--review` on that script to test automatic object regions without rerunning geometry.

## Run the round-trip benchmark

```powershell
unity command script eval --project-path G:\AI\AutoSceneAnalysis\unity\AutoSceneVR --timeout 300 --format json -- --code 'return AutoSceneVR.Editor.ReconstructionWorkbench.GenerateBenchmark("G:/AI/AutoSceneAnalysis/outputs/reconstruction-benchmark",8);' --timeout 300
.\.venv\Scripts\python.exe scripts\benchmark_reconstruction.py --dataset outputs\reconstruction-benchmark --device cpu
```

Use a fresh dataset directory for a new experiment. The generator's seeds 100–103 are development cases and 200–203 are held-out cases. Inference reads only `rgb.png` and `camera.json`. Evaluation reads `truth.json` and `truth-depth.bin` after predictions have been exported. The height-calibrated variant uses the explicitly supplied camera-height field; raw predictions retain model scale.

`benchmark-results.json` records per-scene errors, target recall, ordering and runtime. Raw metric error, measured-height error, and evaluation-only scale-aligned error have different meanings; preserve their labels when comparing methods. Mean absolute relative error of 0.10 means an average distance error of roughly ten percent on the evaluated visible pixels, not ten-percent error in every object's dimensions.

To complete the visual round trip, call `ReconstructionWorkbench.Import` on a case's `prediction/scene.json`. The importer writes the reconstructed source/offset views there. After rendering the cases, `python scripts/report_reconstruction.py --dataset outputs/reconstruction-benchmark` builds a local side-by-side report and comparison grid.

## Next interchangeable stages

Replace GrabCut with a tested instance-mask backend, compare a larger geometry model, then add asset retrieval and image/depth-constrained pose fitting. Keep unseen completion in a separate geometry layer. Before using reconstructed targets for motor tasks, add interaction proxies, explicit action definitions and trial scoring; scene appearance alone does not establish physical feasibility.
