using UnityEngine;

namespace ConceptFactory.Cameras
{
    /// <summary>
    /// Marks something that carries the player — a vehicle, a mount, a turret — as having its own camera
    /// framing. Whoever binds the player's rig (glue code that knows what the player is currently in)
    /// looks this up on that host and hands it to the rig's <see cref="CameraModeController"/>; this
    /// component never finds or drives a camera itself, so any number of them can sit in a scene.
    /// </summary>
    public class CameraFramingSource : MonoBehaviour
    {
        [SerializeField] private CameraFramingProfile profile;
        [Tooltip("What the camera follows while framing this (empty = this object). For a vehicle, its root — the chase/top-down cameras orbit the car, not the seated driver.")]
        [SerializeField] private Transform anchor;
        [Tooltip("Let the player orbit/look around with the camera while inside (mouse/stick). Off locks the camera to its authored angle.")]
        [SerializeField] private bool allowLook = true;

        public CameraFramingProfile Profile => profile;
        public Transform Anchor => anchor != null ? anchor : transform;
        public bool AllowLook => allowLook;
    }
}
