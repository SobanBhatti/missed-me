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

            // Validate spawn point
            if (spawnPoint == null)
            {
                Debug.LogError($"NetworkGameManager: Spawn point is null for client {clientId}! Skipping spawn.");
                continue;
            }

            NetworkObject playerInstance = Instantiate(playerPrefab);

            // Ensure PlayerCapsule is at correct local position before setting world position
            Transform capsule = playerInstance.transform.Find("PlayerCapsule");
            if (capsule != null)
            {
                capsule.localPosition = new Vector3(0f, 1f, 0f);
            }

            // Set root position at spawn point BEFORE spawning
            playerInstance.transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );

            // Get NetworkTransform BEFORE spawning
            var networkTransform = playerInstance.GetComponent<Unity.Netcode.Components.NetworkTransform>();
            
            // Spawn the player object
            playerInstance.SpawnAsPlayerObject(clientId, true);
            
            // CRITICAL FIX: Owner clients don't receive position from NetworkTransform
            // They spawn at prefab default (0,0,0) and never get the server's position
            // Solution: Use ClientRpc to send spawn position to owner client
            var ownership = playerInstance.GetComponent<NetworkPlayerOwnership>();
            if (ownership != null)
            {
                ownership.SetSpawnPositionClientRpc(spawnPoint.position, spawnPoint.rotation);
            }
            else
            {
                Debug.LogError("NetworkPlayerOwnership component not found on player prefab!");
            }
            
            // Also use Teleport for non-owner clients
            if (networkTransform != null)
            {
                networkTransform.Teleport(
                    spawnPoint.position,
                    spawnPoint.rotation,
                    playerInstance.transform.localScale
                );
            }
            else
            {
                Debug.LogError("NetworkTransform component not found on player prefab!");
            }
            
            // Ensure PlayerCapsule local position is still correct
            if (capsule != null)
            {
                capsule.localPosition = new Vector3(0f, 1f, 0f);
                
                CharacterController charController = capsule.GetComponent<CharacterController>();
                if (charController != null && charController.enabled)
                {
                    // Force CharacterController to update its internal position
                    // by doing a zero movement - this "wakes it up" and syncs with transform
                    charController.Move(Vector3.zero);
                }
            }
            
            spawnedPlayers[clientId] = playerInstance;
        }

        Debug.Log($"NetworkGameManager: Spawned {clients.Count} players.");
    }
}