using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AutoSceneVR
{
    public class SimulationController : MonoBehaviour
    {
        public const string SceneVersion = "photo-room-v1";
        public ArmController arm;
        public ViewerController viewer;
        public RoomTarget selected;
        public RoomTarget held;
        public bool assistedReach = true;
        public string status = "Choose an object, then an interaction.";
        public string SessionDirectory { get; private set; }
        public RoomTarget[] Targets { get; private set; }
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public int ContactCount { get; private set; }
        public float SessionSeconds => Time.realtimeSinceStartup - startedAt;
        public event Action<SimulationEvent> EventRaised;

        readonly Dictionary<string, long> lastSequence = new Dictionary<string, long>();
        float startedAt, nextPoseLog;

        void Awake()
        {
            startedAt = Time.realtimeSinceStartup;
            Targets = FindObjectsByType<RoomTarget>().OrderBy(t => t.targetId).ToArray();
            SessionDirectory = Argument("--session-dir");
            if (string.IsNullOrEmpty(SessionDirectory))
                SessionDirectory = Path.Combine(Application.persistentDataPath, "Sessions", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
            Directory.CreateDirectory(SessionDirectory);
            ExportCatalogue();
            File.WriteAllText(Path.Combine(Application.persistentDataPath, "active-session.json"),
                JsonUtility.ToJson(new ActiveSession { session_directory = SessionDirectory, scene_version = SceneVersion }));
            Log("session_started", null, "Authored room; no neural decoder connected");
        }

        public static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
        }

        void Update()
        {
            if (arm != null && Time.unscaledTime >= nextPoseLog) {
                nextPoseLog = Time.unscaledTime + .05f;
                Log("pose", null, held != null ? "holding" : "free");
            }
        }

        public bool SubmitJson(string json)
        {
            try { return SubmitIntent(JsonUtility.FromJson<IntentMessage>(json)); }
            catch (Exception e) { return Reject(null, "Invalid JSON: " + e.GetType().Name); }
        }

        public bool SubmitIntent(IntentMessage message)
        {
            if (message == null || message.schema_version != 1 || message.scene_version != SceneVersion)
                return Reject(message, "Unknown schema or scene version");
            if (string.IsNullOrWhiteSpace(message.source) || message.sequence <= 0)
                return Reject(message, "A source and positive sequence are required");
            double age = IntentMessage.NowMs - message.timestamp_unix_ms;
            if (double.IsNaN(age) || double.IsInfinity(age) || age < -250 || age > 2000)
                return Reject(message, "Input timestamp is stale or ahead of the local clock");
            if (lastSequence.TryGetValue(message.source, out long previous) && message.sequence <= previous)
                return Reject(message, "Duplicate or out-of-order input");
            lastSequence[message.source] = message.sequence;
            Log("input", message, "received");
            switch (message.command) {
                case "stop":
                    arm.StopMotion(); arm.paused = true; return Accept(message, "Motion paused; resume is explicit");
                case "resume":
                    arm.paused = false; return Accept(message, "Motion enabled");
                case "reset":
                    ResetSimulation(); return Accept(message, "Scene and arm reset");
                case "velocity":
                    if (arm.paused) return Reject(message, "Motion is paused");
                    if (!Finite(message.velocity) || message.ttl_ms < 20 || message.ttl_ms > 250 || age > message.ttl_ms)
                        return Reject(message, "Velocity needs finite values and a live 20–250 ms validity window");
                    arm.SetVelocity(message.velocity, (message.ttl_ms - (float)Math.Max(0, age)) / 1000f);
                    return Accept(message, "Velocity applied with speed and workspace limits");
                case "select":
                    var target = FindTarget(message.target_id);
                    if (target == null) return Reject(message, "Target ID is absent from this scene");
                    Select(target); return Accept(message, "Selected " + target.displayName);
                case "act":
                    return ApplyAction(message);
                default: return Reject(message, "Unknown command");
            }
        }

        bool ApplyAction(IntentMessage message)
        {
            if (arm.paused) return Reject(message, "Motion is paused");
            RoomTarget target = string.IsNullOrEmpty(message.target_id) ? selected : FindTarget(message.target_id);
            if (target == null) return Reject(message, "Select an existing target first");
            if (!target.Supports(message.action)) return Reject(message, "Action is unavailable for this target");
            Select(target);
            switch (message.action) {
                case "reach":
                    if (!assistedReach) return Reject(message, "Assisted reaching is disabled; use velocity control");
                    if (!arm.Reach(target.GraspPoint)) return Reject(message, "Target is outside the arm workspace");
                    return Accept(message, "Assisted reach to " + target.displayName);
                case "grasp":
                    if (!target.movable || held != null || Vector3.Distance(arm.EndPoint, target.GraspPoint) > .13f)
                        return Reject(message, "Move the open hand close to a movable target first");
                    held = target; target.Attach(arm.palm); arm.SetClosed(true); ContactCount++;
                    Log("contact", message, "virtual grasp attached");
                    return Accept(message, "Holding " + target.displayName);
                case "release":
                    if (held == null || held != target) return Reject(message, "This target is not held");
                    held.Release(); held = null; arm.SetClosed(false);
                    Log("release", message, "physics enabled");
                    return Accept(message, "Released " + target.displayName);
                default:
                    if (!target.DiscreteAction(message.action)) return Reject(message, "Action handler is absent");
                    Log("object_state", message, "open=" + target.isOpen + ";on=" + target.isOn);
                    return Accept(message, target.displayName + ": " + message.action);
            }
        }

        public RoomTarget FindTarget(string id) => Targets.FirstOrDefault(t => t != null && t.targetId == id);
        public void LocalSelect(RoomTarget target) { SubmitIntent(IntentMessage.Local("select", target.targetId)); }
        public void LocalAction(string action) { SubmitIntent(IntentMessage.Local("act", selected != null ? selected.targetId : "", action)); }

        void Select(RoomTarget target)
        {
            if (selected != null) selected.Highlight(false);
            selected = target;
            selected.Highlight(true);
        }

        public void ResetSimulation()
        {
            if (selected != null) selected.Highlight(false);
            selected = null; held = null;
            foreach (var target in Targets) target.ResetTarget();
            arm.paused = false; arm.ResetArm();
            Log("reset", null, "simulation state restored");
        }

        bool Accept(IntentMessage message, string detail)
        {
            Accepted++; status = detail; Log("accepted", message, detail); return true;
        }

        bool Reject(IntentMessage message, string detail)
        {
            Rejected++; status = detail; Log("rejected", message, detail); return false;
        }

        public void Log(string type, IntentMessage input, string detail)
        {
            var e = new SimulationEvent {
                event_type = type, source = input?.source ?? "simulation", sequence = input?.sequence ?? 0,
                target_id = input?.target_id ?? selected?.targetId ?? "", action = input?.action ?? "",
                detail = detail, unix_ms = IntentMessage.NowMs, simulation_s = SessionSeconds,
                frame = Time.frameCount, hand_position = arm != null && arm.palm != null ? arm.EndPoint : Vector3.zero,
                head_position = viewer != null && viewer.viewCamera != null ? viewer.viewCamera.transform.position : Vector3.zero,
                head_rotation = viewer != null && viewer.viewCamera != null ? viewer.viewCamera.transform.rotation : Quaternion.identity,
                target_position = selected != null ? selected.transform.position : Vector3.zero
            };
            if (!string.IsNullOrEmpty(SessionDirectory))
                File.AppendAllText(Path.Combine(SessionDirectory, "events.jsonl"), JsonUtility.ToJson(e) + "\n");
            EventRaised?.Invoke(e);
        }

        void ExportCatalogue()
        {
            var catalog = new Catalogue {
                scene_version = SceneVersion, geometry_provenance = "manually authored photo approximation; dimensions unmeasured",
                targets = Targets.Select(t => new CatalogueTarget {
                    id = t.targetId, label = t.displayName, actions = t.actions,
                    provenance = t.provenance, position_unity_m = t.transform.position
                }).ToArray()
            };
            File.WriteAllText(Path.Combine(SessionDirectory, "catalog.json"), JsonUtility.ToJson(catalog, true));
        }

        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);

        [Serializable] class Catalogue { public string scene_version, geometry_provenance; public CatalogueTarget[] targets; }
        [Serializable] class CatalogueTarget { public string id, label, provenance; public string[] actions; public Vector3 position_unity_m; }
        [Serializable] class ActiveSession { public string session_directory, scene_version; }
    }
}
