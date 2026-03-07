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
        
        // Apply initial role if already set
        if (currentRole.Value != PlayerRoleType.None)
        {
            ApplyRole(currentRole.Value);
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

        Debug.Log($"PlayerRole: Applied role {role} for client {OwnerClientId}");
    }
}
