# AutoSceneAnalysis

A room can become a catalogue of targets and interactions: **which object does the user mean, and what do they want to do with it?** This project develops the vision and representation layer for that environment-aware neural interface, with implanted recordings as the intended later input.

The first prototype takes one photograph, finds named object candidates, estimates relative depth, and exports an object/action menu. A local report lets you try the two choices with a mouse. The research plan then adds masks, metric geometry, short camera scans, persistent identities, and functional parts.

## Start here

The briefings form a short course. Each can also be read independently.

1. [The project in plain language](briefings/01-the-project-in-plain-language.md) — what can be recovered from one image, and why a moving camera helps.
2. [How the models work](briefings/02-how-the-models-work.md) — detection, masks, depth, reconstruction, and affordances.
3. [Which technology to use](briefings/03-which-technology-to-use.md) — the current shortlist, including 2026 releases.
4. [Local demo and deployment](briefings/04-local-demo-and-deployment.md) — commands, hardware, and staged implementation.
5. [The neural interface](briefings/05-the-neural-interface.md) — target/action decoding, vectors, identity, and timing.
6. [Accuracy and delay](briefings/06-accuracy-delay-and-evaluation.md) — how to decide whether the system is useful enough.

## Research recommendation

Build an object and part catalogue first. Compare a lightweight detector with YOLOE-26 and SAM 3/3.1 for masks; compare MoGe or a metric DA3 configuration for geometry. Test a short camera walkthrough with DA3 or VGGT-Omega once the photo interface works. Use SceneFun3D to guide the functional-part evaluation. The [technology briefing](briefings/03-which-technology-to-use.md) gives the sources and tradeoffs.

The photo baseline uses Grounding DINO Tiny and Depth Anything V2 Small. It returns image locations and relative depth, with category-based action suggestions. Completed hidden surfaces, metric coordinates, persistent tracking, learned affordances, neural decoding, and robot control are later stages.

## Try the demo

The detailed setup is in the [deployment briefing](briefings/04-local-demo-and-deployment.md). With the project environment installed:

```powershell
.\.venv\Scripts\python.exe scripts\fetch_sample.py
.\.venv\Scripts\python.exe scripts\run_demo.py --image data\samples\coco_000000000139.jpg --gpu-name "RTX 2060" --out outputs\my-first-run
Start-Process .\outputs\my-first-run\report.html
```

Use `--device cpu` to run without CUDA. Use `--fixture` without `--image` for a synthetic interface example that does not load vision models. Each run needs a new output directory. The report opens directly from disk.

See the [first experiment](experiments/2026-11-16-baseline.md) for the measured local outcome and the distinction between a smoke test and a room accuracy evaluation.

On this workstation, open the already generated [RTX 2060 report](outputs/coco-rtx2060/report.html) or [synthetic interface example](outputs/interface-example/report.html). These are local generated files and are not part of a Git clone.

## Unity room simulation

The new [Unity quickstart](docs/unity-quickstart.md) describes the editable room, object/action controls, virtual hand and robot arm, Android phone viewing, and the decoder-input boundary. The project is under `unity/AutoSceneVR`, targeting Unity 6000.4.7f1. [Simulation design](docs/vr-simulation-design.md) explains the relationship between the photo, the authored room, and future neural input.

## Repository contents

| Folder | Contents |
| --- | --- |
| `briefings/` | Beginner-friendly research and deployment documents |
| `docs/` | Architecture and design decisions |
| `resources/` | Primary-source catalogue and guided reading |
| `src/autoscene/` | Perception baseline, scene export, and offline report |
| `scripts/` | Demo entry point and public sample downloader |
| `configs/` | Object prompts and demonstration action vocabulary |
| `schemas/` | Versioned scene output contract |
| `tests/` | Geometry conventions and selection contract checks |
| `experiments/` | Small reproducible results and environment records |
| `unity/` | Interactive room simulation, virtual arm and phone VR support |
| `data/`, `outputs/`, `.cache/` | Local images, generated reports, and caches; ignored by Git |

## Continue the work

Use the [roadmap](docs/roadmap.md) for the next experiments. Record a model's exact revision and effective configuration with every comparison. Add new evidence to the source catalogue, and update the recommendation when the results change it. Keep raw room scans and neural recordings local and outside Git.

The literature snapshot is dated 16 November 2026. Model code, weights, and sample images retain their own licences and access conditions; a link in the source catalogue does not redistribute them.
