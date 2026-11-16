# Roadmap for the environment and intention interface

The next perception milestone is a measured room-target experiment. The current implementation includes a photo baseline and a separate, authored Unity simulation. The shared object/action idea lets us improve perception and test control tasks without redesigning the intent interface each time.

## Next experiment

Collect 20–30 photographs across a few capture sessions, plus one 10–20 second walkthrough of a desk or room corner. Label 10–15 task targets, including repeated categories and small interactive parts. Reserve a capture session for testing.

Compare the current baseline with one mask-producing detector and one metric-geometry backend. Keep the prompt vocabulary, target inventory, resolution, and measurement definitions explicit. Use the [evaluation briefing](../briefings/06-accuracy-delay-and-evaluation.md) to judge whether the changes help.

## Milestones

| Milestone | Completion evidence | Status |
| --- | --- | --- |
| Research foundation | Briefings, source catalogue, architecture, and staged deployment plan | Implemented |
| Photo interface | Real model inference, object/action JSON, timing, and mouse report | See first experiment |
| Spatial control prototype | Editable room, virtual arm, Windows checks, Android stereo and synthetic decoder input | See [Unity experiment](../experiments/2026-11-16-unity.md) |
| Phone VR fit and timing | Viewer profile, wearer head rotation, seated alignment, sustained frame timing | Next simulation check |
| Room target evaluation | Held-out recall, precision, part recall, and latency | Next |
| Metric geometry | Measured surface-point error and camera convention checks | Planned |
| Persistent scan map | Same target keeps its ID across views and revisits | Planned |
| Functional interactions | Part links, object state, and grounded action evidence | Planned |
| Decoder synchronization | Versioned target slots and synchronized replay | Planned |
| Implanted neural decoding | Held-out target/action results against context-only and neural-only baselines | Planned |

For the simulation, start with the mug-to-tray task. Add trial cues, success regions, movement-time and path-error scoring, then replay synthetic decoder outputs with known noise and delay. Compare continuous velocity control with explicit assisted reach. Only after those baselines are clear should neural replay and acquisition-clock alignment enter the same task.

## Decisions to carry forward

The intended recording modality is implanted electrodes, potentially ECoG or intracortical. Recording area, available signals, and task design remain open inputs to the later decoder work.

Begin with a limited, task-relevant inventory. Preserve “none of these,” idle, cancel, and request-another-view controls. Fix target slots during an early trial. Add a persistent mapping layer before attaching lasting meaning to object IDs.

Treat category action suggestions as interface scaffolding. Progress toward state-aware and agent-specific interaction evidence when the project reaches execution. Keep broad intention selection and fine manipulation geometry as separate acceptance questions.

## Experiment record template

For a new experiment, save its question, capture or dataset description, split, exact model revisions, effective configuration, hardware, raw measurement location, result, failure examples, and next decision. Save small numeric results in Git. Leave private media and generated artifacts in the ignored local folders.
