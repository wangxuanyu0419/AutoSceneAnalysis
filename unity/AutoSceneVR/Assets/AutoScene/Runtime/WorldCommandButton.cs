using UnityEngine;

namespace AutoSceneVR
{
    public class WorldCommandButton : MonoBehaviour
    {
        public SimulationController simulation;
        public string label;
        public string command = "action";
        public string value;

        public void Activate()
        {
            switch (command) {
                case "action": simulation.LocalAction(value); break;
                case "target":
                    var target = simulation.FindTarget(value);
                    if (target != null) simulation.LocalSelect(target);
                    break;
                case "reset": simulation.SubmitIntent(IntentMessage.Local("reset")); break;
                case "stop": simulation.SubmitIntent(IntentMessage.Local("stop")); break;
                case "resume": simulation.SubmitIntent(IntentMessage.Local("resume")); break;
                case "recenter": simulation.viewer.Recenter(); break;
                case "embodiment": simulation.arm.SetRobotMode(!simulation.arm.robotMode); break;
                case "profile": simulation.viewer.ScanViewerProfile(); break;
            }
        }
    }
}
