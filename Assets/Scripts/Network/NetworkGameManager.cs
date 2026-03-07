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

        // Delay slightly to ensure LobbySlotManager has rebuilt its dictionary after scene transition
        StartCoroutine(SpawnAllPlayersDelayed());
    }

    private System.Collections.IEnumerator SpawnAllPlayersDelayed()
    {
        // Wait a frame to ensure LobbySlotManager has initialized
        yield return null;
        
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

        // Get team assignments from LobbySlotManager
        LobbySlotManager lobbyManager = LobbySlotManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("NetworkGameManager: LobbySlotManager not found! Make sure it's DontDestroyOnLoad.");
            return;
        }

        IReadOnlyList<NetworkClient> clients = NetworkManager.Singleton.ConnectedClientsList;

        int runnerIndex = 0;
        int sabotagerIndex = 0;

        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i].ClientId;
            
            // Get team assignment from lobby (Team 1 = Runners, Team 2 = Sabotagers)
            int team = lobbyManager.GetTeamForClient(clientId);
            bool isRunner = (team == 1); // Team 1 = Runners, Team 2 = Sabotagers
            
            if (team == 0)
            {
                Debug.LogWarning($"NetworkGameManager: Client {clientId} not found in lobby slots. Defaulting to Runner.");
                isRunner = true;
            }

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
            // With CharacterController center at (0, 0, 0) and height 2,
            // PlayerCapsule must be at (0, 1, 0) to align CharacterController bottom with ground
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

            // Spawn the player object
            playerInstance.SpawnAsPlayerObject(clientId, true);
            
            // Assign role based on team
            var playerRole = playerInstance.GetComponent<PlayerRole>();
            if (playerRole != null)
            {
                PlayerRoleType role = isRunner ? PlayerRoleType.Runner : PlayerRoleType.Sabotager;
                playerRole.SetRoleServerRpc(role);
                Debug.Log($"NetworkGameManager: Assigned role {role} to client {clientId} (Team {team})");
            }
            else
            {
                Debug.LogError("NetworkGameManager: PlayerRole component not found on player prefab!");
            }
            
            // Set spawn point for RespawnOnFall component (if player is a Runner)
            // RespawnOnFall is on PlayerCapsule, not the root
            if (isRunner && capsule != null)
            {
                var respawnOnFall = capsule.GetComponent<RespawnOnFall>();
                if (respawnOnFall != null)
                {
                    respawnOnFall.SetSpawnPoint(spawnPoint);
                    Debug.Log($"NetworkGameManager: Set spawn point for Runner client {clientId} at {spawnPoint.name}");
                }
                else
                {
                    Debug.LogWarning($"NetworkGameManager: RespawnOnFall component not found on PlayerCapsule for Runner client {clientId}");
                }
            }
            
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

        Debug.Log($"NetworkGameManager: Spawned {clients.Count} players with team-based roles.");
    }
}