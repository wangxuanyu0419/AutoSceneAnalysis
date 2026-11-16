using System;
using System.IO;
using UnityEngine;

namespace AutoSceneVR
{
    public class BciFileInput : MonoBehaviour
    {
        public SimulationController simulation;
        public bool enabledForSession;
        public string InboxPath => Path.Combine(simulation.SessionDirectory, "inbox.json");
        string lastRead;
        float nextRead;

        void Update()
        {
            if (!enabledForSession || simulation == null || string.IsNullOrEmpty(simulation.SessionDirectory)
                || Time.unscaledTime < nextRead) return;
            nextRead = Time.unscaledTime + .02f;
            try {
                if (!File.Exists(InboxPath)) return;
                string json = File.ReadAllText(InboxPath);
                if (json == lastRead) return;
                lastRead = json;
                simulation.SubmitJson(json);
            } catch (IOException) { /* Atomic replacement may overlap the read; retry next tick. */ }
              catch (UnauthorizedAccessException) { enabledForSession = false; simulation.status = "Cannot read the input file"; }
        }
    }
}
