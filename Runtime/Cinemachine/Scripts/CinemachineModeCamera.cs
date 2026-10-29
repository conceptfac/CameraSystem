#if CONCEPTFACTORY_CINEMACHINE_INSTALLED
using Unity.Cinemachine;
using UnityEngine;

namespace ConceptFactory.Cameras.Cinemachine
{
    /// <summary>What the mouse cursor does while a camera is active.</summary>
    public enum CursorMode { Locked, Free, Unchanged }

    /// <summary>
    /// Makes one Cinemachine virtual camera "know" which <see cref="CameraModeKey"/> it belongs to —
    /// when its rig's <see cref="ICameraModeSource"/> announces a switch, this vcam takes over
    /// (raises its own Priority above every other vcam on the same Brain) if the key matches, or steps
    /// back down if it doesn't. Cinemachine's own Brain only ever compares raw Priority numbers between
    /// vcams and blends into whichever wins — it has no notion of "modes"/"keys" at all. This component
    /// is the translation from our semantic mode-switch into that raw number; the two aren't redundant,
    /// they solve different problems (Brain: blend mechanics; this: which vcam should currently win).
    ///
    /// Whole file is guarded by CONCEPTFACTORY_CINEMACHINE_INSTALLED (see this assembly's own asmdef
    /// versionDefines) — this assembly's "references" list still names Unity.Cinemachine even though
    /// the package might not be installed (Unity only warns about that, it does NOT skip compiling the
    /// assembly — confirmed against Unity's own documented behavior, not assumed), so any code that
    /// unconditionally used Cinemachine types would fail to compile the moment the package is absent.
    /// The #if is what actually makes this optional, not the assembly split by itself.
    ///
    /// The source is found via GetComponentInParent&lt;ICameraModeSource&gt;, not a serialized field —
    /// this vcam is expected to live under the same rig hierarchy as whatever announces mode/target
    /// changes for it, so every vcam added under that rig is wired automatically, nothing to drag in
    /// the Inspector. Looking for the INTERFACE rather than the concrete CameraModeController lets a
    /// rig satisfy this with just its own glue component instead of needing a separate
    /// CameraModeController next to it.
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCameraBase))]
    [DefaultExecutionOrder(-50)] // framing/look limits applied before the Brain positions the camera this frame
    public class CinemachineModeCamera : MonoBehaviour
    {
        [SerializeField] private CameraModeKey key;
        [Tooltip("Priority while this vcam's key is the active one — must beat every other vcam on the same Brain for this to actually take over.")]
        [SerializeField] private int activePriority = 10;
        [Tooltip("Priority while some other key is active — 0 (Cinemachine's own default/inactive value) unless this vcam needs to compete for a specific fallback ordering.")]
        [SerializeField] private int inactivePriority = 0;
        [Tooltip("Free-look (pan/tilt) cameras only: when this camera takes over, start looking the way its tracking target faces (the character's eyes) instead of wherever it was left last time — otherwise a First Person view can come back facing behind the character and swing the body round.")]
        [SerializeField] private bool alignToTargetOnActivate = true;
        [Tooltip("What the mouse cursor does while this camera is the active one: locked and hidden for a camera the mouse looks around with (third/first person), free and visible for one you point at the world with (top-down), or left as it is.")]
        [SerializeField] private CursorMode cursor = CursorMode.Locked;
        private bool _isActive;

        private CinemachineVirtualCameraBase _vcam;
        private ICameraModeSource _source;
        private ICameraInputControl _inputSource;
        private ICameraFramingControl _framingSource;
        private CinemachineInputAxisController[] _inputControllers;
        private bool[] _inputWasEnabled;
        private bool _inputSuspended;

        // Authored framing, captured once — every applied framing is relative to it and restores to it.
        private CinemachineOrbitalFollow _orbital;
        private CinemachineFollow _follow;
        private CinemachineRotationComposer _composer;
        private float _authoredRadius;
        private Cinemachine3OrbitRig.Settings _authoredOrbits;
        private Vector3 _authoredOrbitalOffset, _authoredOrbitalDamping, _authoredFollowOffset, _authoredFollowDamping, _authoredComposerOffset;
        private bool _hasFraming;      // a framing for this key is applied (target of the blend)
        private bool _knowsFraming;    // _framing holds something to blend from/to (kept while blending out)
        private CameraFraming _framing;
        private Rigidbody _framedBody;
        private Transform _occlusionHost;
        private float _speedZoom;
        private float _weight;         // 0 = authored framing, 1 = fully framed
        private CinemachinePanTilt _panTilt;
        private CinemachineDeoccluder _deoccluder;
        private const float LookOverrideSmoothTime = 0.25f; // seconds: eases onto an animation's view, then tracks it closely
        private ICameraLookOverride _lookOverrideSource;
        private Transform _lookOverride;
        private float _panVelocity, _tiltVelocity;
        private bool _authoredUseFollowTarget;
        private float _authoredFollowTargetYOffset;
        private Transform _framedAnchor;
        private float _lookYaw;        // pan relative to the framed object's forward (limitLook)
        private float _lastPan;
        private bool _lookTracking;

        private void Awake()
        {
            _vcam = GetComponent<CinemachineVirtualCameraBase>();
            _source = GetComponentInParent<ICameraModeSource>();
            _inputSource = _source as ICameraInputControl;
            _framingSource = _source as ICameraFramingControl;
            _lookOverrideSource = _source as ICameraLookOverride;
            _inputControllers = GetComponents<CinemachineInputAxisController>();
            _inputWasEnabled = new bool[_inputControllers.Length];
            CaptureAuthoredFraming();
        }

        private void OnEnable()
        {
            if (_source == null) return;
            _source.ModeChanged += HandleModeChanged;
            _source.TargetChanged += HandleTargetChanged;
            if (_inputSource != null)
            {
                _inputSource.InputEnabledChanged += HandleInputEnabledChanged;
                HandleInputEnabledChanged(_inputSource.InputEnabled);
            }
            if (_framingSource != null)
            {
                _framingSource.FramingChanged += HandleFramingChanged;
                HandleFramingChanged(_framingSource.Framing, _framingSource.FramingAnchor);
            }
            if (_lookOverrideSource != null)
            {
                _lookOverrideSource.LookOverrideChanged += HandleLookOverrideChanged;
                HandleLookOverrideChanged(_lookOverrideSource.LookOverride);
            }
        }

        private void HandleLookOverrideChanged(Transform lookSource)
        {
            _lookOverride = lookSource;
            _panVelocity = _tiltVelocity = 0f;
        }

        // Pan to the tracking target's horizontal facing, level tilt (the eyes' own pitch is animation noise).
        private void AlignToTarget()
        {
            var target = _vcam.Follow;
            if (_panTilt == null || target == null) return;
            if (_panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.TrackingTarget || _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.LookAtTarget) return;
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) return;
            float frameYaw = _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.ParentObject && transform.parent != null ? transform.parent.eulerAngles.y : 0f;
            _panTilt.PanAxis.Value = Mathf.DeltaAngle(frameYaw, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
            _panTilt.TiltAxis.Value = 0f;
            _panVelocity = _tiltVelocity = 0f;
        }

        // Free-look (pan/tilt) cameras ease onto the override's forward and track it; nothing to do on release —
        // the player simply resumes from wherever the view is.
        private void FollowLookOverride()
        {
            if (_lookOverride == null || _panTilt == null) return;
            if (_panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.TrackingTarget || _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.LookAtTarget) return;
            Vector3 forward = _lookOverride.forward;
            float frameYaw = _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.ParentObject && transform.parent != null ? transform.parent.eulerAngles.y : 0f;
            float targetPan = Mathf.DeltaAngle(frameYaw, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
            float targetTilt = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg; // Euler X: positive looks down
            float dt = Time.deltaTime;
            _panTilt.PanAxis.Value = Mathf.DeltaAngle(0f, Mathf.SmoothDampAngle(_panTilt.PanAxis.Value, targetPan, ref _panVelocity, LookOverrideSmoothTime, Mathf.Infinity, dt));
            _panTilt.TiltAxis.Value = Mathf.SmoothDampAngle(_panTilt.TiltAxis.Value, targetTilt, ref _tiltVelocity, LookOverrideSmoothTime, Mathf.Infinity, dt);
        }

        private void OnDisable()
        {
            // OnDisable can run during domain reload or while a camera hierarchy is
            // being torn down, before Awake has found the mode source.
            if (_source != null)
            {
                _source.ModeChanged -= HandleModeChanged;
                _source.TargetChanged -= HandleTargetChanged;
            }
            if (_inputSource != null) _inputSource.InputEnabledChanged -= HandleInputEnabledChanged;
            if (_framingSource != null) _framingSource.FramingChanged -= HandleFramingChanged;
            if (_lookOverrideSource != null) _lookOverrideSource.LookOverrideChanged -= HandleLookOverrideChanged;
            _lookOverride = null;
            HandleInputEnabledChanged(true);
            HandleFramingChanged(null, null);
        }

        // ---------- per-object framing ----------

        private void CaptureAuthoredFraming()
        {
            _orbital = GetComponent<CinemachineOrbitalFollow>();
            _follow = GetComponent<CinemachineFollow>();
            _composer = GetComponent<CinemachineRotationComposer>();
            _panTilt = GetComponent<CinemachinePanTilt>();
            _deoccluder = GetComponent<CinemachineDeoccluder>();
            if (_deoccluder != null)
            {
                _authoredUseFollowTarget = _deoccluder.AvoidObstacles.UseFollowTarget.Enabled;
                _authoredFollowTargetYOffset = _deoccluder.AvoidObstacles.UseFollowTarget.YOffset;
            }
            if (_orbital != null)
            {
                _authoredRadius = _orbital.Radius;
                _authoredOrbits = _orbital.Orbits;
                _authoredOrbitalOffset = _orbital.TargetOffset;
                _authoredOrbitalDamping = _orbital.TrackerSettings.PositionDamping;
            }
            if (_follow != null)
            {
                _authoredFollowOffset = _follow.FollowOffset;
                _authoredFollowDamping = _follow.TrackerSettings.PositionDamping;
            }
            if (_composer != null) _authoredComposerOffset = _composer.TargetOffset;
        }

        private void HandleFramingChanged(CameraFramingProfile profile, Transform anchor)
        {
            CameraFraming framing = default;
            bool has = profile != null && profile.TryGet(key, out framing);
            if (has)
            {
                _framing = framing;
                _knowsFraming = true;
                _framedBody = anchor != null ? anchor.GetComponentInParent<Rigidbody>() : null;
            }
            _hasFraming = has;
            _framedAnchor = has ? anchor : null;
            // Whatever is being framed is never an obstacle to the camera framing it.
            var host = _deoccluder != null ? FramedHostOcclusion.HostOf(anchor) : null;
            if (host != _occlusionHost)
            {
                FramedHostOcclusion.Release(_occlusionHost);
                _occlusionHost = host;
                if (host != null) FramedHostOcclusion.Acquire(host, _deoccluder.IgnoreTag);
            }
            _lookTracking = false; // re-measured against the newly framed object next frame
            if (!_knowsFraming || _framing.blendTime <= 0f) _weight = has ? 1f : 0f;
            if (!has && _weight <= 0f) Forget();
            ApplyFraming();
        }

        private void Forget()
        {
            _knowsFraming = false;
            _framedBody = null;
            _speedZoom = 0f;
        }

        private void LateUpdate()
        {
            FollowLookOverride();
            if (!_knowsFraming) return;
            float dt = Time.deltaTime;
            if (_lookOverride == null) LimitLook();
            bool changed = false;

            // Ease in/out of the framing (smoothstep on a linear clock, so it settles without a jolt).
            float target = _hasFraming ? 1f : 0f;
            if (_weight != target)
            {
                float step = _framing.blendTime > 0f ? dt / _framing.blendTime : 1f;
                _weight = Mathf.MoveTowards(_weight, target, step);
                changed = true;
            }

            if (_framing.zoomOutPer10Ms > 0f && _framedBody != null)
            {
                float zoom = _hasFraming ? Mathf.Min(_framing.maxZoomOut, _framedBody.linearVelocity.magnitude / 10f * _framing.zoomOutPer10Ms) : 0f;
                float blend = _framing.zoomSmoothing > 0f ? 1f - Mathf.Exp(-dt / _framing.zoomSmoothing) : 1f;
                _speedZoom = Mathf.Lerp(_speedZoom, zoom, blend);
                changed = true;
            }

            if (changed) ApplyFraming();
            if (!_hasFraming && _weight <= 0f) Forget();
        }

        // Pan/tilt kept within ±maxLookYaw/±maxLookPitch of the framed object, and carried round with it: the
        // player's input only moves the look RELATIVE to the object (a driver glancing out of the side window
        // keeps looking out of it as the car turns). Pan is in the vcam's parent frame (ParentObject/World).
        private void LimitLook()
        {
            if (_panTilt == null || !_hasFraming || !_framing.limitLook || _framedAnchor == null || _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.TrackingTarget || _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.LookAtTarget)
            {
                _lookTracking = false;
                return;
            }
            float frameYaw = _panTilt.ReferenceFrame == CinemachinePanTilt.ReferenceFrames.ParentObject && transform.parent != null ? transform.parent.eulerAngles.y : 0f;
            float anchorYaw = Mathf.DeltaAngle(frameYaw, _framedAnchor.eulerAngles.y);
            if (_framing.lookFollowsAnchor)
            {
                // Start from wherever the view already is (no snap when control comes back from an animation).
                if (!_lookTracking) { _lookYaw = Mathf.DeltaAngle(anchorYaw, _panTilt.PanAxis.Value); _lookTracking = true; }
                else _lookYaw += Mathf.DeltaAngle(_lastPan, _panTilt.PanAxis.Value); // what the input added since last frame
            }
            else _lookYaw = Mathf.DeltaAngle(anchorYaw, _panTilt.PanAxis.Value); // world look; the object turns under it
            _lookYaw = Mathf.Clamp(_lookYaw, -_framing.maxLookYaw, _framing.maxLookYaw);
            _panTilt.PanAxis.Value = Mathf.DeltaAngle(0f, anchorYaw + _lookYaw);
            _panTilt.TiltAxis.Value = Mathf.Clamp(_panTilt.TiltAxis.Value, -_framing.maxLookPitch, _framing.maxLookPitch);
            _lastPan = _panTilt.PanAxis.Value;
        }

        private void ApplyFraming()
        {
            float w = _knowsFraming ? Mathf.SmoothStep(0f, 1f, _weight) : 0f;
            float framedScale = _knowsFraming ? (_framing.distanceScale > 0f ? _framing.distanceScale : 1f) * (1f + _speedZoom) : 1f;
            float scale = Mathf.Lerp(1f, framedScale, w);
            float offsetW = _knowsFraming && _framing.overrideTargetOffset ? w : 0f;
            float dampingW = _knowsFraming && _framing.overrideDamping ? w : 0f;
            if (_orbital != null)
            {
                _orbital.Radius = _authoredRadius * scale;
                var orbits = _authoredOrbits;
                orbits.Top.Radius *= scale; orbits.Top.Height *= scale;
                orbits.Center.Radius *= scale; orbits.Center.Height *= scale;
                orbits.Bottom.Radius *= scale; orbits.Bottom.Height *= scale;
                _orbital.Orbits = orbits;
                _orbital.TargetOffset = Vector3.Lerp(_authoredOrbitalOffset, _framing.targetOffset, offsetW);
                _orbital.TrackerSettings.PositionDamping = Vector3.Lerp(_authoredOrbitalDamping, _framing.positionDamping, dampingW);
            }
            if (_follow != null)
            {
                _follow.FollowOffset = _authoredFollowOffset * scale;
                _follow.TrackerSettings.PositionDamping = Vector3.Lerp(_authoredFollowDamping, _framing.positionDamping, dampingW);
            }
            if (_composer != null) _composer.TargetOffset = Vector3.Lerp(_authoredComposerOffset, _framing.targetOffset, offsetW);
            // Collision checked from above the framed object (not a blended value: it's a different origin).
            if (_deoccluder != null)
            {
                bool lift = _hasFraming && _framing.overrideCollisionOrigin;
                _deoccluder.AvoidObstacles.UseFollowTarget.Enabled = lift || _authoredUseFollowTarget;
                _deoccluder.AvoidObstacles.UseFollowTarget.YOffset = lift ? _framing.collisionOriginHeight : _authoredFollowTargetYOffset;
            }
        }

        private void HandleInputEnabledChanged(bool enabled)
        {
            if (!enabled && !_inputSuspended)
            {
                for (int i = 0; i < _inputControllers.Length; i++)
                {
                    _inputWasEnabled[i] = _inputControllers[i].enabled;
                    _inputControllers[i].enabled = false;
                }
                _inputSuspended = true;
            }
            else if (enabled && _inputSuspended)
            {
                for (int i = 0; i < _inputControllers.Length; i++)
                    if (_inputControllers[i] != null) _inputControllers[i].enabled = _inputWasEnabled[i];
                _inputSuspended = false;
            }
        }

        private void HandleModeChanged(CameraModeKey activeKey)
        {
            bool isActive = activeKey == key;
            if (isActive && !_isActive && alignToTargetOnActivate) AlignToTarget();
            _isActive = isActive;
            _vcam.Priority = isActive ? activePriority : inactivePriority;
            // Reports THIS vcam's own transform as the authoritative look direction the moment it
            // wins — its own look-input component (PanTilt/OrbitalFollow/...) rotates it directly,
            // untouched by however long the Brain then takes to blend the rendered camera into it
            // (see ICameraModeSource.ActiveViewTransform).
            if (isActive)
            {
                _source.ReportActiveViewTransform(transform);
                ApplyCursor();
            }
        }

        private void ApplyCursor()
        {
            if (cursor == CursorMode.Unchanged) return;
            bool locked = cursor == CursorMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // Retargets this vcam onto whichever character the rig now follows — fired right after a
        // spawner instantiates the controlled character, since no Editor-time reference to a spawned
        // object could ever have existed.
        private void HandleTargetChanged(Transform target)
        {
            _vcam.Follow = target;
            _vcam.LookAt = target;
        }
    }
}
#endif
