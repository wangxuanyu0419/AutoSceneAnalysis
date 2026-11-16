using System;
using UnityEngine;

namespace AutoSceneVR
{
    [Serializable]
    public class IntentMessage
    {
        public int schema_version = 1;
        public string scene_version = "photo-room-v1";
        public string source = "simulation";
        public long sequence;
        public double timestamp_unix_ms;
        public string command;
        public string target_id;
        public string action;
        public Vector3 velocity;
        public int ttl_ms = 250;

        public static double NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static IntentMessage Local(string command, string target = "", string action = "")
        {
            return new IntentMessage {
                command = command, target_id = target, action = action,
                source = "local", sequence = ++localSequence, timestamp_unix_ms = NowMs
            };
        }
        static long localSequence;
    }

    [Serializable]
    public class SimulationEvent
    {
        public string scene_version = "photo-room-v1";
        public string event_type;
        public string source;
        public long sequence;
        public string target_id;
        public string action;
        public string detail;
        public double unix_ms;
        public double simulation_s;
        public int frame;
        public Vector3 hand_position;
        public Vector3 head_position;
        public Quaternion head_rotation;
        public Vector3 target_position;
    }
}
