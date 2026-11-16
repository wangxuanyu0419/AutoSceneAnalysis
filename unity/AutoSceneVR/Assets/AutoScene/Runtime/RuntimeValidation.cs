using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AutoSceneVR
{
    public class RuntimeValidation : MonoBehaviour
    {
        [Serializable] class Check { public string name; public bool passed; }
        [Serializable] class Result { public string scene_version; public bool passed; public int frame_start, frame_end; public string device; public Check[] checks; }
        readonly List<Check> checks = new List<Check>();

        IEnumerator Start()
        {
            string folder = SimulationController.Argument("--smoke-test");
            if (string.IsNullOrEmpty(folder)) yield break;
            yield return Run(folder);
            if (SimulationController.Argument("--quit-after-test") == "yes") Application.Quit();
        }

        public IEnumerator Run(string folder)
        {
            Directory.CreateDirectory(folder);
            checks.Clear();
            var sim = GetComponent<SimulationController>();
            yield return new WaitForSecondsRealtime(.5f);
            int startFrame = Time.frameCount;
            sim.ResetSimulation();
            var mug = sim.FindTarget("mug_01");
            Add("Stable authored target catalogue", sim.Targets.Length >= 10 && mug != null);
            var version = IntentMessage.Local("select", "mug_01"); version.scene_version = "wrong-scene";
            Add("Rejects another scene version", !sim.SubmitIntent(version));
            var stale = IntentMessage.Local("velocity"); stale.timestamp_unix_ms -= 5000;
            Add("Rejects stale input", !sim.SubmitIntent(stale));
            var select = IntentMessage.Local("select", "mug_01");
            Add("Target axis selects the mug", sim.SubmitIntent(select) && sim.selected == mug);
            Add("Rejects repeated sequence", !sim.SubmitIntent(select));
            Add("Rejects unsupported action", !sim.SubmitIntent(IntentMessage.Local("act", "mug_01", "open")));
            Add("Action axis starts explicit assisted reach", sim.SubmitIntent(IntentMessage.Local("act", "mug_01", "reach")));
            yield return new WaitForSecondsRealtime(2.0f);
            Add("Hand arrives at mug", Vector3.Distance(sim.arm.EndPoint, mug.GraspPoint) < .04f);
            Add("Near hand grasps movable mug", sim.SubmitIntent(IntentMessage.Local("act", "mug_01", "grasp")) && sim.held == mug);
            Vector3 originalMug = mug.transform.position;
            for (int i = 0; i < 8; i++) {
                var move = IntentMessage.Local("velocity"); move.velocity = new Vector3(.10f, .08f, .05f);
                sim.SubmitIntent(move);
                yield return new WaitForSecondsRealtime(.04f);
            }
            Add("Held object follows continuous motor input", Vector3.Distance(originalMug, mug.transform.position) > .015f);
            yield return new WaitForSecondsRealtime(.35f);
            Vector3 expiredPosition = sim.arm.EndPoint;
            yield return new WaitForSecondsRealtime(.25f);
            Add("Expired velocity stops moving the hand", Vector3.Distance(expiredPosition, sim.arm.EndPoint) < .005f);
            sim.SubmitIntent(IntentMessage.Local("stop"));
            Add("Pause rejects motor commands", !sim.SubmitIntent(IntentMessage.Local("act", "mug_01", "reach")));
            sim.SubmitIntent(IntentMessage.Local("resume"));
            bool handImage = Capture(sim.viewer.viewCamera, Path.Combine(folder, "hand-grasp.png"));
            Add("Release restores object physics", sim.SubmitIntent(IntentMessage.Local("act", "mug_01", "release")) && sim.held == null);
            Add("Drawer accepts open action", sim.SubmitIntent(IntentMessage.Local("act", "drawer_01", "open")));
            yield return new WaitForSecondsRealtime(.5f);
            Add("Drawer animation moves its part", sim.FindTarget("drawer_01").animatedPart.localPosition.z < -.25f);
            sim.ResetSimulation();
            sim.viewer.SetOverview(true);
            yield return new WaitForSecondsRealtime(.2f);
            bool roomImage = Capture(sim.viewer.overviewCamera, Path.Combine(folder, "room-overview.png"));
            yield return null;
            sim.viewer.SetOverview(false);
            Add("The player advances frames", Time.frameCount > startFrame + 20);
            Add("Hidden-window capture renders scene content", handImage && roomImage);
            var result = new Result {
                scene_version = SimulationController.SceneVersion, passed = checks.TrueForAll(c => c.passed),
                frame_start = startFrame, frame_end = Time.frameCount, device = SystemInfo.graphicsDeviceName,
                checks = checks.ToArray()
            };
            File.WriteAllText(Path.Combine(folder, "runtime-validation.json"), JsonUtility.ToJson(result, true));
            sim.status = result.passed ? "Self-check passed. Select a target to begin." : "Self-check failed; inspect runtime-validation.json";
            Debug.Log("AUTOSCENE_VALIDATION " + JsonUtility.ToJson(result));
        }

        void Add(string name, bool passed) { checks.Add(new Check { name = name, passed = passed }); }

        static bool Capture(Camera camera, string path)
        {
            var texture = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            float aspect = camera.aspect;
            camera.aspect = 1.6f;
            try {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                var pixels = image.GetPixels32();
                int min = 765, max = 0;
                for (int i = 0; i < pixels.Length; i += 1019) {
                    int value = pixels[i].r + pixels[i].g + pixels[i].b;
                    min = Math.Min(min, value); max = Math.Max(max, value);
                }
                Destroy(image);
                return max - min > 30;
            } finally {
                camera.aspect = aspect;
                RenderTexture.active = previous;
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
