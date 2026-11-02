using UnityEngine;
using UnityEngine.EventSystems;

namespace Parkology
{
    /// <summary>World taps that did not land on UI.</summary>
    public static class Pointer
    {
        public static bool Down(out Vector2 position)
        {
            position = default;
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return false;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return false;
                position = touch.position;
                return true;
            }
            if (!Input.GetMouseButtonDown(0)) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
            position = Input.mousePosition;
            return true;
        }
    }
}
