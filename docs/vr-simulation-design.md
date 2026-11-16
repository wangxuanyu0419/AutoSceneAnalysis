# A room simulation for target and motor control experiments

The first Unity environment uses an editable approximation of the living and dining room shown in the photo demo. The immediate purpose is to make the object/action interface spatial and interactive: choose a target, reach toward it with a virtual arm, and manipulate it. A desktop mode lets us verify the task before a headset is connected.

The environment will use stable authored target IDs rather than the detector's snapshot IDs. These identify simulation entities, not longitudinally tracked physical objects. Furniture positions and dimensions are provisional estimates from the image; experiment props are explicitly added to make repeatable reaching tasks possible.

## Control boundary

All input sources use the same intent interface. Desktop controls, headset controllers, and an external decoder adapter submit commands to the simulation. The simulation validates the target, available action, sequence number, scene version, and command age before changing state. Continuous velocity input expires when updates stop.

A file adapter is sufficient for the first local demonstration. It avoids coupling the Unity task to an amplifier vendor or a particular neural streaming library. The adapter can later be replaced by an acquisition-compatible transport while preserving the task interface.

## First task

The primary trial is a short reach and grasp of a prop on the dining table, followed by release at a marked destination. Other targets demonstrate pressing, switching, opening, and closing. The virtual hand and the robot arm share an endpoint controller so that task geometry can remain constant while embodiment changes.

The interaction log should separate submitted input, accepted input, motion, contact, and task completion. This makes it possible to evaluate decoder outputs and autonomous assistance separately. Timestamps from the simulation are local application timestamps until a neural acquisition clock is explicitly synchronized.

## VR verification

Desktop rendering and scripted task checks establish the operation of the simulator. Head tracking, controller mappings, frame timing on a headset, and seated alignment require a connected-device test. Keep that verification state visible alongside the build record.
