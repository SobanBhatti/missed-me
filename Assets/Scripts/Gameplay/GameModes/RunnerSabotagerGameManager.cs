using Gameplay.GameModes;
using Gameplay.Spawning;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunnerSabotagerGameManager : GameModeManager
{
    private readonly HashSet<ulong> clientsFinishedLoadingGameplay = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

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

        clientsFinishedLoadingGameplay.Clear();

        foreach (var clientId in clientsCompleted)
        {
            clientsFinishedLoadingGameplay.Add(clientId);
        }

        SpawnAllPlayers();
    }

    private void SpawnAllPlayers()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;

        foreach (var client in clients)
        {
            if (!clientsFinishedLoadingGameplay.Contains(client.ClientId))
            {
                Debug.LogWarning($"Client {client.ClientId} has not finished loading Gameplay. Skipping spawn.");
                continue;
            }

            if (spawnedPlayers.ContainsKey(client.ClientId))
                continue;

            SpawnPlayer(client.ClientId);
        }
    }

    protected override void SpawnPlayer(ulong clientId)
    {
        base.SpawnPlayer(clientId);

        NetworkObject playerInstance = spawnedPlayers[clientId];

        LobbySlotManager lobbyManager = LobbySlotManager.Instance;
        PlayerRoleType role = GetRoleForClient(lobbyManager, clientId);

        AssignRole(playerInstance, role);
        AssignRespawnPoint(playerInstance, role);
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

        Debug.LogWarning($"No team assignment found for client {clientId}. Defaulting to Runner.");
        return PlayerRoleType.Runner;
    }

    private void AssignRole(NetworkObject playerInstance, PlayerRoleType role)
    {
        PlayerRole playerRole = playerInstance.GetComponent<PlayerRole>();

        if (playerRole == null)
        {
            Debug.LogError("PlayerRole component not found on player prefab.");
            return;
        }

        playerRole.SetRoleServer(role);
    }

    private void AssignRespawnPoint(NetworkObject playerInstance, PlayerRoleType role)
    {
        if (role != PlayerRoleType.Runner)
            return;

        RespawnOnFall respawnOnFall = playerInstance.GetComponentInChildren<RespawnOnFall>(true);

        if (respawnOnFall == null)
        {
            Debug.LogWarning("RespawnOnFall component not found on spawned runner.");
            return;
        }

        Transform spawnPoint = GetSpawnPoint(playerInstance.OwnerClientId);
        respawnOnFall.SetSpawnPoint(spawnPoint);
    }

    protected override Transform GetSpawnPoint(ulong clientId)
    {
        LobbySlotManager lobbyManager = LobbySlotManager.Instance;
        PlayerRoleType role = GetRoleForClient(lobbyManager, clientId);

        SpawnPointType type =
            role == PlayerRoleType.Runner
            ? SpawnPointType.RunnerStart
            : SpawnPointType.SabotagerStart;

        var candidates = spawnPoints
            .Where(s => s.type == type)
            .OrderBy(s => s.index)
            .ToList();

        if (candidates.Count == 0)
            return base.GetSpawnPoint(clientId);

        var sameRoleClients = NetworkManager.Singleton.ConnectedClientsList
            .Where(c => GetRoleForClient(lobbyManager, c.ClientId) == role)
            .Select(c => c.ClientId)
            .ToList();

        int roleIndex = sameRoleClients.IndexOf(clientId);

        if (roleIndex < 0)
        {
            Debug.LogWarning($"RunnerSabotagerGameManager: Client {clientId} not found in same-role list. Using first spawn.");
            roleIndex = 0;
        }

        return candidates[roleIndex % candidates.Count].transform;
    }
}