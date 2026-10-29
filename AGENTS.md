# com.conceptfactory.cameras — Agent Guide

Camera mode system built on Cinemachine. Manages which camera configuration is active at runtime via a named key system. Never references Movement or any other Concept Factory package — the bridge is always an external piece.

## Architecture

```
CameraModeKey (ScriptableObject, identity only)
CameraModeController (MonoBehaviour, per-character instance — NOT global)
  └─► ICameraModeSource (interface — adapters listen to this)
        └─► CinemachineModeCamera (on each vcam — adjusts Priority to activate/deactivate)
CameraFramingSource (on character — provides follow/look-at target)
CameraFramingProfile (ScriptableObject — framing offsets per mode)
```

## Key design decisions

**Per-instance channel, never global.** `CameraModeController` is one per character rig. `CameraModeKey` assets ARE shared (both players can be in "ThirdPerson" using the same asset) — the channel is what's per-instance, not the key.

**Cinemachine is optional.** The main `ConceptFactory.Cameras.Runtime` assembly has zero Cinemachine references. All Cinemachine code lives in `Runtime/Cinemachine/` with its own assembly definition gated by:
```json
"defineConstraints": ["CONCEPTFACTORY_CINEMACHINE_INSTALLED"],
"versionDefines": [{ "name": "com.unity.cinemachine", "expression": "", "define": "CONCEPTFACTORY_CINEMACHINE_INSTALLED" }]
```
`com.unity.cinemachine` is NOT in `package.json` dependencies — that would force installation. The asmdef knows about it; the package manifest does not.

**`CinemachineModeCamera` resolves controller via `GetComponentInParent<ICameraModeSource>()`** — no drag-and-drop field to fill. Any vcam placed under the rig's root auto-connects. `ICameraModeSource` is the interface, so the parent can be `CameraModeController` OR any external component (e.g. `PossessionViewController` from Movement Samples) that implements it.

**No camera references on spawnables.** Nothing on a prefab that gets `Instantiate()`d should have a `[SerializeField] Camera` field. Camera is injected at runtime by scene-placed binding scripts, never serialized on the spawnable.

## Key assets shipped

- `Runtime/Cinemachine/Prefabs/GTACamera.prefab` — orbital third-person vcam (with `CinemachineModeCamera`, key = `ThirdPerson.asset`)
- `Runtime/Cinemachine/Prefabs/TopDownCamera.prefab` — top-down vcam (locked vertical axis ~85°, no horizontal orbit)
- `Runtime/Cinemachine/Configs/ThirdPerson.asset` — `CameraModeKey`
- `Runtime/Cinemachine/Configs/TopDown.asset` — `CameraModeKey`

## Pending

- Fixed camera style (Resident Evil 1) — zone-based, different concept from `CameraModeKey`, not designed yet
- Camera without Cinemachine adapter — not implemented

## Dependencies

- `com.conceptfactory.core` 1.0.2+
- Cinemachine (`com.unity.cinemachine`) — optional, only for Cinemachine sub-assembly
