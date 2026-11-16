using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoSceneVR
{
    public class ReconstructionNavigation : MonoBehaviour
    {
        public Camera sourceCamera, overviewCamera;
        Vector3 sourcePosition;
        Quaternion sourceRotation;
        float yaw, pitch;
        void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            sourcePosition = sourceCamera.transform.position;
            sourceRotation = sourceCamera.transform.rotation;
        }
        void Update()
        {
            var k = Keyboard.current; var m = Mouse.current;
            if (k == null) return;
            if (k.rKey.wasPressedThisFrame) {
                sourceCamera.transform.SetPositionAndRotation(sourcePosition, sourceRotation);
                yaw = pitch = 0;
            }
            if (k.tabKey.wasPressedThisFrame) {
                bool show = !overviewCamera.gameObject.activeSelf;
                overviewCamera.gameObject.SetActive(show); sourceCamera.gameObject.SetActive(!show);
            }
            if (m == null || !m.rightButton.isPressed || !sourceCamera.gameObject.activeSelf) return;
            var look = m.delta.ReadValue() * .1f; yaw += look.x; pitch = Mathf.Clamp(pitch-look.y,-80,80);
            sourceCamera.transform.rotation = sourceRotation * Quaternion.Euler(pitch,yaw,0);
            Vector3 move = Vector3.zero;
            if (k.wKey.isPressed) move += sourceCamera.transform.forward;
            if (k.sKey.isPressed) move -= sourceCamera.transform.forward;
            if (k.aKey.isPressed) move -= sourceCamera.transform.right;
            if (k.dKey.isPressed) move += sourceCamera.transform.right;
            if (k.qKey.isPressed) move -= Vector3.up;
            if (k.eKey.isPressed) move += Vector3.up;
            sourceCamera.transform.position += Vector3.ClampMagnitude(move,1) * Time.unscaledDeltaTime;
        }
        void OnGUI()
        {
            GUI.Box(new Rect(12,12,620,66), "IMAGE-GROUNDED RECONSTRUCTION · visible surfaces\nRight mouse + WASD: explore · Q/E: height · R: reference view · Tab: overview\nUnseen surfaces are absent. Photo lighting is captured in the texture.");
        }
    }

}
