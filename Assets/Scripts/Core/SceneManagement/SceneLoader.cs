using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.SceneManagement
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string sceneName)
        {
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            var networkManager = Unity.Netcode.NetworkManager.Singleton;
            
            // If we're connected to a network session
            if (networkManager != null && networkManager.IsClient)
            {
                // If we're the server/host, trigger network scene load
                if (networkManager.IsServer)
                {
                    networkManager.SceneManager.LoadScene(
                        sceneName,
                        UnityEngine.SceneManagement.LoadSceneMode.Single
                    );
                    yield break;
                }
                else
                {
                    // We're a client - don't load scenes manually!
                    // The server will load the scene via NetworkManager.SceneManager
                    // and all clients will automatically receive the scene load event
                    Debug.Log($"SceneLoader: Client detected - waiting for server to load scene '{sceneName}'");
                    yield break;
                }
            }

            // Not connected to network - use regular scene loading (for single-player or offline)
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

            while (!operation.isDone)
                yield return null;
        }
    }
}