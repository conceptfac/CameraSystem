using UnityEngine;

namespace ConceptFactory.Cameras
{
    /// <summary>
    /// Pure identity token for a camera style ("ThirdPerson", "FirstPerson", "TopDown") — no data, no
    /// behavior, just something comparable. Safe to share the SAME asset across multiple players'
    /// CameraModeControllers at once (e.g. both players in third person simultaneously) — this is
    /// identity, never per-instance state, so sharing it causes no cross-talk between players.
    /// </summary>
    [CreateAssetMenu(menuName = "Concept Factory/Cameras/Camera Mode Key", fileName = "CameraModeKey")]
    public class CameraModeKey : ScriptableObject
    {
    }
}
