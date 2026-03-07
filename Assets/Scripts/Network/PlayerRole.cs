using Unity.Netcode;
using UnityEngine;

public enum PlayerRoleType
{
    None,
    Runner,
    Sabotager
}

public class PlayerRole : NetworkBehaviour
{
    private NetworkVariable<PlayerRoleType> currentRole = new NetworkVariable<PlayerRoleType>(
        PlayerRoleType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Role Components")]
    [SerializeField] private RespawnOnFall respawnOnFall;
    [SerializeField] private TargetedPushAbility targetedPushAbility;

    public PlayerRoleType CurrentRole => currentRole.Value;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to role changes
        currentRole.OnValueChanged += OnRoleChanged;
        
        // Apply initial role if already set (this ensures layer is set immediately on spawn)
        if (currentRole.Value != PlayerRoleType.None)
        {
            ApplyRole(currentRole.Value);
            // Update all cameras when a player spawns with a role already assigned
            UpdateAllPlayerCameras();
        }
    }

    public override void OnNetworkDespawn()
    {
        currentRole.OnValueChanged -= OnRoleChanged;
        base.OnNetworkDespawn();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetRoleServerRpc(PlayerRoleType role)
    {
        if (!IsServer)
            return;

        currentRole.Value = role;
    }

    private void OnRoleChanged(PlayerRoleType oldRole, PlayerRoleType newRole)
    {
        ApplyRole(newRole);
        // When any player's role changes, update all cameras to ensure visibility
        UpdateAllPlayerCameras();
    }

    private void ApplyRole(PlayerRoleType role)
    {
        // Enable/disable components based on role
        if (respawnOnFall != null)
        {
            respawnOnFall.enabled = (role == PlayerRoleType.Runner);
        }

        if (targetedPushAbility != null)
        {
            targetedPushAbility.enabled = (role == PlayerRoleType.Sabotager);
        }
        
        // Set layer based on role for visibility
        int runnerLayer = LayerMask.NameToLayer("Runner");
        int sabotagerLayer = LayerMask.NameToLayer("Sabotager");
        
        if (runnerLayer == -1 || sabotagerLayer == -1)
        {
            Debug.LogError($"PlayerRole: Runner or Sabotager layer not found! Runner={runnerLayer}, Sabotager={sabotagerLayer}. Make sure these layers exist in Project Settings > Tags and Layers.");
            return;
        }
        
        // Set layer on root NetworkPlayer and all children
        // IMPORTANT: Layers are local to each client, so ALL clients must set layers
        // This runs on all clients when the NetworkVariable changes
        int targetLayer = role == PlayerRoleType.Runner ? runnerLayer : sabotagerLayer;
        SetLayerRecursive(gameObject, targetLayer);
    }
    
    /// <summary>
    /// Updates all player cameras to ensure they can see both Runner and Sabotager layers.
    /// This is called when any role changes to ensure visibility is maintained.
    /// Uses a coroutine with a small delay to ensure all players are fully spawned.
    /// </summary>
    private void UpdateAllPlayerCameras()
    {
        // Use coroutine to ensure all players are fully spawned before updating cameras
        StartCoroutine(UpdateAllPlayerCamerasDelayed());
    }
    
    private System.Collections.IEnumerator UpdateAllPlayerCamerasDelayed()
    {
        // Wait a frame to ensure all NetworkObjects are fully spawned
        yield return null;
        
        if (NetworkManager.Singleton == null)
            yield break;
        
        // Iterate through all spawned NetworkObjects to find players
        // This is more reliable than FindObjectsByType for networked objects
        var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjectsList;
        
        foreach (var networkObject in spawnedObjects)
        {
            if (networkObject == null)
                continue;
            
            // Check if this is a player object
            var ownership = networkObject.GetComponent<NetworkPlayerOwnership>();
            if (ownership != null && ownership.IsOwner && ownership.playerCamera != null)
            {
                ownership.UpdateCameraCullingMask();
            }
        }
    }
    
    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
