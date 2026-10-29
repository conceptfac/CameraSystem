using UnityEngine;

namespace ConceptFactory.Cameras
{
    /// <summary>
    /// Per-instance camera-mode broadcaster — one per player/character's OWN camera rig, never a
    /// single global/shared channel. Split-screen or multiple simultaneous characters is exactly why:
    /// a shared channel would make one player's mode switch also fire every other player's camera
    /// adapters. Each player's own vcam/plain-camera adapters subscribe to THEIR rig's own
    /// controller instance (a local reference within that rig, e.g. GetComponentInParent — never a
    /// scene-wide search), so a switch here only ever reaches that one rig's own listeners.
    ///
    /// Fully agnostic of whoever calls Activate() — ConceptFactory.Movement's LocomotionModeProvider,
    /// a vehicle system, a cutscene director, anything. This class never knows or cares who raised it.
    ///
    /// This is the plain, reusable default implementation of <see cref="ICameraModeSource"/> — the
    /// component to add when nothing else on the rig already serves that role. A project that also has
    /// glue code combining a Mode system with Cameras (e.g. Movement's own CharacterViewSwitcher
    /// Sample) can implement ICameraModeSource directly instead and skip adding this component
    /// separately — CinemachineModeCamera looks for the INTERFACE on its parent, not this concrete
    /// class, so either one works.
    /// </summary>
    public class CameraModeController : MonoBehaviour, ICameraModeSource, ICameraInputControl, ICameraFramingControl, ICameraLookOverride
    {
        public Transform LookOverride { get; private set; }
        public event System.Action<Transform> LookOverrideChanged;

        /// <summary>Free-look cameras follow <paramref name="lookSource"/>'s forward (e.g. the character's eye point while an animation takes over); null hands the look back to the player. Pair it with <see cref="SetInputEnabled"/>(false) so input doesn't fight it.</summary>
        public void SetLookOverride(Transform lookSource)
        {
            if (LookOverride == lookSource) return;
            LookOverride = lookSource;
            LookOverrideChanged?.Invoke(lookSource);
        }

        public CameraFramingProfile Framing { get; private set; }
        public Transform FramingAnchor { get; private set; }
        public event System.Action<CameraFramingProfile, Transform> FramingChanged;

        /// <summary>Frames <paramref name="anchor"/> with <paramref name="profile"/>'s per-style values (a vehicle the player just got into) — null restores every camera's authored framing. Raised before the next <see cref="Activate"/>/<see cref="Follow"/> so the new style comes up already framed.</summary>
        public void SetFraming(CameraFramingProfile profile, Transform anchor)
        {
            if (Framing == profile && FramingAnchor == anchor) return;
            Framing = profile;
            FramingAnchor = profile != null ? anchor : null;
            FramingChanged?.Invoke(Framing, FramingAnchor);
        }

        public event System.Action<CameraModeKey> ModeChanged;
        /// <summary>Fired when the character this rig follows changes — e.g. right after a spawner instantiates the controlled character, since nothing can pre-wire a scene reference to something that doesn't exist yet at edit time. Deliberately a plain Transform, not a Cinemachine type — this class stays Cinemachine-agnostic; CinemachineModeCamera (which already knows about Cinemachine) is what actually retargets each vcam's Follow/LookAt when this fires.</summary>
        public event System.Action<Transform> TargetChanged;

        public bool InputEnabled { get; private set; } = true;
        public event System.Action<bool> InputEnabledChanged;

        public void SetInputEnabled(bool enabled)
        {
            if (InputEnabled == enabled) return;
            InputEnabled = enabled;
            InputEnabledChanged?.Invoke(enabled);
        }

        public Transform ActiveViewTransform { get; private set; }

        public void Activate(CameraModeKey key) => ModeChanged?.Invoke(key);
        public void Follow(Transform target) => TargetChanged?.Invoke(target);
        public void ReportActiveViewTransform(Transform viewTransform) => ActiveViewTransform = viewTransform;
    }
}
