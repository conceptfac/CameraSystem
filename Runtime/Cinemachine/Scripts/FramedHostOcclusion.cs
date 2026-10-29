using System.Collections.Generic;
using UnityEngine;

namespace ConceptFactory.Cameras
{
    /// <summary>
    /// The camera must never treat what it's framing as an obstacle — a helicopter's tail boom sits right between a
    /// chase camera and its target. While a host (vehicle, mount) is framed, its colliders take the de-occluder's
    /// ignore tag; they get their own tags back once nothing frames it any more. Shared by every mode camera
    /// (several are alive at once), so a host is only restored when the last one lets go.
    /// </summary>
    internal static class FramedHostOcclusion
    {
        private class Entry
        {
            public int Users;
            public readonly List<(GameObject go, string tag)> Original = new List<(GameObject, string)>();
        }

        private static readonly Dictionary<Transform, Entry> Hosts = new Dictionary<Transform, Entry>();
        private static readonly List<Collider> Colliders = new List<Collider>();

        /// <summary>The object whose colliders count as "the host" for a framing anchor: its rigidbody's, else its root.</summary>
        public static Transform HostOf(Transform anchor)
        {
            if (anchor == null) return null;
            var body = anchor.GetComponentInParent<Rigidbody>();
            return body != null ? body.transform : anchor.root;
        }

        public static void Acquire(Transform host, string ignoreTag)
        {
            if (host == null || string.IsNullOrEmpty(ignoreTag)) return;
            if (!Hosts.TryGetValue(host, out var entry))
            {
                entry = new Entry();
                host.GetComponentsInChildren(true, Colliders);
                foreach (var c in Colliders)
                {
                    var go = c.gameObject;
                    if (go.CompareTag(ignoreTag) || entry.Original.Exists(o => o.go == go)) continue;
                    entry.Original.Add((go, go.tag));
                    go.tag = ignoreTag;
                }
                Hosts.Add(host, entry);
            }
            entry.Users++;
        }

        public static void Release(Transform host)
        {
            if (host == null || !Hosts.TryGetValue(host, out var entry)) return;
            if (--entry.Users > 0) return;
            foreach (var (go, tag) in entry.Original)
                if (go != null) go.tag = tag;
            Hosts.Remove(host);
        }
    }
}
