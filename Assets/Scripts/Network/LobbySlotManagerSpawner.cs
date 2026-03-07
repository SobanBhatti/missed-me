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
            return;
        
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
        
        // Check if LobbySlotManager already exists and is spawned
        if (LobbySlotManager.Instance != null)
        {
            var existingNetworkObject = LobbySlotManager.Instance.GetComponent<NetworkObject>();
            if (existingNetworkObject != null && existingNetworkObject.IsSpawned)
            {
                yield break;
            }
            else if (existingNetworkObject != null && !existingNetworkObject.IsSpawned)
            {
                existingNetworkObject.Spawn();
                yield break;
            }
        }
        
        // Spawn from prefab
        if (lobbySlotManagerPrefab != null)
        {
            GameObject instance = Instantiate(lobbySlotManagerPrefab);
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }
        }
    }
}
