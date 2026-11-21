using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CatchTheTroublemaker
{
    /// <summary>
    /// Press Play in any scene (the template SampleScene is fine): this creates
    /// the camera, EventSystem and game objects, so no manual scene wiring is needed.
    /// </summary>
    public static class CttBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<CttGame>() != null) return;

            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = CTTConfig.Parse("#10152f");
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var root = new GameObject("CatchTheTroublemaker");
            root.AddComponent<CTTApi>();
            root.AddComponent<CttSseClient>();
            root.AddComponent<CttGame>();
        }
    }
}
