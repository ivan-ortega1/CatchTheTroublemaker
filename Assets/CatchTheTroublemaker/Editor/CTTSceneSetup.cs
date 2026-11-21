using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CatchTheTroublemaker.Editor
{
    /// <summary>
    /// Creates the runtime scene structure in the Unity Editor: a GameManager root,
    /// its manager children, an EventSystem and a configured orthographic camera.
    /// Run from Tools > Catch the Troublemaker > Setup Scene, or in batch mode with
    /// -executeMethod CatchTheTroublemaker.Editor.CttSceneSetup.Build.
    /// </summary>
    public static class CttSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Catch the Troublemaker/Setup Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            ConfigureCamera();
            ConfigureEventSystem();

            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                var root = new GameObject("GameManager");
                manager = root.AddComponent<GameManager>();
            }

            GameObject networkObject = Child(manager.transform, "Network");
            manager.network = Ensure<NetworkManager>(networkObject);
            Ensure<CTTApi>(networkObject);
            Ensure<CttSseClient>(networkObject);

            manager.ui = Ensure<UIManager>(Child(manager.transform, "UI"));
            manager.board = Ensure<BoardRenderer>(Child(manager.transform, "World"));
            manager.playerInput = Ensure<PlayerController>(Child(manager.transform, "Input"));

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CTT] Scene setup complete: GameManager + Network/UI/World/Input children.");
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CTTConfig.Parse("#10152f");
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void ConfigureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static GameObject Child(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
