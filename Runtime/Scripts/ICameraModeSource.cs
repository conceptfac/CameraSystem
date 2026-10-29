using UnityEngine;

namespace ConceptFactory.Cameras
{
    /// <summary>
    /// The contract a Cinemachine adapter (or any camera adapter) actually needs — "something that
    /// announces mode/target changes for one rig" — extracted so that source doesn't have to be the
    /// concrete <see cref="CameraModeController"/> specifically. A glue component that already knows
    /// about both a Mode system (ConceptFactory.Movement or otherwise) and Cameras — like Movement's
    /// own CharacterViewSwitcher Sample — can implement this directly and skip needing a SEPARATE
    /// CameraModeController on the same rig, one less component for a GD to remember to add. Cameras
    /// itself still ships CameraModeController as the plain, reusable default for anything that has no
    /// such glue (a project using Cameras without Movement at all).
    /// </summary>
    public interface ICameraModeSource
    {
        event System.Action<CameraModeKey> ModeChanged;
        event System.Action<Transform> TargetChanged;

        /// <summary>Whichever Transform currently represents the active view's OWN look direction, independent of any blend a Brain-equivalent might be mid-transition through on the actual output camera — see <see cref="ReportActiveViewTransform"/>. Null until the first camera-mode adapter reports itself.</summary>
        Transform ActiveViewTransform { get; }

        /// <summary>Called by whichever camera-mode adapter (CinemachineModeCamera, or any other) just became the active one, handing over ITS OWN transform — e.g. a Cinemachine vcam's GameObject, which its own look-input component (PanTilt, OrbitalFollow, ...) rotates directly every frame, untouched by however a Brain blends the final rendered camera between vcams. A Mode (FirstPersonMode/ThirdPersonMode) that read the blended output camera instead would visibly chase it for the whole blend duration whenever a GD configures anything longer than a hard cut — this is what lets that duration be anything the GD wants.</summary>
        void ReportActiveViewTransform(Transform viewTransform);
    }
    /// <summary>Optional per-object framing for a rig (see <see cref="CameraFramingProfile"/>) — what the rig is framing right now and which object, for speed-dependent framing.</summary>
    public interface ICameraFramingControl
    {
        CameraFramingProfile Framing { get; }
        Transform FramingAnchor { get; }
        event System.Action<CameraFramingProfile, Transform> FramingChanged;
    }

    /// <summary>
    /// Optional "look where this looks" for a rig's free-look cameras: while set, they ease onto the
    /// transform's forward and then track it (an animation's own view — getting into a car, climbing), and
    /// give control back from wherever they ended up once cleared. Orbit cameras are unaffected.
    /// </summary>
    public interface ICameraLookOverride
    {
        Transform LookOverride { get; }
        event System.Action<Transform> LookOverrideChanged;
    }

    /// <summary>Optional input gate for a rig, independent of any movement package.</summary>
    public interface ICameraInputControl
    {
        bool InputEnabled { get; }
        event System.Action<bool> InputEnabledChanged;
    }
}
