# Reading the literature in a useful order

Start with the questions each paper helps answer. The papers cover several different tasks, so their results should not be combined into a single accuracy score. Primary links and the basis of each entry are recorded in [sources.json](sources.json). Last review: 16 November 2026.

## First understand the representation

Read the opening figure and method overview of [ConceptGraphs](https://concept-graphs.github.io/). Ask how an object observation becomes a persistent object, and what information is stored on relationships. Then inspect [SceneFun3D](https://scenefun3d.github.io/): compare a whole cabinet with its handle and think about which one should be a target in the neural interface.

These two references establish the central distinction between recognizing a room and making its interactions addressable.

## Then understand the working baseline

Use [Grounding DINO](https://arxiv.org/abs/2303.05499) to understand text-conditioned localization, and [Depth Anything V2](https://github.com/DepthAnything/Depth-Anything-V2) to understand relative surface structure. Run the local example alongside the reading. Change one target word and inspect what changes in the output; then look for a clearly visible object that the prompt list omitted.

The relevant lesson is the input/output contract. You do not need to understand every layer of a transformer before evaluating whether the target menu is useful.

## Compare the current recognition candidates

Look at [SAM 3](https://arxiv.org/abs/2511.16719) and the [SAM 3.1 update](https://github.com/facebookresearch/sam3/blob/main/RELEASE_SAM3p1.md), then [YOLOE](https://arxiv.org/abs/2503.07465) and its [YOLOE-26 implementation](https://docs.ultralytics.com/models/yoloe). Focus on the prompt, the returned instance identity, and how much work must be repeated for a new frame.

Write down the camera resolution, number of objects, hardware, and timing boundary before comparing any published speed figures.

## Add geometry when the task needs it

Compare [MoGe-3](https://arxiv.org/abs/2607.17967) with the [MoGe family](https://github.com/microsoft/MoGe). Look at thin structures in the examples and ask whether their improved geometry would change a target-selection error.

For scans, read [DA3](https://arxiv.org/abs/2511.10647), [VGGT-Omega](https://arxiv.org/abs/2605.15195), and the [MapAnything overview](https://github.com/facebookresearch/map-anything). Follow which inputs establish camera position and scale. Separate a network's batch reconstruction from the additional engineering needed for a persistent online map.

## Leave visual completion and articulation for specific questions

[SHARP](https://github.com/apple-aiml-research/ml-sharp) addresses visual exploration from a photograph. [SAM 3D Objects](https://github.com/facebookresearch/sam-3d-objects) addresses completed object geometry. Ask whether a convincing hidden surface is necessary for the intended experiment, and how it would be checked.

[Where2Act](https://arxiv.org/abs/2101.02692) introduces contact-specific action information. [FunRec](https://arxiv.org/abs/2604.05621) uses observed RGB-D interaction to recover moving parts. Both become more useful when the project reaches handles, hinges, and motion parameters.

[OpenSplatGraph](https://github.com/csiro-robotics/OpenSplatGraph) is a current direction to revisit when implementation code becomes available. Its persistent object graph is relevant; its current release status makes it unsuitable as the first reproducible demo.

## Connect perception to shared control

Read [Downey et al. 2016](https://link.springer.com/article/10.1186/s12984-016-0134-9), [bimanual self-feeding in 2022](https://www.frontiersin.org/journals/neurorobotics/articles/10.3389/fnbot.2022.918001/full), and [AI copilots in 2025](https://www.nature.com/articles/s42256-025-01090-y). Identify what the user controls, what vision contributes, and what autonomy supplies. Keep the EEG study distinct from the implanted-BMI precedents.

For each future paper note, record the task, inputs, outputs, public implementation status, useful result, hardware, limitations for this project, and the next experiment it suggests. Prefer a concrete experiment over a broad “state of the art” label.
