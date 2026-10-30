using UnityEditor;
using UnityEngine;

namespace ConceptFactory.Cameras.Editor
{
    [InitializeOnLoad]
    public static class CamerasHierarchyIcons
    {
        private static GUIStyle _style;
        private static GUIStyle _gearStyle;

        static CamerasHierarchyIcons()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItem;
        }

        private static void OnHierarchyItem(UnityEngine.EntityId instanceID, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            GameObject go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            if (go == null) return;

            if (!go.TryGetComponent<CameraModeController>(out _)) return;

            _style ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            _gearStyle ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            float iconSize = rect.height;
            Rect cameraRect = new Rect(rect.xMax - 20, rect.y, iconSize, iconSize);
            Rect gearRect = new Rect(rect.xMax - 9, rect.y + iconSize * 0.4f, 10, iconSize * 0.6f);

            Color prev = GUI.color;
            GUI.color = new Color(0.85f, 0.85f, 1f);
            GUI.Label(cameraRect, "📷", _style);
            GUI.color = new Color(1f, 0.85f, 0.5f);
            GUI.Label(gearRect, "⚙", _gearStyle);
            GUI.color = prev;
        }
    }
}
