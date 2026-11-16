# Reconstructing the room faithfully

The TV should stay where the photograph puts it. A reconstruction must earn that claim by projecting its geometry back through the original camera and checking the result. The earlier Unity room failed this test: it was built from a remembered description of the room, and its TV was put on a perpendicular wall. It was a control-task mockup, not the output of a reconstruction pipeline.

The new workflow starts with the image and preserves its spatial evidence. It also provides a controlled way to test what the image does not directly tell us: depth and metric scale.

## Three different meanings of “accurate”

**Image alignment** asks whether the reconstructed TV lands on the original TV pixels. It detects left/right mistakes and misplaced cabinets. **Visible geometry** asks whether the TV and cabinets have the right distances and shapes in 3D. **Complete geometry** asks whether their hidden backs and the room behind the camera are right too.

One photograph is strongest on the first question. A learned geometry model helps with the second. The third needs additional views or assumptions. A convincing new view can contain a plausible guess rather than a measurement. Keep that distinction visible when choosing experiment targets.

## What runs locally now

1. Read the upright RGB image. Preserve its resolution and camera coordinates.
2. Run **MoGe-2** to estimate a 3D point for each visible pixel and the camera's field of view. A point map is like an image in which every pixel holds X, Y, and Z instead of red, green, and blue. This replaces the old arbitrary relative-depth values with a model's metric estimates. [Official MoGe implementation](https://github.com/microsoft/MoGe)
3. Identify object candidates with Grounding DINO and separate their image regions with GrabCut. GrabCut is a classical foreground/background separation algorithm initialized by a box. It is a practical baseline; overlapping furniture and thin parts can confuse it. For the reference photograph, a small reviewed 2D outline file pins the important object identities and positions.
4. Join neighboring points into triangles. Reject triangles crossing a large depth jump, so a foreground vase does not become a stretched sheet connected to a rear cabinet. Assign seam triangles to an adjacent object to avoid artificial black gaps between image regions.
5. Estimate a floor plane and align the scene's up direction. If the camera height is supplied as a measurement, use it to calibrate global scale. Without that measurement, retain the model's estimated scale.
6. Export separate visible-surface meshes, the source texture, object evidence, camera, and a Unity scene. Re-render from the reference camera and from an offset view. The offset view exposes holes and depth mistakes that the reference view can hide.

The source texture makes the reference view look close to the photograph. That alone is not evidence of accurate depth: even a flat photograph would pass a color comparison. The benchmark therefore scores 3D depth independently.

The reconstructed objects are separate Unity objects with snapshot IDs. Geometry evidence, identity evidence, unknown action feasibility, and decoded intention remain separate fields. A visible handle does not automatically establish that a cabinet can open, or that the user intended to open it.

## The generate–photograph–reconstruct test

Unity creates eight known rooms. They vary furniture position and size, lighting direction and intensity, material colors, and procedural floor texture. Some put the TV on the left and cabinet on the right; others reverse them. Four rooms are development cases and four use held-out seeds.

Each scene supplies a rendered RGB picture and public camera calibration. A separate scoring file contains ray-measured depth and target identities. The prediction function receives the picture and calibration only. It cannot copy the furniture positions from the original scene.

The evaluator then measures:

- **Depth error:** predicted distance versus the distance measured by a ray in the known Unity room.
- **Scale error:** raw metric depth versus a separate, explicitly measured-camera-height variant. A further scale-aligned score is a diagnostic calculated only during evaluation.
- **Target recall:** how many known targets the automatic detector finds with a reasonable image-box match.
- **TV/cabinet ordering:** whether the automatic candidates preserve the correct left/right order, with missing detections counted separately.
- **Runtime:** model inference on this workstation, separate from downloads, Unity rendering, and any future neural decoder.

The result is a small procedural benchmark, not a claim of general indoor accuracy. Its next expansion should include more furniture families, occlusion, camera angles, clutter, and real room photographs. Do not tune on the held-out cases and then continue calling them held out. The [first completed experiment](../experiments/2026-11-16-reconstruction.md) reports 10.7% average visible-depth error on the four held-out rooms. Height calibration did not consistently help development cases, so it stays optional.

## Published workflows worth building on

| Work | Useful idea for this project | Current use here |
| --- | --- | --- |
| [MoGe](https://github.com/microsoft/MoGe) | Dense camera-space geometry with estimated camera parameters | MoGe-2 is the implemented local geometry backend |
| [Diorama, ICCV 2025](https://3dlg-hcvc.github.io/diorama/) | Separate architecture, retrieve object assets, fit their poses, and optimize the overall layout | Design reference for replacing partial surfaces with complete editable assets |
| [Mira-Scene, 2026](https://arxiv.org/abs/2609.23796) | Match object coordinates to image-aligned scene points to recover placement | Research candidate for better object alignment; not run here |
| [Unravel, 2026](https://arxiv.org/abs/2610.16146) | Decompose occluded scenes, reassemble in support order, and audit placements | Research reference; its public quickstart still says “coming soon” at this review |
| [SAM 3D Objects](https://github.com/facebookresearch/sam-3d-objects) | Generate independent object geometry from selected image regions | Higher-resource comparison candidate; official setup requests Linux and at least 32 GB VRAM |
| [BlenderProc](https://github.com/DLR-RM/BlenderProc) and [ProcTHOR](https://procthor.allenai.org/) | Procedural scenes and automatically generated observations for evaluation | Routes to larger and more varied tests after the small Unity benchmark |

Diorama is especially relevant to the end goal because the scene is assembled from editable objects rather than one fused visual surface. Its official setup includes several models, GPU libraries, and an asset-retrieval stage, so it is a larger integration than the current local backend. The new pipeline keeps image observations and reconstruction output separate so those stages can be upgraded without changing the control interface.

Unravel's removal-and-reassembly idea is useful for occlusion. However, an image editor's invented background remains inferred content. It should never silently acquire the status of an observed surface in our scene representation. [Unravel code-release status](https://github.com/SII-Hui/Unravel)

## Recommendation

Use the new visible-surface reconstruction to fix layout and establish the benchmark now. Make object segmentation and metric calibration the next measured improvements. Add complete object assets only when their placement can be checked against the image and depth evidence. For BCI trials, begin with a few well-supported targets and explicitly record which motion is decoded and which is assisted.

Commands and measured results are in the [reconstruction workflow](../docs/reconstruction-workflow.md) and its experiment record. The original hand/robot task remains a separate control prototype; the new surface reconstruction does not claim to have recovered articulation or physical action feasibility.
