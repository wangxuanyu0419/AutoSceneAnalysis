# Turning a room into a menu of intentions

Imagine looking at a room and thinking, “that cup, pick it up.” Your proposed interface needs to connect those two choices to something the computer can represent. Computer vision can supply the visible candidates. A neural decoder can then estimate which candidate and which interaction the user intends.

The first useful product is a **map of things you can refer to and act on**. It can begin as labels and clickable regions on a photograph. Three dimensional geometry can improve that map as the project grows.

## Five questions hidden inside the task

Suppose a cup stands on a desk beside a drawer.

| Question | What the computer needs | Name used in computer vision |
| --- | --- | --- |
| What is present? | Cup, desk, drawer | Object detection |
| Which pixels belong to each thing? | An outline of the cup, separate from the desk | Instance segmentation |
| Where is it? | Image position, then distance and 3D position | Depth estimation and reconstruction |
| What can act on what? | The handle belongs to the drawer; pulling it may open the drawer | Parts, relationships, and affordances |
| Is it still the same thing? | The cup keeps its identity after the camera moves | Tracking and object association |

These are related problems, but progress on one does not automatically solve the others. A sharp outline tells us where a cup appears in the image. It does not establish the cup's weight or whether a robot can reach it.

**Affordance** means a possible interaction for a particular actor in a particular situation. A handle may afford pulling. A small robot cannot necessarily move the chair that a person can move. This makes your second decoding axis partly about the agent and its available skills, as well as the object.

SceneFun3D is especially relevant: it labels functional elements such as handles, knobs, and buttons, alongside the actions and motions associated with them. It makes a good research target for the step after whole-object detection. [SceneFun3D project](https://scenefun3d.github.io/)

## One photograph and one camera are different starting points

One photograph gives us one projection of the room. A small object nearby can occupy the same number of pixels as a large object far away. A chair can hide a socket. Models use learned patterns to fill in these gaps, much as a person estimates familiar sizes from a photograph.

A moving camera produces several projections. If you move sideways while recording, nearby objects shift against the background more than distant ones. That change provides geometric evidence called **parallax**. A phone walkthrough can therefore offer much more information than a still image, even though both use one camera. Turning a camera around a fixed point mainly expands coverage; moving its position adds the parallax useful for geometry.

For this project, use a photograph to prove the object/action interface. Then compare it with a short walkthrough and, if available, an RGB-D scan. RGB-D means an ordinary colour image plus a measured depth image.

## How much 3D do we need first

A useful progression is:

1. **Image map:** “cup at this region of the screen.” Enough for a selection interface.
2. **Visible surface map:** “these visible cup pixels appear nearer than the wall.” Often called 2.5D, because the back of the object is missing.
3. **Metric object map:** “this visible surface is about 0.8 metres from the camera.” Useful for reach planning once calibrated and checked.
4. **Functional map:** “this part is a handle; this drawer slides along this axis.” Useful for manipulation.

A whole-room mesh is optional for the early decoding experiments. It is more valuable when you need collision checking, navigation, or a simulator. A compact scene graph often gives the intention decoder what it needs with less computation: objects are nodes; relationships such as “part of” and “supported by” connect them.

ConceptGraphs provides an influential example of building this kind of object map from multiple posed colour-and-depth images. Its input already includes depth and camera poses; it should not be read as a method that obtains a reliable room map from one photograph. [ConceptGraphs](https://concept-graphs.github.io/)

## A practical first research question

Start with: **Can we find and keep track of the small set of targets a user needs in this room, and present sensible interaction choices quickly enough?**

This is easier to test than “identify everything an agent could ever interact with.” Make an inventory of your room's task targets, including small parts. Count missed targets. Check whether two similar cups remain distinguishable. Measure the time until the interface shows the updated choice.

Your long-term idea is plausible as an environment-aware decoding framework. The first vision demo can establish the quality of its input menu. Whether object and action intention can be decoded from your implanted recordings is a separate experiment, described in the [neural interface briefing](05-the-neural-interface.md).
