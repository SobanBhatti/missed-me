using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawns LobbySlotManager as a NetworkObject when the server starts.
/// This is needed because scene-placed NetworkObjects with DontDestroyOnLoad
/// can't be automatically spawned on clients - they need to be registered as prefabs.
/// By spawning it from code, we ensure it's available to all clients.
/// </summary>
public class LobbySlotManagerSpawner : MonoBehaviour
{
    [Header("LobbySlotManager Prefab")]
    [SerializeField] private GameObject lobbySlotManagerPrefab;
    
    private void Start()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("LobbySlotManagerSpawner: NetworkManager not found!");
            return;
        }
        
        // Wait for NetworkManager to be ready, then spawn
        StartCoroutine(SpawnWhenReady());
    }
    
    private System.Collections.IEnumerator SpawnWhenReady()
    {
        // Wait until NetworkManager is initialized
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            yield return null;
        }
        
        // Additional small delay to ensure everything is ready
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log("LobbySlotManagerSpawner: Server ready, checking LobbySlotManager...");
        
        // Check if LobbySlotManager already exists and is spawned
        if (LobbySlotManager.Instance != null)
        {
            var existingNetworkObject = LobbySlotManager.Instance.GetComponent<NetworkObject>();
            if (existingNetworkObject != null && existingNetworkObject.IsSpawned)
            {
                Debug.Log("LobbySlotManagerSpawner: LobbySlotManager already exists and is spawned. NetworkObjectId=" + existingNetworkObject.NetworkObjectId);
                yield break;
            }
            else if (existingNetworkObject != null && !existingNetworkObject.IsSpawned)
            {
                Debug.Log("LobbySlotManagerSpawner: LobbySlotManager exists but not spawned. Spawning existing instance...");
                existingNetworkObject.Spawn();
                Debug.Log($"LobbySlotManagerSpawner: Spawned existing instance. NetworkObjectId={existingNetworkObject.NetworkObjectId}");
                yield break;
            }
        }
        
        // Spawn from prefab
        if (lobbySlotManagerPrefab != null)
        {
            Debug.Log("LobbySlotManagerSpawner: Spawning LobbySlotManager from prefab...");
            GameObject instance = Instantiate(lobbySlotManagerPrefab);
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
                Debug.Log($"LobbySlotManagerSpawner: Successfully spawned LobbySlotManager from prefab. NetworkObjectId={networkObject.NetworkObjectId}, Instance={LobbySlotManager.Instance != null}");
            }
            else
            {
                Debug.LogError("LobbySlotManagerSpawner: Prefab doesn't have NetworkObject component!");
            }
        }
        else
        {
            Debug.LogError("LobbySlotManagerSpawner: No prefab assigned! Please assign the LobbySlotManager prefab in the inspector.");
        }
    }
}
