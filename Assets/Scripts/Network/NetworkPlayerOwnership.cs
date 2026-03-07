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
            
            // Ensure camera can see both Runner and Sabotager layers
            UpdateCameraCullingMask();
        }
        else
        {
            playerCamera.enabled = false;
            audioListener.enabled = false;
        }
    }
    
    public void UpdateCameraCullingMask()
    {
        if (playerCamera == null)
            return;
            
        int runnerLayer = LayerMask.NameToLayer("Runner");
        int sabotagerLayer = LayerMask.NameToLayer("Sabotager");
        
        if (runnerLayer != -1 && sabotagerLayer != -1)
        {
            // Explicitly set mask to include both Runner and Sabotager layers
            // Start with Everything (-1) and ensure both layers are included
            int currentMask = playerCamera.cullingMask;
            
            // Explicitly set mask to include both Runner and Sabotager layers
            // Don't rely on -1 (Everything) - explicitly include our layers
            int newMask = currentMask;
            
            // Always explicitly add both layers (even if mask is -1, this ensures they're included)
            newMask |= (1 << runnerLayer);
            newMask |= (1 << sabotagerLayer);
            
            playerCamera.cullingMask = newMask;
            
            // Verify the mask includes both layers
            bool hasRunner = (newMask & (1 << runnerLayer)) != 0 || newMask == -1;
            bool hasSabotager = (newMask & (1 << sabotagerLayer)) != 0 || newMask == -1;
            
            if (!hasRunner || !hasSabotager)
            {
                Debug.LogError($"NetworkPlayerOwnership: Camera culling mask update failed! Runner layer {runnerLayer} included: {hasRunner}, Sabotager layer {sabotagerLayer} included: {hasSabotager}, Mask: {newMask}");
            }
        }
        else
        {
            Debug.LogError($"NetworkPlayerOwnership: Runner or Sabotager layer not found! Runner={runnerLayer}, Sabotager={sabotagerLayer}. Make sure these layers exist in Project Settings > Tags and Layers.");
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