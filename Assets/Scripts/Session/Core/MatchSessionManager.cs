using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Session.Data;
using Core.SceneManagement;

namespace Session.Core
{
    public class MatchSessionManager : NetworkBehaviour
    {
        public static MatchSessionManager Instance { get; private set; }

        [Header("Playlist")]
        [SerializeField]
        private List<PlaylistEntry> playlist = new List<PlaylistEntry>();

        private int currentRoundIndex = -1;

        private readonly Dictionary<ulong, int> playerScores = new();

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

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            Debug.Log("MatchSessionManager: Session initialized.");
        }

        public void StartMatch()
        {
            if (!IsServer)
                return;

            currentRoundIndex = -1;

            InitializeScores();

            LoadNextRound();
        }

        private void InitializeScores()
        {
            playerScores.Clear();

            var clients = NetworkManager.Singleton.ConnectedClientsList;

            foreach (var client in clients)
            {
                playerScores[client.ClientId] = 0;
            }
        }

        public void LoadNextRound()
        {
            if (!IsServer)
                return;

            currentRoundIndex++;

            if (currentRoundIndex >= playlist.Count)
            {
                EndMatch();
                return;
            }

            PlaylistEntry entry = playlist[currentRoundIndex];

            Debug.Log($"MatchSessionManager: Loading round {currentRoundIndex + 1} scene {entry.sceneName}");

            NetworkManager.SceneManager.LoadScene(
                entry.sceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }

        private void EndMatch()
        {
            Debug.Log("MatchSessionManager: Match finished.");

            // Later we will load a results scene here
        }
    }
}