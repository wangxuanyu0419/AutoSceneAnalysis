using UnityEngine;

namespace AutoSceneVR
{
    public class ArmController : MonoBehaviour
    {
        public Transform viewer;
        public Transform palm;
        public Transform upperArm, forearm;
        public Transform shoulderJoint, elbowJoint, wristJoint;
        public Transform[] fingers;
        public Material handMaterial, robotMaterial;
        public bool robotMode;
        public bool paused;
        public float upperLength = .38f;
        public float foreLength = .36f;
        public float maxSpeed = .45f;
        public Vector3 EndPoint => palm.position;
        public Vector3 Shoulder => viewer.position + Quaternion.Euler(0, viewer.eulerAngles.y, 0) * new Vector3(.19f, -.19f, .12f);
        public bool Reaching => reaching;
        public bool Closed => closed;

        Vector3 goal, velocity;
        float velocityExpires;
        bool reaching, closed;

        void Start() { ResetArm(); }

        public void ResetArm()
        {
            reaching = false;
            closed = false;
            velocity = Vector3.zero;
            palm.position = Shoulder + Quaternion.Euler(0, viewer.eulerAngles.y, 0) * new Vector3(-.04f, -.15f, .34f);
            palm.rotation = Quaternion.Euler(0, viewer.eulerAngles.y - 90, 0);
        }

        public bool Reach(Vector3 worldPoint)
        {
            if (Vector3.Distance(Shoulder, worldPoint) > upperLength + foreLength - .015f) return false;
            goal = worldPoint;
            reaching = true;
            velocity = Vector3.zero;
            return true;
        }

        public void SetVelocity(Vector3 worldVelocity, float seconds)
        {
            reaching = false;
            velocity = Vector3.ClampMagnitude(worldVelocity, maxSpeed);
            velocityExpires = Time.unscaledTime + Mathf.Clamp(seconds, .02f, .25f);
        }

        public void StopMotion() { velocity = Vector3.zero; reaching = false; }
        public void SetClosed(bool value) { closed = value; }

        void LateUpdate()
        {
            if (palm == null || viewer == null) return;
            if (!paused) {
                if (reaching) {
                    palm.position = Vector3.MoveTowards(palm.position, goal, maxSpeed * Time.deltaTime);
                    if (Vector3.Distance(palm.position, goal) < .005f) reaching = false;
                } else if (Time.unscaledTime <= velocityExpires) {
                    palm.position += velocity * Time.deltaTime;
                }
            }
            Vector3 shoulder = Shoulder;
            Vector3 delta = palm.position - shoulder;
            float distance = Mathf.Clamp(delta.magnitude, .12f, upperLength + foreLength - .01f);
            Vector3 direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector3.forward;
            palm.position = shoulder + direction * distance;
            palm.rotation = Quaternion.Euler(0, viewer.eulerAngles.y - 90, 0);
            float along = (upperLength * upperLength - foreLength * foreLength + distance * distance) / (2 * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 hint = viewer.right * .8f + Vector3.down * .6f;
            Vector3 perpendicular = Vector3.ProjectOnPlane(hint, direction).normalized;
            Vector3 elbow = shoulder + direction * along + perpendicular * bend;
            Segment(upperArm, shoulder, elbow, .075f);
            Segment(forearm, elbow, palm.position, .06f);
            shoulderJoint.position = shoulder;
            elbowJoint.position = elbow;
            wristJoint.position = palm.position;
            if (fingers != null)
                for (int i = 0; i < fingers.Length; i++)
                    fingers[i].localRotation = Quaternion.Slerp(fingers[i].localRotation,
                        Quaternion.Euler(closed ? 65 : 5, i == 0 ? -30 : 0, 0), 1 - Mathf.Exp(-12 * Time.deltaTime));
        }

        public void SetRobotMode(bool enabled)
        {
            robotMode = enabled;
            foreach (var r in GetComponentsInChildren<Renderer>())
                r.sharedMaterial = enabled ? robotMaterial : handMaterial;
        }

        static void Segment(Transform segment, Vector3 start, Vector3 end, float radius)
        {
            segment.position = (start + end) * .5f;
            segment.up = end - start;
            segment.localScale = new Vector3(radius, Vector3.Distance(start, end) * .5f, radius);
        }
    }
}
