# Running the local demo and growing it into a room scanner

Start with the existing photo demo. It turns one image into object candidates, a relative-depth preview, and a two-axis selection interface. The report opens from disk, so it needs no web service, tunnel, or port assignment.

The implementation uses Grounding DINO Tiny and Depth Anything V2 Small. It is a reference for evaluating later models. The [first experiment record](../experiments/2026-11-16-baseline.md) records the measured result separately from the research recommendations.

## The workstation

The initial hardware check found an RTX 2060 with 6 GB VRAM and an RTX A2000 with 12 GB. About 5.8 GB and 4.2 GB respectively were free at that check. Free memory changes as other work runs. The two cards' memories do not form one 18 GB pool.

Inspect free memory before running:

```powershell
nvidia-smi --query-gpu=index,name,memory.free --format=csv
```

Select by GPU name with `--gpu-name "RTX 2060"`. PyTorch and `nvidia-smi` enumerate these two GPUs in different orders on this machine, so their numeric indices are not interchangeable. The demo prints and records the actual selected name. Model files and packages are kept under this project's `.cache` and `.venv` directories.

## Reproducing the environment

From `G:\AI\AutoSceneAnalysis`, create a fresh environment if one does not already exist. Python 3.11 is the tested interpreter series.

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install --cache-dir .cache\pip torch==2.7.1 torchvision==0.22.1 --index-url https://download.pytorch.org/whl/cu126
.\.venv\Scripts\python.exe -m pip install --cache-dir .cache\pip -e .
```

For a machine without an NVIDIA GPU, use PyTorch's `https://download.pytorch.org/whl/cpu` index in the second command and run with `--device cpu`. The first CUDA install downloads about 2.7 GB for the Windows PyTorch wheel, plus dependencies. The two model weight files add roughly 0.8 GB. Later runs reuse cached files.

The package and model revisions actually used are recorded in each run. The tested dependency list is also saved in [the environment record](../experiments/environment-win-py311.txt).

## Seeing the two-axis idea without downloading models

```powershell
.\.venv\Scripts\python.exe scripts\run_demo.py --fixture --out outputs\interface-example
Start-Process .\outputs\interface-example\report.html
```

This generates a deliberately simple drawing with known objects. It tests the interface only. The page labels it as synthetic and does not report an inference speed.

## Running a real photograph

Use your own room photograph, or download the small public indoor COCO example:

```powershell
.\.venv\Scripts\python.exe scripts\fetch_sample.py
.\.venv\Scripts\python.exe scripts\run_demo.py --image data\samples\coco_000000000139.jpg --gpu-name "RTX 2060" --warmups 1 --repeats 10 --out outputs\coco-rtx2060
Start-Process .\outputs\coco-rtx2060\report.html
```

Choose a new output directory for each run. For your own image, replace `--image` with its path. The first model run needs an internet connection to download public weights. Add `--offline` on subsequent runs to use only the local cache.

The report shows annotated boxes and a relative-depth map. Select an object and then a suggested action. The JSON preview contains a one-hot object vector and a separate one-hot action vector: a `1` marks the chosen entry. This is a convenient visible stand-in for a future decoder output.

The output directory also contains `scene.json`, `metrics.json`, the raw inverse-depth array, and the images. The scene file deliberately leaves metre coordinates empty. Actions are category rules, and IDs only refer to the current snapshot. No tracking, learned affordances, camera streaming, or neural decoding is implemented in this baseline.

## Adjusting the experiment

Edit a copy of `configs/room.json` to change the target words and thresholds, then pass it with `--config`. Edit a copy of `configs/actions.json` to change the demonstration menu, then pass it with `--actions`. Keep the vocabulary focused; the detector has a finite text-token budget. Long lists should later be processed in explicit prompt groups and merged.

A low detection threshold may recover a small handle and also add wrong labels. Pick thresholds on development photographs and then freeze them for a held-out test. An attractive single output should not decide the configuration.

## Three deployment stages after the photo baseline

**Stage 1 adds masks and a ruler.** Benchmark YOLOE-26 Small against the baseline for whole objects and handles. Separately test SAM 3 on a few frames for mask quality. SAM's upstream environment requires Python 3.12 or newer, PyTorch 2.7 or newer, CUDA 12.6 or newer, and access to its checkpoint. Keep that environment separate from this demo. Compare MoGe-2 or DA3 Metric with measured visible surfaces. [SAM installation](https://github.com/facebookresearch/sam3), [YOLOE installation](https://docs.ultralytics.com/models/yoloe), [DA3 variants](https://github.com/ByteDance-Seed/Depth-Anything-3)

**Stage 2 adds a moving camera and memory.** Record a slow 10–20 second walkthrough with sideways motion and overlapping views. Initially extract 8–16 useful keyframes. Compare a DA3 configuration with VGGT-Omega, fuse observations into object instances, and measure whether IDs survive revisiting the same target. Save camera calibration, frame timestamps, and the scale reference with the capture. A video file alone does not establish accurate geometry.

VGGT-Omega's official A100 profile lists about 6.67 GB peak GPU memory for 10 frames at its stated preprocessing settings. That suggests trying a freed 12 GB A2000, with geometry and segmentation run separately. It leaves too little margin for the 6 GB RTX 2060 at those settings. The profile is a memory reference, not a latency measurement on either local card. [VGGT-Omega runtime table](https://github.com/facebookresearch/vggt-omega#runtime-and-gpu-memory)

**Stage 3 adds functional parts and fast updates.** Introduce parent/part IDs, object state, and action-specific contact regions. Use SceneFun3D-style labels to evaluate the parts. A later RGB-D interaction recording can test a FunRec-style articulated reconstruction. Separate slow discovery from fast tracking: refresh the inventory occasionally, while updating already-known targets more frequently. [SceneFun3D](https://scenefun3d.github.io/), [FunRec](https://arxiv.org/abs/2604.05621)

MoGe-3's current stack introduces sparse-computation and Triton dependencies. A separate Linux or WSL environment is a sensible place to test it before native Windows integration. Record a working upstream commit after installation; keep any WSL setup independent of existing services. [MoGe installation](https://github.com/microsoft/MoGe)

## A first month of work

These are suggested work packages, not deadlines:

| Package | Deliverable | What decides the next step |
| --- | --- | --- |
| Photo prototype | A small set of room images, labels, timing, and mouse selections | Are task targets reliably in the menu? |
| Parts and metric geometry | Mask comparison and measured desk targets | Do masks and calibrated depth improve useful position estimates? |
| Short scan | A persistent map from a moving camera | Does the map reduce misses and ID changes? |
| Decoder integration prototype | Timestamped scene snapshots and simulated decoder messages | Can the two streams stay aligned during a trial? |

If a hosted or live web interface becomes useful, first follow this machine's shared Cloudflare index at `E:\MMD\cloudflare-config\AGENT_INDEX.md` and its registry. The current demo has no service launcher and makes no changes to that configuration.
