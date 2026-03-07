using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LobbySlotManager : NetworkBehaviour
{
    public static LobbySlotManager Instance { get; private set; }

    [Header("Slot References")]
    [SerializeField] private LobbySlot[] team1Slots = new LobbySlot[4]; // Left side
    [SerializeField] private LobbySlot[] team2Slots = new LobbySlot[4]; // Right side

    private readonly Dictionary<ulong, int> clientIdToSlotIndex = new Dictionary<ulong, int>();

    private void Awake()
    {
        var networkObject = GetComponent<NetworkObject>();
        Debug.Log($"LobbySlotManager: Awake called. GameObject: {gameObject.name}, NetworkObject exists: {networkObject != null}, NetworkObjectId: {networkObject?.NetworkObjectId}, IsSpawned: {networkObject?.IsSpawned}");
        
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Debug.Log($"LobbySlotManager: Duplicate instance detected. Destroying. Existing instance: {Instance.gameObject.name}, New instance: {gameObject.name}");
            Destroy(gameObject);
            Debug.Log("LobbySlotManager: Destroying duplicate instance.");
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"LobbySlotManager: Instance set and DontDestroyOnLoad called. NetworkObject: {networkObject != null}, Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        
        // Find slots dynamically if arrays are null (happens after scene transitions)
        if (team1Slots == null || team2Slots == null || 
            team1Slots.Length == 0 || team2Slots.Length == 0 ||
            (team1Slots.Length > 0 && team1Slots[0] == null) || 
            (team2Slots.Length > 0 && team2Slots[0] == null))
        {
            Debug.Log("LobbySlotManager: Slot arrays are null or empty, attempting to find slots dynamically.");
            FindSlotsDynamically();
        }
    }
    
    private void FindSlotsDynamically()
    {
        Debug.Log($"LobbySlotManager: FindSlotsDynamically called. Current scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        // Find all LobbySlot components in the scene
        LobbySlot[] allSlots = FindObjectsByType<LobbySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"LobbySlotManager: Found {allSlots.Length} LobbySlot components in scene.");
        
        if (allSlots.Length > 0)
        {
            Debug.Log($"LobbySlotManager: First slot name: {allSlots[0].name}, Active: {allSlots[0].gameObject.activeInHierarchy}");
        }
        
        if (allSlots.Length >= 8)
        {
            // Sort by name to ensure consistent ordering (Slot0, Slot1, etc.)
            System.Array.Sort(allSlots, (a, b) => string.Compare(a.name, b.name));
            
            // Assign first 4 to team1, next 4 to team2
            team1Slots = new LobbySlot[4];
            team2Slots = new LobbySlot[4];
            
            for (int i = 0; i < 4; i++)
            {
                team1Slots[i] = allSlots[i];
                team2Slots[i] = allSlots[i + 4];
                Debug.Log($"LobbySlotManager: team1Slots[{i}] = {allSlots[i].name}, team2Slots[{i}] = {allSlots[i + 4].name}");
            }
            
            Debug.Log("LobbySlotManager: Dynamically assigned slots to teams.");
        }
        else
        {
            Debug.LogError($"LobbySlotManager: Expected 8 slots but found {allSlots.Length}. Make sure all slots are in the scene. Current scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }
    }

    private void Start()
    {
        Debug.Log($"LobbySlotManager: Start called. NetworkManager exists: {NetworkManager.Singleton != null}");
        
        var networkObject = GetComponent<NetworkObject>();
        Debug.Log($"LobbySlotManager: Start - NetworkObject exists: {networkObject != null}, IsSpawned: {networkObject?.IsSpawned}, Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        
        // Initialize slots when scene loads, even if NetworkObject isn't spawned yet
        // This ensures slots are assigned as soon as possible
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"LobbySlotManager: Start - IsServer=true, starting delayed initialization");
            // Delay one frame to ensure NetworkManager is fully initialized
            StartCoroutine(InitializeSlotsDelayed());
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // Client: Try to spawn NetworkObject if it's not spawned yet
            Debug.Log($"LobbySlotManager: Start - IsClient=true, checking if NetworkObject needs spawning");
            if (networkObject != null && !networkObject.IsSpawned)
            {
                Debug.Log("LobbySlotManager: Client NetworkObject not spawned. Waiting for server to spawn it or scene load to spawn it.");
                // The NetworkObject should spawn automatically when the scene loads via NetworkManager.SceneManager
                // But if it doesn't, we'll request a refresh when OnNetworkSpawn is called
                StartCoroutine(WaitForNetworkSpawn());
            }
        }
        else
        {
            Debug.Log($"LobbySlotManager: Start - Not server or NetworkManager not ready. IsServer={NetworkManager.Singleton?.IsServer}, IsClient={NetworkManager.Singleton?.IsClient}");
        }
    }
    
    private System.Collections.IEnumerator WaitForNetworkSpawn()
    {
        var networkObject = GetComponent<NetworkObject>();
        int maxWaitFrames = 60; // Wait up to 1 second (60 frames at 60fps)
        int frameCount = 0;
        
        while (networkObject != null && !networkObject.IsSpawned && frameCount < maxWaitFrames)
        {
            yield return null;
            frameCount++;
        }
        
        if (networkObject != null && networkObject.IsSpawned)
        {
            Debug.Log($"LobbySlotManager: Client NetworkObject spawned after {frameCount} frames. Requesting slot refresh.");
            RequestSlotRefreshServerRpc();
        }
        else
        {
            Debug.LogError($"LobbySlotManager: Client NetworkObject never spawned after {frameCount} frames! This is the root cause of the issue.");
        }
    }

    private System.Collections.IEnumerator InitializeSlotsDelayed()
    {
        yield return null; // Wait one frame
        
        Debug.Log($"LobbySlotManager: InitializeSlotsDelayed - Checking if still server. IsServer={NetworkManager.Singleton?.IsServer}, IsSpawned={IsSpawned}");
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // Subscribe to client connection/disconnection events
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // CRITICAL FIX: Scene-placed NetworkObjects with DontDestroyOnLoad need to be manually spawned
            // Unity Netcode can't automatically spawn them on clients because they're not registered as prefabs
            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                Debug.Log("LobbySlotManager: Server NetworkObject not spawned, spawning now (this will sync to all clients)...");
                networkObject.Spawn();
                Debug.Log($"LobbySlotManager: Server NetworkObject spawned. IsSpawned={networkObject.IsSpawned}, NetworkObjectId={networkObject.NetworkObjectId}");
            }
            else if (networkObject != null && networkObject.IsSpawned)
            {
                Debug.Log($"LobbySlotManager: Server NetworkObject already spawned. NetworkObjectId={networkObject.NetworkObjectId}");
            }
            
            // Always refresh slots - UpdateSlotsDirectly will handle UI if not spawned
            RefreshAllSlots();
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"LobbySlotManager: OnNetworkSpawn called. IsServer={IsServer}, IsClient={IsClient}, IsSpawned={IsSpawned}, Dictionary count={clientIdToSlotIndex.Count}");
        
        // Find slots on all clients (not just server) since serialized references may be null
        if (team1Slots == null || team2Slots == null || 
            team1Slots.Length == 0 || team2Slots.Length == 0 ||
            (team1Slots.Length > 0 && team1Slots[0] == null) || 
            (team2Slots.Length > 0 && team2Slots[0] == null))
        {
            Debug.Log("LobbySlotManager: OnNetworkSpawn - Finding slots dynamically.");
            FindSlotsDynamically();
        }
        
        if (IsServer)
        {
            // Subscribe to client connection/disconnection events (in case Start() didn't run)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            
            // If dictionary is empty (scene transition), rebuild it from connected clients
            // This ensures slot assignments persist across scene transitions
            if (clientIdToSlotIndex.Count == 0)
            {
                Debug.Log("LobbySlotManager: Dictionary empty after scene transition, rebuilding from connected clients");
            }
            
            // Update all slots for all clients - this will assign everyone including host
            RefreshAllSlots();
        }
        else if (IsClient)
        {
            // Client just spawned - request current slot state from server
            Debug.Log($"[CLIENT] LobbySlotManager: Client NetworkObject spawned, requesting slot refresh from server. OwnerClientId={OwnerClientId}, NetworkObjectId={NetworkObjectId}");
            try
            {
                RequestSlotRefreshServerRpc();
                Debug.Log($"[CLIENT] LobbySlotManager: RequestSlotRefreshServerRpc call completed");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CLIENT] LobbySlotManager: Failed to call RequestSlotRefreshServerRpc: {e.Message}\n{e.StackTrace}");
            }
        }
        else
        {
            Debug.LogWarning($"LobbySlotManager: OnNetworkSpawn - Neither server nor client! IsServer={IsServer}, IsClient={IsClient}");
        }
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSlotRefreshServerRpc()
    {
        Debug.Log($"[RPC] LobbySlotManager: RequestSlotRefreshServerRpc called. IsServer={IsServer}, OwnerClientId={OwnerClientId}");
        if (!IsServer)
        {
            Debug.LogWarning($"[RPC] LobbySlotManager: RequestSlotRefreshServerRpc called but not server!");
            return;
        }
            
        Debug.Log($"[RPC] LobbySlotManager: Server received slot refresh request from client {OwnerClientId}");
        // Send current slot state to all clients (including the one that just requested)
        RefreshAllSlots();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer)
            return;

        // Find the first available slot
        int slotIndex = FindFirstAvailableSlot();
        if (slotIndex >= 0)
        {
            AssignPlayerToSlot(clientId, slotIndex);
        }
        else
        {
            Debug.LogWarning($"LobbySlotManager: No available slots for client {clientId}");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        // Remove player from slot
        if (clientIdToSlotIndex.TryGetValue(clientId, out int slotIndex))
        {
            clientIdToSlotIndex.Remove(clientId);
            
            // Refresh all slots for all clients
            RefreshAllSlots();
        }
    }

    private int FindFirstAvailableSlot()
    {
        // Assign slots in alternating pattern: 0, 4, 1, 5, 2, 6, 3, 7
        // This alternates between team 1 (0-3) and team 2 (4-7)
        int[] slotOrder = { 0, 4, 1, 5, 2, 6, 3, 7 };
        
        foreach (int slotIndex in slotOrder)
        {
            if (!clientIdToSlotIndex.ContainsValue(slotIndex))
            {
                return slotIndex;
            }
        }
        return -1; // No available slot
    }

    private void AssignPlayerToSlot(ulong clientId, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 8)
        {
            Debug.LogError($"LobbySlotManager: Invalid slot index {slotIndex}");
            return;
        }

        clientIdToSlotIndex[clientId] = slotIndex;
        RefreshAllSlots();
    }

    private void ClearSlot(int slotIndex)
    {
        LobbySlot slot = GetSlotByIndex(slotIndex);
        if (slot != null)
        {
            slot.SetPlayer(null, false);
        }
    }

    [ClientRpc]
    private void RefreshAllSlotsClientRpc(FixedString64Bytes[] playerNames, bool[] isHostFlags)
    {
        Debug.Log($"[CLIENT RPC] LobbySlotManager: RefreshAllSlotsClientRpc received. IsServer={IsServer}, IsClient={IsClient}, IsSpawned={IsSpawned}");
        Debug.Log($"[CLIENT RPC] LobbySlotManager: playerNames length: {playerNames?.Length}, isHostFlags length: {isHostFlags?.Length}");
        
        // Always try to find slots dynamically on clients (serialized references may be null)
        // Re-find slots if arrays are null or empty
        if (team1Slots == null || team2Slots == null || 
            team1Slots.Length == 0 || team2Slots.Length == 0 ||
            (team1Slots.Length > 0 && team1Slots[0] == null) || 
            (team2Slots.Length > 0 && team2Slots[0] == null))
        {
            Debug.Log("LobbySlotManager: Slot arrays are null or invalid in ClientRpc, finding slots dynamically.");
            FindSlotsDynamically();
        }
        
        // Update all slots on all clients
        for (int i = 0; i < 8; i++)
        {
            LobbySlot slot = GetSlotByIndex(i);
            if (slot != null)
            {
                if (i < playerNames.Length && !string.IsNullOrEmpty(playerNames[i].ToString()))
                {
                    string playerName = playerNames[i].ToString();
                    bool isHost = i < isHostFlags.Length && isHostFlags[i];
                    Debug.Log($"LobbySlotManager: [Client] Setting slot {i} to player '{playerName}', isHost={isHost}");
                    slot.SetPlayer(playerName, isHost);
                }
                else
                {
                    Debug.Log($"LobbySlotManager: [Client] Clearing slot {i}");
                    slot.SetPlayer(null, false);
                }
            }
            else
            {
                Debug.LogWarning($"LobbySlotManager: [Client] Slot {i} is null! team1Slots length: {team1Slots?.Length}, team2Slots length: {team2Slots?.Length}");
            }
        }
    }

    private void RefreshAllSlots()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
        {
            Debug.Log($"LobbySlotManager: RefreshAllSlots called but not server. NetworkManager exists: {networkManager != null}, IsServer={networkManager?.IsServer}, IsSpawned={IsSpawned}");
            return;
        }

        // Get all connected clients
        var connectedClients = NetworkManager.Singleton.ConnectedClientsList;
        ulong hostClientId = NetworkManager.Singleton.LocalClientId;
        
        Debug.Log($"LobbySlotManager: RefreshAllSlots - Connected clients: {connectedClients.Count}, Host ID: {hostClientId}");
        
        // Build arrays for slot data
        FixedString64Bytes[] playerNames = new FixedString64Bytes[8];
        bool[] isHostFlags = new bool[8];
        
        // Clear existing assignments
        clientIdToSlotIndex.Clear();
        
        // Assign players to slots in alternating pattern: 0, 4, 1, 5, 2, 6, 3, 7
        // This alternates between team 1 (0-3) and team 2 (4-7)
        int[] slotOrder = { 0, 4, 1, 5, 2, 6, 3, 7 };
        int clientIndex = 0;
        
        foreach (var client in connectedClients)
        {
            if (clientIndex >= slotOrder.Length)
                break;
                
            int slotIndex = slotOrder[clientIndex];
            bool isHost = client.ClientId == hostClientId;
            string playerName = GetPlayerName(client.ClientId);
            
            playerNames[slotIndex] = new FixedString64Bytes(playerName);
            isHostFlags[slotIndex] = isHost;
            
            // Update the mapping
            clientIdToSlotIndex[client.ClientId] = slotIndex;
            Debug.Log($"LobbySlotManager: Assigned client {client.ClientId} ({playerName}) to slot {slotIndex}, isHost={isHost}");
            clientIndex++;
        }
        
        Debug.Log($"LobbySlotManager: Dictionary now has {clientIdToSlotIndex.Count} entries");
        
        // If NetworkObject is spawned, use ClientRpc to update all clients
        // Otherwise, update UI directly (for server/host before spawn)
        Debug.Log($"LobbySlotManager: RefreshAllSlots - IsSpawned={IsSpawned}, NetworkObject={GetComponent<NetworkObject>()?.IsSpawned}");
        if (IsSpawned)
        {
            Debug.Log($"LobbySlotManager: RefreshAllSlots - Calling RefreshAllSlotsClientRpc. Connected clients: {NetworkManager.Singleton.ConnectedClientsList.Count}");
            RefreshAllSlotsClientRpc(playerNames, isHostFlags);
            Debug.Log($"LobbySlotManager: RefreshAllSlots - RefreshAllSlotsClientRpc call completed");
        }
        else
        {
            // NetworkObject not spawned yet - update UI directly on server/host
            Debug.Log("LobbySlotManager: NetworkObject not spawned, updating UI directly on server/host");
            UpdateSlotsDirectly(playerNames, isHostFlags);
        }
    }
    
    private void UpdateSlotsDirectly(FixedString64Bytes[] playerNames, bool[] isHostFlags)
    {
        // Re-find slots if arrays are null (scene might have reloaded)
        if (team1Slots == null || team2Slots == null || 
            team1Slots.Length == 0 || team2Slots.Length == 0 ||
            (team1Slots.Length > 0 && team1Slots[0] == null) || 
            (team2Slots.Length > 0 && team2Slots[0] == null))
        {
            Debug.Log("LobbySlotManager: Slot arrays are null in UpdateSlotsDirectly, finding slots dynamically.");
            FindSlotsDynamically();
        }
        
        // Update all slots directly
        for (int i = 0; i < 8; i++)
        {
            LobbySlot slot = GetSlotByIndex(i);
            if (slot != null)
            {
                if (i < playerNames.Length && !string.IsNullOrEmpty(playerNames[i].ToString()))
                {
                    string playerName = playerNames[i].ToString();
                    bool isHost = i < isHostFlags.Length && isHostFlags[i];
                    Debug.Log($"LobbySlotManager: Setting slot {i} to player '{playerName}', isHost={isHost}");
                    slot.SetPlayer(playerName, isHost);
                }
                else
                {
                    Debug.Log($"LobbySlotManager: Clearing slot {i}");
                    slot.SetPlayer(null, false);
                }
            }
            else
            {
                Debug.LogWarning($"LobbySlotManager: Slot {i} is null! team1Slots length: {team1Slots?.Length}, team2Slots length: {team2Slots?.Length}");
            }
        }
    }

    private LobbySlot GetSlotByIndex(int index)
    {
        if (index < 0 || index >= 8)
            return null;
            
        if (index < 4)
        {
            // Team 1 (left side)
            if (team1Slots == null || index >= team1Slots.Length)
            {
                Debug.LogWarning($"LobbySlotManager: team1Slots is null or index {index} out of range. Array length: {team1Slots?.Length}");
                return null;
            }
            return team1Slots[index];
        }
        else
        {
            // Team 2 (right side)
            int team2Index = index - 4;
            if (team2Slots == null || team2Index >= team2Slots.Length)
            {
                Debug.LogWarning($"LobbySlotManager: team2Slots is null or index {team2Index} out of range. Array length: {team2Slots?.Length}");
                return null;
            }
            return team2Slots[team2Index];
        }
    }

    private string GetPlayerName(ulong clientId)
    {
        // Try to get player name from Unity Services Authentication
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            try
            {
                string playerId = Unity.Services.Authentication.AuthenticationService.Instance?.PlayerId;
                if (!string.IsNullOrEmpty(playerId))
                {
                    return $"Player {playerId.Substring(0, Mathf.Min(8, playerId.Length))}";
                }
            }
            catch { }
        }
        
        // Fallback to client ID
        return $"Player {clientId}";
    }

    // Public method to get team assignment for a client
    // Returns: 1 for Team 1 (slots 0-3), 2 for Team 2 (slots 4-7), 0 if not found
    public int GetTeamForClient(ulong clientId)
    {
        if (clientIdToSlotIndex.TryGetValue(clientId, out int slotIndex))
        {
            // Team 1: slots 0-3, Team 2: slots 4-7
            return slotIndex < 4 ? 1 : 2;
        }
        return 0; // Not found
    }

    // Public method to get slot index for a client
    public int GetSlotIndexForClient(ulong clientId)
    {
        if (clientIdToSlotIndex.TryGetValue(clientId, out int slotIndex))
        {
            return slotIndex;
        }
        return -1; // Not found
    }
}
