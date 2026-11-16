# Testing whether the system is useful enough

The right test is whether the system supplies the targets and interaction choices your task needs, at the time the user needs them. A benchmark score or a beautiful reconstructed room helps choose candidates, but cannot answer that question on its own.

## Start with a small room task

Use a desk or a section of a room with 10–15 intended targets: two similar cups, a bottle, a book, a remote, a drawer handle, a switch, and a few larger objects. Record which things are visible and which are hidden. Take photographs under several lighting and clutter conditions, and record one short camera walkthrough.

Create a human inventory before inspecting the model output. Include small interactive parts as their own targets. An annotated list of only objects the model already found would conceal its most important errors.

Split the photographs into development and test groups, preferably by capture session or viewpoint. Choose words and thresholds on development images, then evaluate the frozen configuration on the test set.

## Measure the errors that change the interface

| Question | Useful measurement | Why it matters |
| --- | --- | --- |
| Did the intended target appear? | Recall of task-relevant visible targets | A missing target cannot be selected |
| Did the system invent a target? | Precision of the proposed target list | False candidates clutter the menu |
| Did it find the handle itself? | Recall and mask overlap for functional parts | A cabinet box may hide a missed handle |
| Is the location useful? | Pixel error, then visible-surface position error in cm | Separates selection geometry from manipulation geometry |
| Does identity survive motion? | ID changes and reacquisition after occlusion | The decoder must keep pointing to the same thing |
| Are the actions sensible? | Human review of category, state, part, and agent skill | “Open” can be plausible yet unavailable now |
| Does the user select the right pair? | Joint target/action accuracy and correction time | Directly tests the interface's purpose |

Recall is the fraction of relevant targets recovered. Precision is the fraction of proposed targets that are right. Mask overlap is often measured with intersection over union: how much the predicted and reference regions overlap relative to their combined area. Report small parts separately, because excellent furniture detection can conceal poor switch detection.

For metric geometry, measure several distances from the camera to visible reference points. Use a depth camera, calibration target, or a practical ruler arrangement with known geometry. Compare predicted and reference points in the same coordinate frame. A box's median depth is not an object's true 3D centre. Surface points and object centres require different reference labels.

Monocular relative inverse depth needs more than an arbitrary multiplication to become metric depth. Scale, offset, camera calibration, and the model's output convention all matter. Evaluate a metric model or measured depth explicitly when centimetres become part of the requirement.

## Separate three kinds of numbers

**Paper results** describe a particular benchmark, model, resolution, hardware, and timing scope. For example, SHARP's subsecond prediction result concerns single-image Gaussian reconstruction, while SAM 3.1's multi-object speedup concerns video tracking on an H100. Neither number is the delay of an object/action interface on an RTX 2060. [SHARP](https://github.com/apple-aiml-research/ml-sharp), [SAM 3.1](https://github.com/facebookresearch/sam3/blob/main/RELEASE_SAM3p1.md)

**Local measurements** come from the same executable pipeline and hardware you intend to use. Our demo records repeated perception times, model-load time, selected GPU, package versions, image size, and peak PyTorch memory. CUDA work is synchronized before stopping the timer. Read the [baseline record](../experiments/2026-11-16-baseline.md) for this machine's result.

**Design goals** express what would be useful in the next experiment. They should change when the task or measurements justify it.

## Proposed goals for the first room pilot

The following are starting goals for an object-selection interface, rather than measured model capabilities or robot execution criteria:

| Aspect | Initial goal | If it misses |
| --- | --- | --- |
| Visible task-target recall | At least 90 percent, with small parts reported separately | Improve prompts, masks, viewpoint, or target-specific training |
| Candidate precision | At least 90 percent | Adjust thresholds and merge duplicate hypotheses |
| Initial inventory | Within 3 seconds after models are loaded, for a focused view | Use a smaller detector or separate initial mapping from selection |
| Age of displayed target positions | p95 below 200 ms during active tracking | Reduce resolution, use a tracking path, and remove stale queues |
| Useful 3D selection layout | Median visible-target position error below 5 cm at desk distance | Add calibrated depth or multiple views |
| Identity during a short trial | No target-ID swaps in a 30 second trial | Freeze slots and improve association before neural integration |

The 5 cm goal is for testing a spatial selection layout. A grasp, button press, or insertion can demand a much tighter task-specific contact estimate. Do not transfer this number to manipulation acceptance.

## What delay means to the user

Distinguish time to first inventory, ongoing map refresh, and the delay from intended selection to visible feedback. Models can remain loaded after mapping, so a target selection need not rerun the full discovery pipeline.

For a live system, timestamp capture, queue entry, preprocessing, inference, object association, decoder output, and display. Report median and p95, with cold startup separate. Neural acquisition windows and decoding add their own delay. Pipeline stages may overlap, so measure the complete path in addition to each stage.

A common failure is processing every camera frame even after the computer falls behind. The GPU may report good throughput while the displayed scene is seconds old. Use a bounded queue that favours the newest frame, retain the observation age on every object, and distinguish static mapped objects from currently observed ones.

The photo baseline is a smoke test of inference and representation. Its repeated processing of one image is not a live-camera test and does not establish accuracy across rooms. It gives us a reproducible starting point for the more informative room experiment above.
