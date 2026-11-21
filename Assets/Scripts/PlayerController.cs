using UnityEngine;
using UnityEngine.InputSystem;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Reads the local player's intent from the new Input System keyboard and the
    /// on-screen touch pad. It never moves the character: intent is POSTed to the
    /// server, which remains authoritative for positions and collisions.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("How often the input intent is sent to the server, in seconds.")]
        [Range(0.05f, 0.5f)]
        public float SendInterval = 0.1f;

        private UIManager ui;

        public void Initialize(UIManager uiManager) => ui = uiManager;

        /// <summary>Returns (dx, dy, sprint) where dx/dy are -1, 0 or 1.</summary>
        public Vector3 ReadIntent()
        {
            int dx = ui != null ? ui.TouchX : 0;
            int dy = ui != null ? ui.TouchY : 0;
            bool sprint = ui != null && ui.TouchSprint;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) dx--;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) dx++;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) dy--;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) dy++;
                sprint |= keyboard.spaceKey.isPressed
                       || keyboard.leftShiftKey.isPressed
                       || keyboard.rightShiftKey.isPressed;
            }

            return new Vector3(Mathf.Clamp(dx, -1, 1), Mathf.Clamp(dy, -1, 1), sprint ? 1f : 0f);
        }
    }
}
