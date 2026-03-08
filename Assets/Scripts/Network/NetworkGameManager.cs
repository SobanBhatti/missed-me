using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkGameManager : NetworkBehaviour
{
    [Header("Player Prefab")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Runner Spawn Points")]
    [SerializeField] private Transform[] runnerSpawnPoints;

    [Header("Sabotager Spawn Points")]
    [SerializeField] private Transform[] sabotagerSpawnPoints;

    private readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new();
    private readonly HashSet<ulong> clientsFinishedLoadingGameplay = new();

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

        if (sceneName != "Gameplay")
            return;

        clientsFinishedLoadingGameplay.Clear();

        for (int i = 0; i < clientsCompleted.Count; i++)
        {
            clientsFinishedLoadingGameplay.Add(clientsCompleted[i]);
        }

        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        if (!ValidateSetup())
            return;

        LobbySlotManager lobbyManager = LobbySlotManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("NetworkGameManager: LobbySlotManager not found.");
            return;
        }

        IReadOnlyList<NetworkClient> clients = NetworkManager.Singleton.ConnectedClientsList;

        int runnerIndex = 0;
        int sabotagerIndex = 0;

        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i].ClientId;

            if (!clientsFinishedLoadingGameplay.Contains(clientId))
            {
                Debug.LogWarning($"NetworkGameManager: Client {clientId} has not finished loading Gameplay. Skipping spawn.");
                continue;
            }

            if (spawnedPlayers.ContainsKey(clientId))
            {
                continue;
            }

            PlayerRoleType role = GetRoleForClient(lobbyManager, clientId);
            bool isRunner = role == PlayerRoleType.Runner;

            Transform spawnPoint = isRunner
                ? runnerSpawnPoints[runnerIndex % runnerSpawnPoints.Length]
                : sabotagerSpawnPoints[sabotagerIndex % sabotagerSpawnPoints.Length];

            if (isRunner)
                runnerIndex++;
            else
                sabotagerIndex++;

            if (spawnPoint == null)
            {
                Debug.LogError($"NetworkGameManager: Spawn point is null for client {clientId}. Skipping spawn.");
                continue;
            }

            SpawnPlayerForClient(clientId, role, spawnPoint);
        }
    }

    private bool ValidateSetup()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("NetworkGameManager: Player Prefab is not assigned.");
            return false;
        }

        if (runnerSpawnPoints == null || runnerSpawnPoints.Length == 0)
        {
            Debug.LogError("NetworkGameManager: No runner spawn points assigned.");
            return false;
        }

        if (sabotagerSpawnPoints == null || sabotagerSpawnPoints.Length == 0)
        {
            Debug.LogError("NetworkGameManager: No sabotager spawn points assigned.");
            return false;
        }

        return true;
    }

    private PlayerRoleType GetRoleForClient(LobbySlotManager lobbyManager, ulong clientId)
    {
        int team = lobbyManager.GetTeamForClient(clientId);

        if (team == 1)
            return PlayerRoleType.Runner;

        if (team == 2)
            return PlayerRoleType.Sabotager;

        int slotIndex = lobbyManager.GetSlotIndexForClient(clientId);
        if (slotIndex >= 0)
            return slotIndex < 4 ? PlayerRoleType.Runner : PlayerRoleType.Sabotager;

        Debug.LogWarning($"NetworkGameManager: No team assignment found for client {clientId}. Defaulting to Runner.");
        return PlayerRoleType.Runner;
    }

    private void SpawnPlayerForClient(ulong clientId, PlayerRoleType role, Transform spawnPoint)
    {
        NetworkObject playerInstance = Instantiate(playerPrefab);

        playerInstance.transform.SetPositionAndRotation(
            spawnPoint.position,
            spawnPoint.rotation
        );

        playerInstance.SpawnAsPlayerObject(clientId, true);

        AssignRole(playerInstance, role);
        AssignRespawnPoint(playerInstance, role, spawnPoint);
        SyncOwnerSpawnPosition(playerInstance, spawnPoint);

        spawnedPlayers[clientId] = playerInstance;
    }

    private void AssignRole(NetworkObject playerInstance, PlayerRoleType role)
    {
        PlayerRole playerRole = playerInstance.GetComponent<PlayerRole>();
        if (playerRole == null)
        {
            Debug.LogError("NetworkGameManager: PlayerRole component not found on player prefab.");
            return;
        }

        playerRole.SetRoleServer(role);
    }

    private void AssignRespawnPoint(NetworkObject playerInstance, PlayerRoleType role, Transform spawnPoint)
    {
        if (role != PlayerRoleType.Runner)
            return;

        RespawnOnFall respawnOnFall = playerInstance.GetComponentInChildren<RespawnOnFall>(true);
        if (respawnOnFall == null)
        {
            Debug.LogWarning("NetworkGameManager: RespawnOnFall component not found on spawned runner.");
            return;
        }

        respawnOnFall.SetSpawnPoint(spawnPoint);
    }

    private void SyncOwnerSpawnPosition(NetworkObject playerInstance, Transform spawnPoint)
    {
        NetworkPlayerOwnership ownership = playerInstance.GetComponent<NetworkPlayerOwnership>();
        if (ownership == null)
        {
            Debug.LogError("NetworkGameManager: NetworkPlayerOwnership component not found on player prefab.");
            return;
        }

        ownership.SetSpawnPositionClientRpc(spawnPoint.position, spawnPoint.rotation);
    }
}