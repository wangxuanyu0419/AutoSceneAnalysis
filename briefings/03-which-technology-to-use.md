# Choosing the technology for this project

Research snapshot: 16 November 2026. The recommendation is to build a small, replaceable perception pipeline, then compare stronger models on the same room tasks. Different methods lead on object outlines, geometry, rendering, and interaction understanding, so a single ranking would hide the differences that matter here.

## The shortlist

| Role | Leading or useful candidates | What they give this project | Main deployment consideration |
| --- | --- | --- | --- |
| Text-guided detection and masks | SAM 3 with SAM 3.1 tracking; YOLOE-26 | Named object and part regions | Compare quality-oriented SAM with the faster YOLOE family |
| First reproducible detector | Grounding DINO Tiny | Boxes and text labels | Already implemented here; masks come later |
| Small depth baseline | Depth Anything V2 Small | Relative surface structure | Already implemented; no metre coordinates |
| Single-image metric geometry | MoGe-2, MoGe-3, DA3 Metric | Predicted depth or point maps at physical scale | Compare against measured distances in the actual room |
| Geometry from a short scan | VGGT-Omega, DA3, MapAnything | Cameras and surfaces across views | Memory grows with view count; add object association |
| A visual 3D preview | SHARP | Single-image Gaussian scene | Strong visual demonstration, separate from functional understanding |
| Completed individual objects | SAM 3D Objects | Object shapes and placement | Official setup exceeds this workstation's individual GPU memory |
| Persistent object map | ConceptGraphs; newer OpenSplatGraph | Object identities and relations | Their mapping inputs and release maturity differ |
| Functional parts and motion | SceneFun3D, Where2Act, FunRec | Handles, possible contacts, articulation | Dataset, affordance model, and interaction-video method serve different roles |

Links and the reasons for these choices follow. These are candidates worth testing, rather than a claim that they have identical benchmarks or solve an entire room end to end.

## Recognition and segmentation

**SAM 3 and SAM 3.1** are the quality-oriented candidates. Text and visual examples can specify a concept, and the system returns matching instance masks. The March 2026 release specifically improves multi-object video tracking. Meta's approximately sevenfold speedup figure is for 128 objects on an H100 against its earlier SAM 3 release; it is not an expected speedup for this workstation. [SAM 3 paper](https://arxiv.org/abs/2511.16719), [SAM 3.1 release](https://github.com/facebookresearch/sam3/blob/main/RELEASE_SAM3p1.md)

**YOLOE-26** is the first speed-oriented alternative to benchmark. Its official implementation supports text prompts, image examples, and a prompt-free built-in vocabulary. The small model gives us a practical route toward frequent updates with masks. Start it in a separate environment because the first text-prompt setup also downloads a text encoder. [Official YOLOE documentation](https://docs.ultralytics.com/models/yoloe)

**Grounding DINO Tiny** supplies our working reference point. Its advantage here is a straightforward supported API and easy-to-inspect output. Once we have room annotations, we can judge a new detector by what it improves over this baseline. [Model card](https://huggingface.co/IDEA-Research/grounding-dino-tiny)

## Geometry

**MoGe-3** is a current candidate for fine detail in a single-image point map. Its July 2026 paper and August code release extend the MoGe family with refinement in sparse 3D space. Compare it with MoGe-2 to learn whether better detail actually helps locate the handles and small targets in your room. [MoGe-3 paper](https://arxiv.org/abs/2607.17967), [release and models](https://github.com/microsoft/MoGe)

**Depth Anything 3** is attractive because one family covers single and multiple views. Choose the checkpoint deliberately: the main series, relative monocular series, metric model, and nested metric combination have different purposes. A small main-series model does not inherit the metric guarantees of the separate metric configuration. [DA3 model documentation](https://github.com/ByteDance-Seed/Depth-Anything-3)

**VGGT-Omega** is a 2026 successor to VGGT and a strong walkthrough candidate. Use the 512 model for in-the-wild exploration and the September reproduction checkpoint for reported-benchmark comparisons, following the upstream guidance. The released text-aligned variant is interesting for future spatial features, but it does not constitute a trained neural intention decoder. [Official checkpoints and release notes](https://github.com/facebookresearch/vggt-omega)

**MapAnything** combines image-only and optional geometry inputs in one metric reconstruction framework. Its unified interface to several reconstruction models could save integration work if multi-view geometry becomes a major part of the project. It is a later comparison, after the basic menu and measurement tools work. [MapAnything](https://github.com/facebookresearch/map-anything)

## Maps and interactions

**ConceptGraphs** remains a useful design reference for compact object graphs. **OpenSplatGraph**, announced for ACCV 2026, connects persistent object nodes to an online Gaussian semantic map. At the time checked, its official repository said source code and installation instructions were still coming. Put it on the reading list rather than the immediate installation list. [ConceptGraphs](https://concept-graphs.github.io/), [OpenSplatGraph release status](https://github.com/csiro-robotics/OpenSplatGraph)

**SceneFun3D** should guide the evaluation of functional parts. **Where2Act** is useful for understanding learned contact and motion proposals. **FunRec** is the closer long-term direction for observing a drawer or door move and building an articulated scene, but requires RGB-D interaction video. [SceneFun3D](https://scenefun3d.github.io/), [Where2Act](https://arxiv.org/abs/2101.02692), [FunRec](https://arxiv.org/abs/2604.05621)

## What to postpone

SHARP is worth trying if an explorable 3D view helps explain the system. Apple reports subsecond prediction on a standard GPU; we have not measured it here. SAM 3D Objects is worth revisiting on larger hardware: its official setup specifies Linux and at least 32 GB VRAM. Neither is the first dependency for testing whether a user can select the correct object/action pair. [SHARP](https://github.com/apple-aiml-research/ml-sharp), [SAM 3D Objects setup](https://github.com/facebookresearch/sam-3d-objects/blob/main/doc/setup.md)

The next implementation choice is **masks plus metric geometry**, tested against the same photograph and a measured desk scene. The more consequential experiment is then whether a short camera walkthrough reduces missed targets and position error enough to justify its acquisition and processing time.
