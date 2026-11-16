# Image-grounded room reconstruction and round-trip evaluation

Date: 16 November 2026. The room is now reconstructed from image-aligned predicted geometry instead of hand-placed furniture. In the reference-camera view, the television stays on the left, the windows retain their arrangement, and the kitchen cabinet remains in the rear-right alcove. The saved scene contains separate object surface groups and can be inspected from another viewpoint.

This is a reconstruction of **visible surfaces**, not a completed and calibrated room. The earlier hand/robot simulation remains a separate task prototype. The new workflow and measured results are published with the source; images, meshes, weights and builds remain local.

## Reference photograph

The input is public COCO val2017 image 139, the same photograph used in the first baseline. MoGe-2 predicts the visible 3D points and camera intrinsics. A [review file](../resources/coco139-review.json) gives assistant-reviewed 2D outlines for 13 important objects and architectural elements. Its RGB hash binds those outlines to this photograph. The review contains no measured 3D positions.

The model is `Ruicheng/moge-2-vitb-normal`, revision `ca5f0e07ff01d3e5a364c1d954ed12ee1814b368`, using resolution level 5. The exact upstream code and utility revisions are recorded in [reconstruction-results.json](reconstruction-results.json). Model weights were downloaded from the official repository and verified against its published SHA256.

The reference result has 14 surface groups including the unassigned background, approximately 279,000 vertices and 535,000 triangles. Unassigned pixels remain a separate background surface rather than being quietly assigned to a nearby target. In Unity, object identity evidence, geometry evidence, unknown action feasibility and decoded intention are separate fields.

The fully automatic path was also run on this photograph using the same cached geometry. It proposed 25 candidates, including a false “bed” and duplicate/ambiguous categories. This confirms why automatic target identity still needs improvement. The guided 13-object reference must not be described as automatic segmentation accuracy.

## Synthetic experiment

The generator creates eight rooms from a furniture family with different positions, sizes, material colors, floor textures and lighting. TV and cabinet sides are deliberately reversed across cases. Seeds 100–103 are development cases; seeds 200–203 are held out. No model was trained on these eight renders.

The prediction function reads RGB and camera field of view. Its optional height-calibrated variant also reads the explicitly supplied camera height. Furniture positions and ray-measured depth are in separate files, opened only by the evaluator after prediction export. The normal automatic route uses Grounding DINO boxes and GrabCut, without the reference review file.

| Measurement | Development, four rooms | Held out, four rooms |
| --- | ---: | ---: |
| Mean absolute relative visible-depth error | 13.1% | **10.7%** |
| Constant 5 m depth baseline | 32.0% | 32.0% |
| Depth RMSE | 0.99 m | 0.78 m |
| Target recall at image-box IoU ≥ 0.25 | 24/24 | 24/24 |
| Correct TV/cabinet left–right order | 4/4 | 4/4 |
| Geometry inference, CPU average | 9.09 s | 7.44 s |

IoU measures overlap between two image boxes; 1 means identical boxes. Recall here concerns the six designated targets per room. Most cases produced seven candidates, so the result does not mean perfect object recognition. The 5 m comparison is a simple flat-depth baseline, not a competing research model.

These results support correct layout and useful depth structure on this small procedural family. They do not demonstrate centimetre-scale manipulation accuracy or generalization to arbitrary furniture. The next test needs new furniture families, heavier occlusion, different camera poses and measured real scenes.

## Improvements found through the loop

The first importer copied serialized mesh objects when changing scenes. The numeric depth scores looked reasonable, but the side-by-side renders exposed corrupted furniture and missing surfaces. Recreating the generated native mesh assets fixed the stale GPU-buffer behavior. All eight reconstructions were then imported and rendered again.

Preserving the source texture's exact resolution also mattered. Unity's default import resampling blurred details. Disabling that resizing and compression reduced reference-view mean absolute RGB error from about 10.4 to **2.5 on a 0–255 scale**. About **98.4% of pixels** then had mean RGB error below 20. This is an image-alignment check using the source texture; it does not establish accurate depth. Separate depth scores provide that evidence.

Triangles crossing large depth jumps are rejected. Boundary faces are assigned to adjacent object regions so segmentation seams do not create avoidable image gaps. Hidden surfaces are left absent rather than guessed into the measured layer.

The measured-height variant did not consistently improve development performance: its mean relative depth error was 15.0%, versus 13.1% for raw model scale. It remains optional and off by default. Its held-out average was 9.0%; that result does not justify silently changing the default after looking at the test set.

## Timing and verification

The completed benchmark used CPU float32 inference with four PyTorch threads. Held-out geometry times ranged from 6.89 to 8.60 seconds. The existing detection/relative-depth baseline added roughly 6–7 seconds per held-out image; that path still includes a relative-depth pass not used by the new geometry export. Mesh export, segmentation and Unity import are additional work.

The initial reference run on the RTX 2060 took 134.8 seconds for its geometry call and about 22.1 seconds to load. Subsequent diagnostics showed unusually slow local CUDA tensor transfers. No drivers or other GPU jobs were changed; CPU was used to complete evaluation. The slow GPU observation is retained rather than replaced by a paper speed claim.

The compiled Windows viewer passed checks for advancing frames, loaded surface meshes, the TV/cabinet ordering and nonblank source rendering. Ten Python tests cover the scene interface, geometry axes, floor alignment, depth-edge handling, evidence separation and review-to-image binding. Full results and artifact hashes are in the JSON record.

The intended use at this stage is reconstruction before a trial. These seconds-long measurements are not the arm's update interval and are not neural acquisition-to-display delay.

## Run and inspect

- [Workflow and commands](../docs/reconstruction-workflow.md)
- [Beginner briefing](../briefings/07-reconstructing-the-room-faithfully.md)
- [Published tools and source review](../resources/reconstruction-sources.json)
- Local round-trip report: `outputs/reconstruction-benchmark/report.html`
- Local reference result: `outputs/reconstruction-coco139-seam-refined/`
- Local automatic object candidate result: `outputs/reconstruction-coco139-automatic/`
- Standalone viewer: `unity/AutoSceneVR/Builds/Reconstruction/RoomReconstruction.exe`

Use `scripts/launch_reconstruction.ps1 -Verify` for a hidden standalone check, or omit `-Verify` to explore. The Unity editor can remain in the background.
