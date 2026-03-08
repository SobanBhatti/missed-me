using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Gameplay.Spawning;
using System.Linq;

namespace Gameplay.GameModes
{
    public abstract class GameModeManager : NetworkBehaviour
    {
        [Header("Player Prefab")]
        [SerializeField] protected NetworkObject playerPrefab;
        protected List<SpawnPoint> spawnPoints = new();
        protected readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new();

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).ToList();
            Debug.Log("SpawnPoints found: " + spawnPoints.Count());
        }

        protected virtual void SpawnPlayers()
        {
            var clients = NetworkManager.Singleton.ConnectedClientsList;

            foreach (var client in clients)
            {
                SpawnPlayer(client.ClientId);
            }
        }

        protected virtual void SpawnPlayer(ulong clientId)
        {
            NetworkObject playerInstance = Instantiate(playerPrefab);

            Transform spawn = GetSpawnPoint(clientId);

            playerInstance.transform.SetPositionAndRotation(
                spawn.position,
                spawn.rotation
            );

            playerInstance.SpawnAsPlayerObject(clientId, true);

            spawnedPlayers[clientId] = playerInstance;
        }

        protected virtual Transform GetSpawnPoint(ulong clientId)
        {
            var genericSpawn = spawnPoints.Find(s => s.type == SpawnPointType.Generic);

            if (genericSpawn != null)
                return genericSpawn.transform;

            return transform;
        }
    }
}