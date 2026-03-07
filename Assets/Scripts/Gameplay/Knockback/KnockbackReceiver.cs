using Unity.Netcode;
using UnityEngine;

public class KnockbackReceiver : NetworkBehaviour
{
    public float drag = 8f;

    private Vector3 _knockbackVelocity;

    /// <summary>
    /// Called by TargetedPushAbility to apply knockback over the network.
    /// This ensures knockback is applied on the owner client where movement happens.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ApplyKnockbackServerRpc(Vector3 force)
    {
        if (!IsServer)
            return;

        // Apply knockback on the owner client (where CharacterController movement happens)
        ApplyKnockbackClientRpc(force);
    }

    /// <summary>
    /// Applies knockback on the owner client. This is called via ClientRpc from the server.
    /// </summary>
    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector3 force)
    {
        // Only apply on owner client (where movement is authoritative)
        if (IsOwner)
        {
            _knockbackVelocity = force;
        }
    }

    /// <summary>
    /// Direct local application (for backwards compatibility or local-only effects).
    /// For network-synced knockback, use ApplyKnockbackServerRpc instead.
    /// </summary>
    public void ApplyKnockback(Vector3 force)
    {
        // If this is a NetworkBehaviour and we're networked, use the RPC
        if (IsSpawned)
        {
            ApplyKnockbackServerRpc(force);
        }
        else
        {
            // Fallback for non-networked or local-only scenarios
            _knockbackVelocity = force;
        }
    }

    public Vector3 ConsumeKnockback()
    {
        Vector3 current = _knockbackVelocity;

        _knockbackVelocity *= Mathf.Exp(-drag * Time.deltaTime);

        // Hard stop when small enough
        if (_knockbackVelocity.magnitude < 0.2f)
            _knockbackVelocity = Vector3.zero;

        return current;
    }
}