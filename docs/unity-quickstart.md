# Using the room simulation

Open `unity/AutoSceneVR` in Unity 6000.4.7f1. The project uses the Universal Render Pipeline and Google's Cardboard SDK for Android phone viewers. The original photo's broad layout is recreated as editable furniture: wood floor, three rear windows, dining table and chairs, television, kitchen cabinets, fireplace, and a green vase. A mug, book, and placement tray are added experiment props.

The units are Unity metres, with provisional authored dimensions. The initial single-image pipeline supplies neither measured room dimensions nor the hidden surfaces used by this simulation.

## Ready builds on this workstation

The Windows build is `unity/AutoSceneVR/Builds/Windows/RoomIntentionLab.exe`. Keep the entire Windows build folder together when copying it to another PC. The Android installer is `unity/AutoSceneVR/Builds/Android/RoomIntentionLab.apk`; it is also installed on the prototype phone as **Room Intention Lab**. Builds are local generated artifacts, so a fresh Git clone needs to build them first.

From the repository root:

```powershell
.\scripts\launch_unity_demo.ps1
.\scripts\launch_unity_demo.ps1 -Background
.\scripts\launch_unity_demo.ps1 -Verify
```

The first command opens the standalone simulation. The second keeps it hidden for external input, and the third runs automated checks and exits. The Unity Editor can remain minimized throughout.

## Desktop controls

Choose a target on the left, then choose an interaction. For the first trial, select **Training mug**, choose **reach**, wait for the hand, then choose **grasp**. Use the arrows and Page Up/Page Down to move the hand; choose **release** to drop the mug. The hand's endpoint is constrained by the arm lengths.

| Control | Behaviour |
| --- | --- |
| E / G / F | Reach / grasp / release |
| Arrow keys | Move the hand in the room's horizontal plane |
| Page Up / Page Down | Move the hand vertically |
| Escape / Enter | Pause motion / resume motion |
| Right mouse drag | Look around |
| Right mouse held plus WASD | Move the desktop viewpoint |
| Tab | Switch between seated and overview cameras |
| R | Restore the seated viewpoint |
| F2 | Desktop stereo preview |
| I | Show the session folder |

**Robot arm** changes the embodiment while retaining the same endpoint and task. **Assisted reach** is an explicit target-directed movement. Turn it off to use continuous motor commands without that assistance. Opening a cabinet or pressing a switch uses a symbolic object/action command in this first version; these actions do not require simulated fingertip contact.

Continuous motor input is endpoint velocity in metres per second in the room's frame: X is right, Y is up, and Z is forward. The first controller fixes hand orientation and treats grasp as a separate command. The robot appearance shares this controller; full robot joint dynamics and dexterous finger control are later additions.

Vision and rendering have different jobs here. A room map can be prepared before a trial while the simulator renders and moves the arm continuously. The earlier photo pipeline's 431 ms perception measurement is not the frame delay of this Unity environment.

## Android phone glasses

The initial phone target is the connected Huawei MAR-LX1B, running Android 10. The build targets Android 8 or newer and includes ARMv7 and ARM64. It uses landscape rendering and a viewer profile for the optical geometry. Screen dimensions alone do not describe the lens distortion or eye spacing; scan the Renkforce viewer's compatible QR profile when available. [Google Cardboard setup](https://developers.google.com/cardboard/develop/unity/quickstart)

In the glasses, look at an object or a large button on the world panel and dwell to select it. Target and action are separate selections. The panel provides mug selection, reach, grasp, release, reset, pause, resume, embodiment change, recentering, and viewer-profile scanning. The phone supplies head orientation; it does not track a hand or the user's translation through the room.

Hold the phone upright in landscape in the viewer. When it is lying flat on a desk, the tracked view can point at the virtual ceiling; this is expected. Recenter after facing the intended forward direction.

The desktop **stereo preview** is a visual inspection tool. Actual phone VR uses the Cardboard renderer and head tracker. A later PC headset can use another XR provider while retaining the simulation's target IDs, task logic, and intent interface.

## Running without the editor in front

The compiled desktop demo runs independently of the Unity Editor. Use `scripts/launch_unity_demo.ps1` to open it, add `-Background` to keep its window hidden, or add `-Verify` to run the interaction checks, save screenshots and a JSON result, and quit automatically. Each launch places its logs in a fresh `outputs/unity-session-*` directory. Unity's own compilation and packaging can also run in a background editor or batch process.

## Building

Use the Unity menu **AutoScene > Rebuild photo room** to regenerate the editable scene from `RoomFactory.cs`. It replaces this project's generated scene, so save any hand edits as a separate scene before rebuilding.

Use **AutoScene > Build Windows demo** for a desktop executable, or **AutoScene > Build Android phone demo** for the APK. These write to the project's ignored `Builds/` folder. The editor creates the Cardboard XR settings through Unity's APIs. The Gradle hook adds the SDK's documented Java dependencies to this build's generated Gradle project.

## Sessions and decoder inputs

Each run writes `catalog.json` and `events.jsonl` into its own session directory. The catalogue lists simulation targets and their actions. The event stream records inputs, acceptance or rejection, object state, virtual contact, release, and hand position sampled at 20 Hz. Local application timestamps do not establish synchronization with a neural acquisition clock.

The external-input checkbox enables a local file adapter. Write an atomic replacement of `inbox.json` in the session folder. The supplied example sends simulated input:

```powershell
python scripts\send_sim_intent.py --session-dir "<session-folder>" --command select --target mug_01
python scripts\send_sim_intent.py --session-dir "<session-folder>" --command act --target mug_01 --action reach
# Wait for the hand to arrive before grasping.
python scripts\send_sim_intent.py --session-dir "<session-folder>" --command act --target mug_01 --action grasp
python scripts\send_sim_intent.py --session-dir "<session-folder>" --command velocity --velocity 0.1 0.1 0 --seconds 1
python scripts\send_sim_intent.py --session-dir "<session-folder>" --command act --target mug_01 --action release
```

The C# entry point is `SimulationController.SubmitIntent`, or `SubmitJson` for serialized messages. A future acquisition or network adapter should call this boundary rather than move scene objects directly. The [message schema](../schemas/simulation-intent.schema.json) defines the format. Sequence numbers prevent repeats; the scene version prevents applying a target slot from another environment. Velocity commands expire within 250 ms and are speed-limited.

For a USB-connected Android phone, `scripts/send_phone_intent.py` reads this app's active session and writes commands through a persistent ADB shell. It supports the same command, target, action, velocity, and duration flags. Pass `--adb <platform-tools/adb.exe>` when ADB is not on PATH, and `--serial` when needed. It estimates the computer-to-phone clock offset for these simulated messages and reports the clock-probe round trip. That estimate is a transport check, not synchronization of a neural acquisition device.

This file transport is an integration demonstration. Measure acquisition-to-display delay, timestamp alignment, phone performance, and failure handling with the intended streaming adapter before using it to assess neural motor control.

## Repeating the integration checks

These checks send synthetic decoder messages through the same boundary a future neural decoder will use. They do not require a brain recording.

```powershell
python scripts\check_simulator_input.py --out outputs\my-desktop-input-check
# Start Room Intention Lab on the USB phone first; put adb on PATH or add --adb.
python scripts\check_phone_simulator_input.py --out outputs\my-phone-input-check
```

Choose a new output folder for each run. The phone check resets this app's virtual task, selects and manipulates the mug, and returns it to its initial state. It records the clock probes and a rough application frame cadence. Read the [first Unity experiment](../experiments/2026-11-16-unity.md) for the measured results and outstanding wearer checks.
