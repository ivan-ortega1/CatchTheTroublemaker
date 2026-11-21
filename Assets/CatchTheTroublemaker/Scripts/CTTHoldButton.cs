using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchTheTroublemaker
{
    /// <summary>A button that reports held/released state (used by the touch D-pad).</summary>
    public class CttHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private System.Action<bool> onChange;

        public void Setup(System.Action<bool> callback) => onChange = callback;

        public void OnPointerDown(PointerEventData eventData) => onChange?.Invoke(true);
        public void OnPointerUp(PointerEventData eventData) => onChange?.Invoke(false);
        public void OnPointerExit(PointerEventData eventData) => onChange?.Invoke(false);

        private void OnDisable() => onChange?.Invoke(false);
    }
}
