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
    private readonly NetworkVariable<PlayerRoleType> currentRole = new(
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

        currentRole.OnValueChanged += OnRoleChanged;

        ApplyRole(currentRole.Value);

        if (IsOwner)
            UpdateOwnerCamera();
    }

    public override void OnNetworkDespawn()
    {
        currentRole.OnValueChanged -= OnRoleChanged;
        base.OnNetworkDespawn();
    }

    public void SetRoleServer(PlayerRoleType role)
    {
        if (!IsServer)
        {
            Debug.LogError("PlayerRole: SetRoleServer called on non-server.");
            return;
        }

        currentRole.Value = role;
    }

    private void OnRoleChanged(PlayerRoleType oldRole, PlayerRoleType newRole)
    {
        ApplyRole(newRole);
        UpdateOwnerCamera();
    }

    private void ApplyRole(PlayerRoleType role)
    {
        if (respawnOnFall != null)
            respawnOnFall.enabled = role == PlayerRoleType.Runner;

        if (targetedPushAbility != null)
            targetedPushAbility.enabled = role == PlayerRoleType.Sabotager;

        if (role == PlayerRoleType.None)
            return;

        int runnerLayer = LayerMask.NameToLayer("Runner");
        int sabotagerLayer = LayerMask.NameToLayer("Sabotager");

        if (runnerLayer == -1 || sabotagerLayer == -1)
        {
            Debug.LogError("PlayerRole: Runner or Sabotager layer missing.");
            return;
        }

        int targetLayer = role == PlayerRoleType.Runner ? runnerLayer : sabotagerLayer;

        SetLayerRecursive(gameObject, targetLayer);
    }

    private void UpdateOwnerCamera()
    {
        if (NetworkManager.Singleton == null)
            return;

        foreach (var networkObject in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject == null)
                continue;

            var ownership = networkObject.GetComponent<NetworkPlayerOwnership>();

            if (ownership != null && ownership.IsOwner)
                ownership.UpdateCameraCullingMask();
        }
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}