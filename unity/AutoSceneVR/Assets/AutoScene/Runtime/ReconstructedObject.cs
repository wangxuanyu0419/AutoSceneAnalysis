using UnityEngine;

namespace AutoSceneVR
{
    public class ReconstructedObject : MonoBehaviour
    {
        public string snapshotId, identityEvidence;
        public string geometryEvidence = "MoGe-2 predicted visible surface";
        public string actionFeasibility = "unknown";
        public string decodedIntention = "none";
    }
}
