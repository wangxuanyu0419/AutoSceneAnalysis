# Roadmap for the environment and intention interface

The next milestone is a measured room-target experiment. The current implementation is a photo baseline with an inspectable scene contract. It lets us improve perception without redesigning the object/action interface each time.

## Next experiment

Collect 20–30 photographs across a few capture sessions, plus one 10–20 second walkthrough of a desk or room corner. Label 10–15 task targets, including repeated categories and small interactive parts. Reserve a capture session for testing.

Compare the current baseline with one mask-producing detector and one metric-geometry backend. Keep the prompt vocabulary, target inventory, resolution, and measurement definitions explicit. Use the [evaluation briefing](../briefings/06-accuracy-delay-and-evaluation.md) to judge whether the changes help.

## Milestones

| Milestone | Completion evidence | Status |
| --- | --- | --- |
| Research foundation | Briefings, source catalogue, architecture, and staged deployment plan | Implemented |
| Photo interface | Real model inference, object/action JSON, timing, and mouse report | See first experiment |
| Room target evaluation | Held-out recall, precision, part recall, and latency | Next |
| Metric geometry | Measured surface-point error and camera convention checks | Planned |
| Persistent scan map | Same target keeps its ID across views and revisits | Planned |
| Functional interactions | Part links, object state, and grounded action evidence | Planned |
| Decoder synchronization | Versioned target slots and synchronized replay | Planned |
| Implanted neural decoding | Held-out target/action results against context-only and neural-only baselines | Planned |

## Decisions to carry forward

The intended recording modality is implanted electrodes, potentially ECoG or intracortical. Recording area, available signals, and task design remain open inputs to the later decoder work.

Begin with a limited, task-relevant inventory. Preserve “none of these,” idle, cancel, and request-another-view controls. Fix target slots during an early trial. Add a persistent mapping layer before attaching lasting meaning to object IDs.

Treat category action suggestions as interface scaffolding. Progress toward state-aware and agent-specific interaction evidence when the project reaches execution. Keep broad intention selection and fine manipulation geometry as separate acceptance questions.

## Experiment record template

For a new experiment, save its question, capture or dataset description, split, exact model revisions, effective configuration, hardware, raw measurement location, result, failure examples, and next decision. Save small numeric results in Git. Leave private media and generated artifacts in the ignored local folders.
