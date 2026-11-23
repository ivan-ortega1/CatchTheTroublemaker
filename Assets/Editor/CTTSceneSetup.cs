using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CatchTheTroublemaker.Editor
{
    /// <summary>
    /// Wires the scene objects used by the game: a GameManager plus the network, UI,
    /// world and input managers, an EventSystem and an orthographic camera.
    /// It works both on the currently open scene and on SampleScene in batch mode.
    /// Run from Tools > Catch the Troublemaker > Setup Scene.
    /// </summary>
    public static class CttSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Catch the Troublemaker/Reset Connection Settings")]
        public static void ResetConnectionSettings()
        {
            string old = PlayerPrefs.GetString(CTTConfig.PlayerPrefKey, CTTConfig.DefaultServer);
            PlayerPrefs.DeleteKey(CTTConfig.PlayerPrefKey);
            PlayerPrefs.DeleteKey("ctt_code");
            PlayerPrefs.DeleteKey("ctt_playerId");
            PlayerPrefs.Save();
            Debug.Log("[CTT] Cleared saved server URL (" + old + ") and room session. Press Play again.");
        }

        [MenuItem("Tools/Catch the Troublemaker/Setup Scene")]
        public static void Build()
        {
            var active = EditorSceneManager.GetActiveScene();
            var scene = active.IsValid() && !string.IsNullOrEmpty(active.path)
                && Object.FindFirstObjectByType<GameManager>() != null
                ? active
                : EditorSceneManager.OpenScene(ScenePath);

            Setup(scene);
        }

        private static void Setup(UnityEngine.SceneManagement.Scene scene)
        {
            ConfigureCamera();
            ConfigureEventSystem();

            GameManager manager = FindOrCreate<GameManager>("Game Manager");
            NetworkManager network = FindExisting<NetworkManager>() ??
                                     Ensure<NetworkManager>(Child(manager.transform, "Network"));
            UIManager ui = FindExisting<UIManager>() ??
                           Ensure<UIManager>(Child(manager.transform, "UI"));
            BoardRenderer board = FindExisting<BoardRenderer>() ??
                                  Ensure<BoardRenderer>(Child(manager.transform, "World"));
            PlayerController input = FindExisting<PlayerController>() ??
                                     Ensure<PlayerController>(Child(manager.transform, "Input"));

            // The transport helpers must live on the same object as NetworkManager.
            Ensure<CTTApi>(network.gameObject);
            Ensure<CttSseClient>(network.gameObject);

            manager.network = network;
            manager.ui = ui;
            manager.board = board;
            manager.playerInput = input;

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CTT] Scene setup complete: GameManager + Network/UI/World/Input wired (" + scene.path + ").");
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

        private static T FindExisting<T>() where T : Component => Object.FindFirstObjectByType<T>();

        private static T FindOrCreate<T>(string name) where T : Component
        {
            T found = FindExisting<T>();
            if (found != null) return found;
            return new GameObject(name).AddComponent<T>();
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
