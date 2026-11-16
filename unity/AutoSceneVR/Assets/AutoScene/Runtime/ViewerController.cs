using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace AutoSceneVR
{
    public class ViewerController : MonoBehaviour
    {
        public Camera viewCamera;
        public Camera overviewCamera;
        public SimulationController simulation;
        public bool overview;
        public bool stereoPreview;
        public bool gazeDwell = true;
        public float dwellSeconds = 1.25f;
        public string hoverLabel = "";
        public float dwellProgress;
        public bool PhoneVr => Application.platform == RuntimePlatform.Android && XrActive;
        public bool XrActive {
            get {
                displays.Clear();
                SubsystemManager.GetSubsystems(displays);
                return displays.Exists(d => d.running);
            }
        }
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        Object hover;
        float hoverStarted;
        bool dwellFired;
        Vector3 initialPosition;
        Quaternion initialRotation;
        Camera previewLeft, previewRight;
        float pitch, yaw;

        void Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 72;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            yaw = viewCamera.transform.localEulerAngles.y;
            var tracked = viewCamera.gameObject.AddComponent<TrackedPoseDriver>();
            tracked.positionInput = new InputActionProperty(new InputAction("Head position", binding: "<XRHMD>/centerEyePosition"));
            tracked.rotationInput = new InputActionProperty(new InputAction("Head rotation", binding: "<XRHMD>/centerEyeRotation"));
            tracked.trackingStateInput = new InputActionProperty(new InputAction("Head tracking", binding: "<XRHMD>/trackingState"));
            if (Application.platform == RuntimePlatform.Android) {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                QualitySettings.shadows = ShadowQuality.Disable;
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool xr = XrActive;
            if (!xr && !overview && mouse != null && mouse.rightButton.isPressed) {
                Vector2 look = mouse.delta.ReadValue() * .11f;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -70, 70);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (!xr && keyboard != null) {
                if (keyboard.tabKey.wasPressedThisFrame) SetOverview(!overview);
                if (keyboard.rKey.wasPressedThisFrame) Recenter();
                if (keyboard.f2Key.wasPressedThisFrame) SetStereoPreview(!stereoPreview);
                if (!overview && mouse != null && mouse.rightButton.isPressed) {
                    Vector3 delta = Vector3.zero;
                    if (keyboard.wKey.isPressed) delta += viewCamera.transform.forward;
                    if (keyboard.sKey.isPressed) delta -= viewCamera.transform.forward;
                    if (keyboard.aKey.isPressed) delta -= viewCamera.transform.right;
                    if (keyboard.dKey.isPressed) delta += viewCamera.transform.right;
                    delta.y = 0;
                    transform.position += Vector3.ClampMagnitude(delta, 1) * Time.deltaTime * 1.4f;
                }
            }
            if (overview) return;
            bool activate = !xr && mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (!xr && keyboard != null && keyboard.spaceKey.wasPressedThisFrame) activate = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (xr) {
                Google.XR.Cardboard.Api.UpdateScreenParams();
                if (Google.XR.Cardboard.Api.HasNewDeviceParams()) Google.XR.Cardboard.Api.ReloadDeviceParams();
                if (Google.XR.Cardboard.Api.IsGearButtonPressed) Google.XR.Cardboard.Api.ScanDeviceParams();
                activate |= Google.XR.Cardboard.Api.IsTriggerPressed;
                if (Google.XR.Cardboard.Api.IsTriggerHeldPressed) Recenter();
            }
#endif
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!xr && !stereoPreview && mouse != null && !mouse.rightButton.isPressed
                && !SimulationHud.MouseOverPanel())
                ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            Object candidate = null;
            string label = "";
            if (Physics.Raycast(ray, out RaycastHit hit, 12)) {
                var hitButton = hit.collider.GetComponentInParent<WorldCommandButton>();
                var hitTarget = hit.collider.GetComponentInParent<RoomTarget>();
                if (hitButton != null) { candidate = hitButton; label = hitButton.label; }
                else if (hitTarget != null) { candidate = hitTarget; label = hitTarget.displayName; }
            }
            if (candidate != hover) { hover = candidate; hoverStarted = Time.unscaledTime; dwellFired = false; }
            hoverLabel = label;
            dwellProgress = candidate != null ? Mathf.Clamp01((Time.unscaledTime - hoverStarted) / dwellSeconds) : 0;
            // Gaze only selects an object. Actions require their own visible button selection.
            bool dwell = xr && gazeDwell && dwellProgress >= 1 && !dwellFired;
            if ((activate || dwell) && candidate != null && !SimulationHud.MouseOverPanel()) {
                dwellFired = true;
                if (candidate is RoomTarget target) simulation.LocalSelect(target);
                else if (candidate is WorldCommandButton button) button.Activate();
            }
        }

        public void SetOverview(bool enabled)
        {
            if (XrActive) return;
            overview = enabled;
            if (enabled && stereoPreview) SetStereoPreview(false);
            overviewCamera.gameObject.SetActive(enabled);
            viewCamera.gameObject.SetActive(!enabled);
        }

        public void Recenter()
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            pitch = yaw = 0;
            viewCamera.transform.localRotation = Quaternion.identity;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (XrActive) Google.XR.Cardboard.Api.Recenter();
#endif
            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputs);
            foreach (var input in inputs) input.TryRecenter();
        }

        public void ScanViewerProfile()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (XrActive) Google.XR.Cardboard.Api.ScanDeviceParams();
#endif
        }

        public void SetStereoPreview(bool enabled)
        {
            if (XrActive) return;
            stereoPreview = enabled;
            if (previewLeft == null) {
                previewLeft = MakeEye("Stereo preview left", -.032f, new Rect(0, 0, .5f, 1));
                previewRight = MakeEye("Stereo preview right", .032f, new Rect(.5f, 0, .5f, 1));
            }
            viewCamera.enabled = !enabled;
            previewLeft.gameObject.SetActive(enabled);
            previewRight.gameObject.SetActive(enabled);
        }

        Camera MakeEye(string name, float offset, Rect viewport)
        {
            var go = new GameObject(name);
            go.transform.SetParent(viewCamera.transform, false);
            go.transform.localPosition = new Vector3(offset, 0, 0);
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(viewCamera);
            camera.rect = viewport;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = 75;
            return camera;
        }
    }
}
