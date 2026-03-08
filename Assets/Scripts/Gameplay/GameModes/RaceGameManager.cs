using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.GameModes
{
    public class RaceGameManager : GameModeManager
    {
        private readonly HashSet<ulong> clientsFinishedLoadingRace = new();

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
            }
        }

        private void OnLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (!IsServer)
                return;

            if (sceneName != SceneManager.GetActiveScene().name)
                return;

            clientsFinishedLoadingRace.Clear();

            for (int i = 0; i < clientsCompleted.Count; i++)
            {
                clientsFinishedLoadingRace.Add(clientsCompleted[i]);
            }

            SpawnPlayers();
            Debug.Log("RaceGameManager: Race initialized.");
        }

        protected override void SpawnPlayers()
        {
            var clients = NetworkManager.Singleton.ConnectedClientsList;

            foreach (var client in clients)
            {
                if (!clientsFinishedLoadingRace.Contains(client.ClientId))
                {
                    Debug.LogWarning($"RaceGameManager: Client {client.ClientId} has not finished loading Race01. Skipping spawn.");
                    continue;
                }

                SpawnPlayer(client.ClientId);
            }
        }
    }
}