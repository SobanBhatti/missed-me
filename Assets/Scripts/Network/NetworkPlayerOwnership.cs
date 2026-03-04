using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayerOwnership : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
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
    }
}