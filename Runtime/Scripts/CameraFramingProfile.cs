using System;
using UnityEngine;

namespace ConceptFactory.Cameras
{
    /// <summary>
    /// How one camera style should frame one particular thing — a small hatchback, a bus, a mech —
    /// relative to how that style's camera is authored for the character on foot. Values are relative
    /// (a distance scale, not a distance) so the same profile keeps working if the rig's own cameras are
    /// re-tuned. A key with no entry here keeps its authored framing untouched (e.g. First Person, which
    /// should look exactly the same in a car as on foot).
    /// </summary>
    [Serializable]
    public struct CameraFraming
    {
        [Tooltip("The camera style this framing applies to.")]
        public CameraModeKey key;
        [Tooltip("Multiplies the style's own distance (orbit radius/heights). >1 pulls back for bigger things; for a GTA2-style top-down, push it well out.")]
        [Min(0.05f)] public float distanceScale;
        [Tooltip("Replace the point the camera follows and aims at (relative to the framed object), e.g. the roof of a tall truck.")]
        public bool overrideTargetOffset;
        public Vector3 targetOffset;
        [Tooltip("Check camera collision from this height above the framed object's pivot instead of from the aim point. Set it above the vehicle's roof: an aim point inside the body makes the vehicle's own colliders look like an obstacle, and the camera gets pushed onto its roof. The camera still collides with everything else.")]
        public bool overrideCollisionOrigin;
        public float collisionOriginHeight;
        [Tooltip("Replace the style's own position damping — a fast car usually wants a tighter (lower) one than a walking character.")]
        public bool overrideDamping;
        public Vector3 positionDamping;
        [Tooltip("Extra pull-back per 10 m/s of the framed object's speed (0 = none). Classic top-down: the camera rises as the car speeds up, so you see further ahead.")]
        [Min(0f)] public float zoomOutPer10Ms;
        [Tooltip("Cap on the speed pull-back, as an extra distance scale (0.6 = at most 60% further out).")]
        [Min(0f)] public float maxZoomOut;
        [Tooltip("Seconds the speed pull-back takes to follow speed changes.")]
        [Min(0f)] public float zoomSmoothing;
        [Tooltip("Seconds to ease into this framing when it's applied (and back out when it's removed). 0 = cut.")]
        [Min(0f)] public float blendTime;
        [Tooltip("Free-look cameras (pan/tilt, e.g. First Person): keep the look within a range around the framed object's forward and turn with it — a seated driver can glance sideways but can't spin the view like an owl.")]
        public bool limitLook;
        [Range(0f, 180f)] public float maxLookYaw;
        [Range(0f, 89f)] public float maxLookPitch;
        [Tooltip("On: the look turns WITH the framed object (a car turning keeps a sideways glance sideways). Off: the look stays put in the world and the object turns under it — only the range is enforced, so pushing against the limit makes a character's body turn to follow (it can never look past its own head).")]
        public bool lookFollowsAnchor;

        public static CameraFraming Default(CameraModeKey key) => new CameraFraming
        {
            key = key, distanceScale = 1f, zoomSmoothing = 0.6f, blendTime = 1f, maxLookYaw = 85f, maxLookPitch = 50f,
        };
    }

    /// <summary>
    /// Per-object camera framing preset — one per vehicle model (or shared by vehicles of the same size).
    /// Applied by the rig's camera adapters while a <see cref="CameraModeController"/> carries it (see
    /// <see cref="CameraModeController.SetFraming"/>); removed the moment it's cleared, restoring each
    /// camera's authored values.
    /// </summary>
    [CreateAssetMenu(menuName = "Concept Factory/Cameras/Camera Framing Profile", fileName = "CameraFramingProfile")]
    public class CameraFramingProfile : ScriptableObject
    {
        [SerializeField] private CameraFraming[] framings = Array.Empty<CameraFraming>();

        public bool TryGet(CameraModeKey key, out CameraFraming framing)
        {
            foreach (var f in framings)
                if (f.key == key) { framing = f; return true; }
            framing = default;
            return false;
        }

        private void Reset()
        {
            framings = Array.Empty<CameraFraming>();
        }
    }
}
