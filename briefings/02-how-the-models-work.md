# How the vision models turn pixels into useful information

The models in this project can be understood as a team of specialists. One connects words to regions, another estimates geometry, and another maintains the room's memory. The useful question is what each specialist contributes to the object/action menu.

## Finding something you can name

An ordinary detector learns a fixed list of labels. An **open vocabulary detector** also takes words at inference time, such as “cup,” “drawer handle,” or “red bottle.” It connects visual features with language features and returns matching regions.

Grounding DINO is an established example. It exchanges information between image and text representations, chooses promising object queries using language, and refines those queries into boxes. Think of the text as telling the detector which visual evidence to search for. [Grounding DINO paper](https://arxiv.org/abs/2303.05499)

“Open vocabulary” expands the naming system. It does not promise an exhaustive inventory. Prompt wording, tiny objects, clutter, and unfamiliar appearances still affect the result. An unprompted socket may never appear in a list that only asks for furniture.

Our first baseline uses Grounding DINO Tiny so that these behaviours are easy to inspect. Its score measures how strongly the model supports a detection; a score of 0.8 is not automatically an 80 percent chance that the object is correct.

## Drawing an outline around each instance

A box encloses an object and some background. A segmentation **mask** assigns pixels to the object itself. Two adjacent chairs need two instance masks, even though both are chairs. Masks matter for depth because taking all pixels inside a box can mix a cup, a desk, and the wall behind them.

SAM 3 accepts concept prompts and produces detections and masks. Its detector and tracker share a visual encoder; a presence mechanism helps decide whether the requested concept is actually present. SAM 3.1 improves joint tracking of many objects by processing groups together, sharing work that would otherwise be repeated. [SAM 3 overview](https://github.com/facebookresearch/sam3), [SAM 3.1 release notes](https://github.com/facebookresearch/sam3/blob/main/RELEASE_SAM3p1.md)

This makes the SAM family a good quality-oriented candidate for a later version. A compact YOLOE model offers another route: it combines detection and segmentation with text, image-example, or built-in vocabulary prompts, aiming for lower inference cost. [YOLOE paper](https://arxiv.org/abs/2503.07465)

## Estimating distance from appearance

A depth network looks for patterns associated with distance: perspective, familiar object size, texture, and scene structure. It learns these associations from training examples. At runtime it can produce a dense image with a depth-related value at every pixel.

Depth Anything V2 Small, used in our baseline, provides **relative inverse depth**. Larger values generally indicate nearer surfaces. These values are useful for visual structure, but are not distances in metres and should not be compared across independently processed images as if they shared a ruler. [Depth Anything V2 implementation](https://github.com/DepthAnything/Depth-Anything-V2)

Metric models such as MoGe-2 and the DA3 Metric series are trained to estimate a physical scale. “Metric” describes what the model predicts; calibration and an actual measurement tell us how accurate that prediction is in your room. Camera field of view also matters. [MoGe model documentation](https://github.com/microsoft/MoGe), [DA3 model series](https://github.com/ByteDance-Seed/Depth-Anything-3)

MoGe-3 adds a useful 2026 idea. It begins with a coarse 3D point map, lifts it into a sparse arrangement of 3D cells, and refines nearby surfaces in 3D space. This helps avoid blending two things that look adjacent in the image but are separated in depth, especially around thin structures. [MoGe-3 paper](https://arxiv.org/abs/2607.17967)

If a pixel has a measured or predicted depth Z, and we know the camera calibration, we can place it along its viewing ray. For a simple pinhole camera, X = (u − cx) Z / fx and Y = (v − cy) Z / fy. Here u and v are pixel coordinates, fx and fy describe focal length in pixels, and cx and cy mark the optical centre. The important idea is simple: **a viewing direction plus a distance gives a 3D point**. Relative inverse-depth values cannot be substituted for Z in metres.

## Using several views together

Geometry transformers exchange information between views to estimate cameras and surfaces jointly. DA3 uses depth and viewing rays as a common representation, while VGGT predicts several geometric quantities from shared image features. These are feed-forward approaches: a trained network can produce a reconstruction directly, instead of optimizing a new scene from scratch for every capture. [DA3 paper](https://arxiv.org/abs/2511.10647), [VGGT paper and code](https://github.com/facebookresearch/vggt)

VGGT-Omega improves the approach using a simpler dense prediction head and compact scene registers that carry information across views. It is a current candidate for the walkthrough experiment. A batch of reconstructed frames still needs a persistent mapping layer if you want a room that remembers objects across sessions. [VGGT-Omega paper](https://arxiv.org/abs/2605.15195)

## Making a view look 3D

Gaussian splatting represents appearance with many small coloured, translucent 3D blobs. Rendered together, these can look remarkably realistic. SHARP predicts such a representation from a single photograph and supports nearby novel views. This is attractive for an explorable visual demo. It does not automatically provide object identities, articulated parts, or grasp contact locations. [SHARP project and code](https://github.com/apple-aiml-research/ml-sharp)

SAM 3D Objects is more object-oriented: it reconstructs shape, texture, and placement from image evidence. Its completed hidden surfaces are useful hypotheses for a digital twin, rather than observations of the back of a real object. [SAM 3D Objects](https://github.com/facebookresearch/sam-3d-objects)

## Connecting shape to an interaction

Where2Act learns where pushing or pulling might move an articulated part, and proposes interaction orientations. It illustrates why affordance learning needs more than an object name: the contact point and motion direction matter. [Where2Act](https://openaccess.thecvf.com/content/ICCV2021/html/Mo_Where2Act_From_Pixels_to_Actions_for_Articulated_3D_Objects_ICCV_2021_paper.html)

FunRec takes a richer input: a colour-and-depth video of a person interacting with the environment. Observing parts move lets it discover articulation and recover a functional scene. This is an appealing later experiment for drawers and doors, once the static room map works. [FunRec](https://arxiv.org/abs/2604.05621)

For our first demo, the action menu is a small hand-written table: a cup suggests “inspect,” “grasp,” and “move.” This tests the interface design. A learned affordance model, object-state checks, and an agent's skill model would progressively improve that second axis.
