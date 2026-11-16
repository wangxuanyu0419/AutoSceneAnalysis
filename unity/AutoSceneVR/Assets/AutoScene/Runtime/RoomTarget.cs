using System;
using UnityEngine;

namespace AutoSceneVR
{
    public class RoomTarget : MonoBehaviour
    {
        public string targetId;
        public string displayName;
        public string provenance = "authored approximation from photo";
        public string[] actions = { "inspect" };
        public bool movable;
        public Transform animatedPart;
        public Vector3 openOffset;
        public Vector3 openEuler;
        public Light controlledLight;
        public RoomTarget stateOwner;
        public Renderer stateRenderer;
        public Color onColor = new Color(.09f, .18f, .22f);
        public Color offColor = new Color(.015f, .02f, .025f);
        public bool isOpen;
        public bool isOn = true;
        public bool held;
        public Vector3 graspOffset = new Vector3(0, .075f, 0);
        public Vector3 GraspPoint => transform.TransformPoint(graspOffset);

        Vector3 initialPosition, closedPosition;
        Quaternion initialRotation, closedRotation;
        Renderer[] renderers;
        Color[] baseColors;
        Rigidbody body;
        bool highlighted;

        void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            if (animatedPart != null) {
                closedPosition = animatedPart.localPosition;
                closedRotation = animatedPart.localRotation;
            }
            body = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].sharedMaterial.HasProperty("_BaseColor")
                    ? renderers[i].sharedMaterial.GetColor("_BaseColor") : Color.white;
        }

        void Update()
        {
            if (animatedPart == null) return;
            animatedPart.localPosition = Vector3.Lerp(animatedPart.localPosition,
                closedPosition + (isOpen ? openOffset : Vector3.zero), 1 - Mathf.Exp(-8 * Time.deltaTime));
            animatedPart.localRotation = Quaternion.Slerp(animatedPart.localRotation,
                closedRotation * Quaternion.Euler(isOpen ? openEuler : Vector3.zero), 1 - Mathf.Exp(-8 * Time.deltaTime));
        }

        public bool Supports(string action) => Array.IndexOf(actions, action) >= 0;

        public void Highlight(bool selected)
        {
            highlighted = selected;
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) {
                var block = new MaterialPropertyBlock();
                Color baseColor = renderers[i] == stateRenderer ? (isOn ? onColor : offColor) : baseColors[i];
                block.SetColor("_BaseColor", selected ? Color.Lerp(baseColor, new Color(.15f, .95f, .85f), .55f) : baseColor);
                renderers[i].SetPropertyBlock(block);
            }
        }

        public bool DiscreteAction(string action)
        {
            if (!Supports(action)) return false;
            if (stateOwner != null) {
                bool applied = stateOwner.DiscreteAction(action);
                isOn = stateOwner.isOn;
                return applied;
            }
            switch (action) {
                case "open": isOpen = true; break;
                case "close": isOpen = false; break;
                case "switch_on": isOn = true; break;
                case "switch_off": isOn = false; break;
                case "press": isOn = !isOn; break;
                case "inspect": break;
                default: return false;
            }
            if (controlledLight != null) controlledLight.enabled = isOn;
            if (stateRenderer != null) Highlight(highlighted);
            return true;
        }

        public void Attach(Transform hand)
        {
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
            held = true;
            transform.SetParent(hand, true);
        }

        public void Release()
        {
            transform.SetParent(null, true);
            held = false;
            if (body != null) { body.isKinematic = false; body.useGravity = true; }
        }

        public void ResetTarget()
        {
            transform.SetParent(null, true);
            held = false;
            if (body != null) {
                if (!body.isKinematic) {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
            }
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            isOpen = false;
            isOn = true;
            if (controlledLight != null) controlledLight.enabled = true;
            if (stateRenderer != null) Highlight(false);
        }
    }
}
