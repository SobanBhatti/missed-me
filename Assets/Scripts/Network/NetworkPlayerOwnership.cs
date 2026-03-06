using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerOwnership : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener audioListener;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            playerCamera.enabled = true;
            audioListener.enabled = true;
        }
        else
        {
            playerCamera.enabled = false;
            audioListener.enabled = false;
        }
    }

    // ClientRpc to set spawn position on owner client
    // This is needed because owner clients don't receive position updates from NetworkTransform
    [ClientRpc]
    public void SetSpawnPositionClientRpc(Vector3 position, Quaternion rotation)
    {
        if (IsOwner)
        {
            // Get NetworkTransform and teleport to position
            var networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            
            if (networkTransform != null)
            {
                networkTransform.Teleport(position, rotation, transform.localScale);
            }
            else
            {
                // Fallback if NetworkTransform not found
                transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}