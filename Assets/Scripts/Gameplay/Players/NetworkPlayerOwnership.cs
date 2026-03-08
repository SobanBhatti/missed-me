using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerOwnership : NetworkBehaviour
{
    [Header("Owner Only Components")]
    [SerializeField] public Camera playerCamera;
    [SerializeField] public AudioListener audioListener;
    [SerializeField] private PlayerInput playerInput;

    public override void OnNetworkSpawn()
    {
        bool isOwner = IsOwner;

        if (playerCamera != null)
            playerCamera.enabled = isOwner;

        if (audioListener != null)
            audioListener.enabled = isOwner;

        if (playerInput != null)
            playerInput.enabled = isOwner;

        if (isOwner)
            UpdateCameraCullingMask();
    }

    public void UpdateCameraCullingMask()
    {
        if (playerCamera == null)
            return;

        int runnerLayer = LayerMask.NameToLayer("Runner");
        int sabotagerLayer = LayerMask.NameToLayer("Sabotager");

        if (runnerLayer == -1 || sabotagerLayer == -1)
        {
            Debug.LogError("NetworkPlayerOwnership: Missing Runner or Sabotager layer.");
            return;
        }

        int newMask = playerCamera.cullingMask;
        newMask |= (1 << runnerLayer);
        newMask |= (1 << sabotagerLayer);
        playerCamera.cullingMask = newMask;
    }

    [ClientRpc]
    public void SetSpawnPositionClientRpc(Vector3 position, Quaternion rotation)
    {
        if (!IsOwner)
            return;

        NetworkTransform networkTransform = GetComponent<NetworkTransform>();

        if (networkTransform != null)
        {
            networkTransform.Teleport(position, rotation, transform.localScale);
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}