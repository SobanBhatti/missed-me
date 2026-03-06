using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Runner Spawn Points")]
    [SerializeField] private Transform[] runnerSpawnPoints;

    [Header("Sabotager Spawn Points")]
    [SerializeField] private Transform[] sabotagerSpawnPoints;

    private readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("NetworkGameManager: Player Prefab is not assigned.");
            return;
        }

        if (runnerSpawnPoints == null || runnerSpawnPoints.Length == 0)
        {
            Debug.LogError("NetworkGameManager: No runner spawn points assigned.");
            return;
        }

        if (sabotagerSpawnPoints == null || sabotagerSpawnPoints.Length == 0)
        {
            Debug.LogError("NetworkGameManager: No sabotager spawn points assigned.");
            return;
        }

        IReadOnlyList<NetworkClient> clients = NetworkManager.Singleton.ConnectedClientsList;

        int runnerIndex = 0;
        int sabotagerIndex = 0;

        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i].ClientId;
            bool isRunner = i < 4;

            Transform spawnPoint = isRunner
                ? runnerSpawnPoints[runnerIndex % runnerSpawnPoints.Length]
                : sabotagerSpawnPoints[sabotagerIndex % sabotagerSpawnPoints.Length];

            if (isRunner)
                runnerIndex++;
            else
                sabotagerIndex++;

            NetworkObject playerInstance = Instantiate(
                playerPrefab,
                spawnPoint.position,
                spawnPoint.rotation);

            playerInstance.SpawnAsPlayerObject(clientId, true);
            spawnedPlayers[clientId] = playerInstance;
        }

        Debug.Log($"NetworkGameManager: Spawned {clients.Count} players.");
    }
}