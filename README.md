# Concept Factory — Camera System

`com.conceptfactory.cameras` is a Cinemachine-based camera mode system. It lets you define multiple named camera configurations and switch between them at runtime, with each mode driving its own Cinemachine virtual camera and framing profile.

## Included

- `CameraModeController` — runtime manager; switches the active camera mode and raises transition events
- `CinemachineModeCamera` — virtual camera component that activates/deactivates based on the current mode key
- `CameraFramingProfile` — ScriptableObject that stores framing offsets and follow settings per mode
- `CameraFramingSource` — component that provides the per-character framing target to the active camera
- `CameraModeKey` — identifier asset used to reference a named camera mode
- `ICameraModeSource` — interface for any component that can drive the active mode selection
- `FramedHostOcclusion` — Cinemachine extension that shifts the framing target to avoid occlusion

## Requirements

- Unity 6000.0+
- `com.conceptfactory.core` 1.0.2+
- Cinemachine (`com.unity.cinemachine`)

## Usage

1. Create `CameraModeKey` assets for each mode (e.g. `ThirdPerson`, `FirstPerson`).
2. Add `CameraModeController` to a scene object.
3. Add a `CinemachineModeCamera` to each Cinemachine virtual camera and assign its `CameraModeKey`.
4. Assign a `CameraFramingProfile` to each virtual camera for offset and follow settings.
5. Add `CameraFramingSource` to the character to act as the follow/look-at target.
6. Call `CameraModeController.SetMode(key)` from gameplay code or the Movement System's `LocomotionModeProvider`.
