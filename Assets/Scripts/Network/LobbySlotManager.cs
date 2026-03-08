using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbySlotManager : NetworkBehaviour
{
    public static LobbySlotManager Instance { get; private set; }

    [Header("Slot References")]
    [SerializeField] private LobbySlot[] team1Slots = new LobbySlot[4];
    [SerializeField] private LobbySlot[] team2Slots = new LobbySlot[4];

    [Header("Networking")]
    [SerializeField] private int maxClientSpawnWaitFrames = 60;

    private readonly Dictionary<ulong, int> clientIdToSlotIndex = new();
    private bool serverCallbacksSubscribed;

    private static readonly int[] SlotOrder = { 0, 4, 1, 5, 2, 6, 3, 7 };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(this);

        EnsureSlotReferences();
    }

    private void Start()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(InitializeServerRoutine());
        }
        else if (NetworkManager.Singleton.IsClient && networkObject != null && !networkObject.IsSpawned)
        {
            StartCoroutine(WaitForNetworkSpawnRoutine());
        }
    }

    public override void OnNetworkSpawn()
    {
        EnsureSlotReferences();

        if (IsServer)
        {
            SubscribeServerCallbacks();
            AssignExistingClientsToSlots();
            RefreshAllSlots();
        }
        else if (IsClient)
        {
            try
            {
                RequestSlotRefreshServerRpc();
            }
            catch (Exception e)
            {
                Debug.LogError($"LobbySlotManager: Failed to request slot refresh. {e.Message}\n{e.StackTrace}");
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            UnsubscribeServerCallbacks();
        }
    }

    private IEnumerator InitializeServerRoutine()
    {
        yield return null;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            yield break;

        SubscribeServerCallbacks();

        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
        {
            networkObject.Spawn();
        }

        AssignExistingClientsToSlots();
        RefreshAllSlots();
    }

    private IEnumerator WaitForNetworkSpawnRoutine()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        int waitedFrames = 0;

        while (networkObject != null && !networkObject.IsSpawned && waitedFrames < maxClientSpawnWaitFrames)
        {
            waitedFrames++;
            yield return null;
        }

        if (networkObject != null && networkObject.IsSpawned)
        {
            RequestSlotRefreshServerRpc();
        }
    }

    private void SubscribeServerCallbacks()
    {
        if (serverCallbacksSubscribed || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        serverCallbacksSubscribed = true;
    }

    private void UnsubscribeServerCallbacks()
    {
        if (!serverCallbacksSubscribed || NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        serverCallbacksSubscribed = false;
    }

    private void EnsureSlotReferences()
    {
        if (team1Slots == null || team2Slots == null ||
            team1Slots.Length != 4 || team2Slots.Length != 4 ||
            team1Slots[0] == null || team2Slots[0] == null)
        {
            FindSlotsDynamically();
        }
    }

    private void FindSlotsDynamically()
    {
        LobbySlot[] allSlots = FindObjectsByType<LobbySlot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (allSlots.Length < 8)
        {
            Debug.LogError(
                $"LobbySlotManager: Expected 8 LobbySlot objects but found {allSlots.Length}. " +
                $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}"
            );
            return;
        }

        Array.Sort(allSlots, (a, b) => string.CompareOrdinal(a.name, b.name));

        team1Slots = new LobbySlot[4];
        team2Slots = new LobbySlot[4];

        for (int i = 0; i < 4; i++)
        {
            team1Slots[i] = allSlots[i];
            team2Slots[i] = allSlots[i + 4];
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSlotRefreshServerRpc()
    {
        if (!IsServer)
            return;

        RefreshAllSlots();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        if (clientIdToSlotIndex.ContainsKey(clientId))
        {
            RefreshAllSlots();
            return;
        }

        int slotIndex = FindFirstAvailableSlot();
        if (slotIndex < 0)
        {
            Debug.LogWarning($"LobbySlotManager: No available slot for client {clientId}.");
            return;
        }

        clientIdToSlotIndex[clientId] = slotIndex;
        RefreshAllSlots();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        if (clientIdToSlotIndex.Remove(clientId))
        {
            RefreshAllSlots();
        }
    }

    private void AssignExistingClientsToSlots()
    {
        if (!IsServer || NetworkManager.Singleton == null)
            return;

        foreach (NetworkClient client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (clientIdToSlotIndex.ContainsKey(client.ClientId))
                continue;

            int slotIndex = FindFirstAvailableSlot();
            if (slotIndex >= 0)
            {
                clientIdToSlotIndex[client.ClientId] = slotIndex;
            }
        }
    }

    private int FindFirstAvailableSlot()
    {
        foreach (int slotIndex in SlotOrder)
        {
            if (!clientIdToSlotIndex.ContainsValue(slotIndex))
            {
                return slotIndex;
            }
        }

        return -1;
    }

    private void RefreshAllSlots()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
            return;

        EnsureSlotReferences();

        FixedString64Bytes[] playerNames = new FixedString64Bytes[8];
        bool[] isHostFlags = new bool[8];

        ulong hostClientId = networkManager.LocalClientId;

        foreach (KeyValuePair<ulong, int> pair in clientIdToSlotIndex)
        {
            ulong clientId = pair.Key;
            int slotIndex = pair.Value;

            if (slotIndex < 0 || slotIndex >= 8)
                continue;

            if (!networkManager.ConnectedClients.ContainsKey(clientId))
                continue;

            playerNames[slotIndex] = new FixedString64Bytes(GetPlayerName(clientId));
            isHostFlags[slotIndex] = clientId == hostClientId;
        }

        if (IsSpawned)
        {
            RefreshAllSlotsClientRpc(playerNames, isHostFlags);
        }
        else
        {
            UpdateSlotsDirectly(playerNames, isHostFlags);
        }
    }

    [ClientRpc]
    private void RefreshAllSlotsClientRpc(FixedString64Bytes[] playerNames, bool[] isHostFlags)
    {
        EnsureSlotReferences();
        UpdateSlotsDirectly(playerNames, isHostFlags);
    }

    private void UpdateSlotsDirectly(FixedString64Bytes[] playerNames, bool[] isHostFlags)
    {
        EnsureSlotReferences();

        for (int i = 0; i < 8; i++)
        {
            LobbySlot slot = GetSlotByIndex(i);
            if (slot == null)
                continue;

            if (i < playerNames.Length && !string.IsNullOrEmpty(playerNames[i].ToString()))
            {
                string playerName = playerNames[i].ToString();
                bool isHost = i < isHostFlags.Length && isHostFlags[i];
                slot.SetPlayer(playerName, isHost);
            }
            else
            {
                slot.SetPlayer(null, false);
            }
        }
    }

    private LobbySlot GetSlotByIndex(int index)
    {
        if (index < 0 || index >= 8)
            return null;

        if (index < 4)
        {
            if (team1Slots == null || index >= team1Slots.Length)
                return null;

            return team1Slots[index];
        }

        int team2Index = index - 4;

        if (team2Slots == null || team2Index >= team2Slots.Length)
            return null;

        return team2Slots[team2Index];
    }

    private string GetPlayerName(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            try
            {
                string playerId = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerId;
                if (!string.IsNullOrEmpty(playerId))
                {
                    return $"Player {playerId.Substring(0, Mathf.Min(8, playerId.Length))}";
                }
            }
            catch
            {
            }
        }

        return $"Player {clientId}";
    }

    public int GetTeamForClient(ulong clientId)
    {
        if (clientIdToSlotIndex.TryGetValue(clientId, out int slotIndex))
        {
            return slotIndex < 4 ? 1 : 2;
        }

        return 0;
    }

    public int GetSlotIndexForClient(ulong clientId)
    {
        if (clientIdToSlotIndex.TryGetValue(clientId, out int slotIndex))
        {
            return slotIndex;
        }

        return -1;
    }
}