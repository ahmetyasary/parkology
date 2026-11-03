using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Parkology
{
    /// <summary>World taps that did not land on UI.</summary>
    public static class Pointer
    {
        /// <summary>
        /// Every touch that began this frame, not only the first finger: in quick taps the next finger often lands
        /// before the previous one has lifted, and it must not be lost.
        /// </summary>
        public static void Downs(List<Vector2> positions)
        {
            positions.Clear();
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began) continue;
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                    positions.Add(touch.position);
                }
                return;
            }
            if (!Input.GetMouseButtonDown(0)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            positions.Add(Input.mousePosition);
        }
    }
}
