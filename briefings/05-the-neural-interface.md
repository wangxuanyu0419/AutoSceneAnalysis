# Connecting a room map to implanted neural recordings

The proposed split into **target object** and **interaction** is a useful interface design. It lets perception describe the available world while a neural decoder estimates the user's choice. Whether the recordings express those choices cleanly depends on the recording site, task, and learning procedure. The first prototype can establish the interface before that question is settled.

## What the decoder should receive

For each target, keep an ID, label, image region, geometry when available, current state, and candidate interactions. A cup on the left and another cup on the right need different IDs. A drawer handle needs a link to its parent drawer. Put the visible evidence and the model's interpretation in separate fields so that we can improve one without silently changing the other.

Think of this as a small catalogue that the decoder can point into. Ten objects with a few relevant actions each form a manageable set of candidate intentions. The candidates can change when the room changes; a decoder-facing slot mapping should remain fixed during a trial.

The implemented `scene.json` is an initial version of this catalogue. Its image coordinates are real model outputs; its action suggestions are hand-written rules. The planned persistent map will add session IDs, object parts, metric geometry, state, and observation age.

## Two axes can still depend on each other

“Open” has a different meaning for a book, a bottle, and a door. It is therefore sensible to predict the action in the context of the selected object:

`P(object, action | neural activity, scene)`

`= P(object | neural activity, scene) × P(action | object, neural activity, scene)`

This expresses your two choices without assuming they are independent. A practical model could use a shared neural representation with one target head and one object-conditioned action head. Another starting point is a neural cursor or intended endpoint, with vision translating its location into a target ID. Compare those approaches rather than assuming direct decoding of arbitrary object names will be easiest.

A scene prior can make likely choices easier to select. It can also guess the obvious action even when the neural signal contributes little. Keep a vision-only predictor and a neural-only predictor alongside the combined model so that we can see where the improvement comes from.

## Why shared autonomy is relevant

Earlier implanted BMI work blended neural arm control with vision-guided grasp assistance. Downey and colleagues tested this approach with two participants with tetraplegia and found benefits for object transport. It supports the broad idea of supplying environmental structure to a BMI, although it did not test this project's particular object/action factorization. [Downey et al. 2016](https://link.springer.com/article/10.1186/s12984-016-0134-9)

The bimanual self-feeding work shows another useful division: users provide selected task-relevant control while autonomy handles other motion details. It is a concrete reading example of reducing the amount of continuous control the user must supply. [Shared control for self-feeding, 2022](https://www.frontiersin.org/journals/neurorobotics/articles/10.3389/fnbot.2022.918001/full)

A newer AI-copilot study also combines task context and vision with BCI control, but uses EEG. Read it for the shared-autonomy architecture rather than as evidence of performance with implanted electrodes. [Lee et al. 2025](https://www.nature.com/articles/s42256-025-01090-y)

These papers motivate an architecture. They do not establish how well a new participant's ECoG or intracortical recordings will distinguish a target from an action. That is the experiment your framework should make easy to run.

## How to represent the environment as vectors

There are three useful representations, serving different purposes:

| Representation | Example | Purpose |
| --- | --- | --- |
| Human-readable scene graph | Cup 7 is on table 2; handle 4 belongs to drawer 3 | Inspection, explanation, persistent memory |
| Feature vector per object | Image centre, apparent size, visual embedding, geometry, state | Input to a decoder or target-association model |
| Output distribution | Probabilities over target slots and actions | The decoder's current estimate of intention |

Do not train the system to attach permanent meaning to the baseline's arbitrary `obj_0001`. It is a snapshot label. In a session, create a versioned table that maps persistent object IDs to decoder slots. Freeze that table for a trial, retain an explicit “none of these” choice, and update it between trials. For a variable number of objects, padding and a validity mask let a fixed tensor hold the current catalogue.

The demo's one-hot vectors show the mechanics of selection. A real decoder would normally output graded probabilities first, followed by a selection policy. Idle, cancel, and request-another-view remain available even if the object detector misses the intended target.

## Timing is part of the interface

Each prediction must identify which scene snapshot it refers to. Keep camera capture time, neural sample time, scene version, decoder output time, and the time the user sees the result. Use a common clock or measure the offset between acquisition clocks. The `created_at_utc` field in a generated file records file creation; it is not a substitute for synchronized acquisition timestamps.

Freeze the candidate set during early trials. This prevents a background perception update from turning “slot 3” into a different object while the decoder is selecting it. Later, asynchronous mapping can continue while the interface pins the current trial's target set.

## A progression of decoding experiments

Begin with mouse selection to test the scene representation. Next, replay timestamped artificial decoder outputs to test synchronization and recovery from ambiguous choices. Only then attach the neural stream.

For neural evaluation, compare target-only, action-only, and combined decoding. Vary the object locations and action combinations across held-out trials. Balance cues and labels so a decoder cannot succeed simply by recognizing a fixed visual cue or the most common action. Hold out complete runs or sessions as appropriate; neighbouring neural time windows should not leak across training and test splits.

Measure correct object/action pairs, selection time, false activations during idle, corrections, and the benefit of scene context. An action prior that makes a cup easier to grasp is useful, but its contribution should remain visible separately from the neural evidence.
