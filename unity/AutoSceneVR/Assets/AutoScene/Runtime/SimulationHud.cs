using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoSceneVR
{
    public class SimulationHud : MonoBehaviour
    {
        public SimulationController simulation;
        public BciFileInput fileInput;
        public bool showDetails;
        GUIStyle heading, body, button, small;
        Vector2 targetScroll;
        float smoothedDelta = .016f;
        static Rect panel = new Rect(18, 18, 330, 680);

        public static bool MouseOverPanel()
        {
            if (Mouse.current == null || Application.platform == RuntimePlatform.Android) return false;
            Vector2 p = Mouse.current.position.ReadValue();
            return panel.Contains(new Vector2(p.x, Screen.height - p.y));
        }

        void Update()
        {
            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, .03f);
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) simulation.SubmitIntent(IntentMessage.Local("stop"));
            if (k.enterKey.wasPressedThisFrame) simulation.SubmitIntent(IntentMessage.Local("resume"));
            if (k.gKey.wasPressedThisFrame) simulation.LocalAction("grasp");
            if (k.fKey.wasPressedThisFrame) simulation.LocalAction("release");
            if (k.eKey.wasPressedThisFrame) simulation.LocalAction("reach");
            if (k.iKey.wasPressedThisFrame) showDetails = !showDetails;
            Vector3 velocity = Vector3.zero;
            if (k.upArrowKey.isPressed) velocity.z += .22f;
            if (k.downArrowKey.isPressed) velocity.z -= .22f;
            if (k.leftArrowKey.isPressed) velocity.x -= .22f;
            if (k.rightArrowKey.isPressed) velocity.x += .22f;
            if (k.pageUpKey.isPressed) velocity.y += .22f;
            if (k.pageDownKey.isPressed) velocity.y -= .22f;
            if (velocity != Vector3.zero) {
                var message = IntentMessage.Local("velocity"); message.velocity = velocity;
                simulation.SubmitIntent(message);
            }
        }

        void OnGUI()
        {
            if (simulation == null || simulation.viewer == null || simulation.viewer.PhoneVr) return;
            InitStyles();
            if (simulation.viewer.stereoPreview) {
                for (int eye = 0; eye < 2; eye++) {
                    float x = Screen.width * (.25f + eye * .5f);
                    GUI.Label(new Rect(x - 15, Screen.height * .5f - 16, 40, 40), "+", heading);
                }
                GUI.Label(new Rect(20, 15, Screen.width - 40, 60), "Stereo preview · F2 to exit · no headset tracking in this desktop preview", body);
                return;
            }
            panel.height = Mathf.Min(Screen.height - 36, 840);
            GUI.color = new Color(.06f, .10f, .15f, .96f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(panel.x + 16, panel.y + 12, panel.width - 32, panel.height - 24));
            GUILayout.Label("ROOM / INTENTION LAB", small);
            GUILayout.Label("Objects → actions", heading);
            GUILayout.Label("Photo-inspired simulation · provisional dimensions", small);
            GUILayout.Space(12);
            GUILayout.Label("1  Choose a target", body);
            targetScroll = GUILayout.BeginScrollView(targetScroll, GUILayout.Height(180));
            foreach (var target in simulation.Targets) {
                GUI.backgroundColor = simulation.selected == target ? new Color(.18f, .8f, .68f) : Color.white;
                if (GUILayout.Button(target.displayName, button)) simulation.LocalSelect(target);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndScrollView();
            GUILayout.Space(10);
            GUILayout.Label("2  Choose an interaction", body);
            if (simulation.selected == null) GUILayout.Label("Select a room object or a target above.", small);
            else {
                GUILayout.Label(simulation.selected.displayName + " · " + simulation.selected.targetId, small);
                GUILayout.BeginHorizontal();
                int column = 0;
                foreach (string action in simulation.selected.actions) {
                    if (column == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); column = 0; }
                    if (GUILayout.Button(action.Replace('_', ' '), button)) simulation.LocalAction(action);
                    column++;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            GUILayout.Label(simulation.status, body);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(simulation.arm.paused ? "Resume" : "Pause", button))
                simulation.SubmitIntent(IntentMessage.Local(simulation.arm.paused ? "resume" : "stop"));
            if (GUILayout.Button("Reset trial", button)) simulation.SubmitIntent(IntentMessage.Local("reset"));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(simulation.arm.robotMode ? "Human hand" : "Robot arm", button)) simulation.arm.SetRobotMode(!simulation.arm.robotMode);
            if (GUILayout.Button(simulation.viewer.overview ? "Seated view" : "Room overview", button)) simulation.viewer.SetOverview(!simulation.viewer.overview);
            GUILayout.EndHorizontal();
            simulation.assistedReach = GUILayout.Toggle(simulation.assistedReach, " Assisted reach (explicit action)");
            fileInput.enabledForSession = GUILayout.Toggle(fileInput.enabledForSession, " External decoder file input");
            GUILayout.Space(10);
            GUILayout.Label("E reach · G grasp · F release\nArrows + PgUp/PgDn move hand\nRight mouse + WASD look / move\nTab overview · R recenter · F2 stereo", small);
            GUILayout.Label($"{1 / Mathf.Max(.001f, smoothedDelta):0} FPS · {simulation.Accepted} accepted inputs", small);
            if (showDetails) GUILayout.Label("Input and logs: " + simulation.SessionDirectory, small);
            GUILayout.EndArea();
            if (!simulation.viewer.overview) GUI.Label(new Rect(Screen.width * .5f - 8, Screen.height * .5f - 12, 30, 30), "+", body);
        }

        void InitStyles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            small.normal.textColor = new Color(.65f, .78f, .83f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(9, 9, 7, 7), wordWrap = true };
        }
    }
}
