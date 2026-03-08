using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using Gameplay.GameModes;

public class RespawnOnFall : MonoBehaviour
{
    [Header("Spawn Point")]
    [SerializeField] private Transform spawnPoint;

    [SerializeField] private float killY = -10f;

    private CharacterController characterController;

    private NetworkObject rootNetworkObject;
    private NetworkTransform rootNetworkTransform;

    private Transform rootTransform;

    private Vector3 capsuleLocalPosition;
    private Quaternion capsuleLocalRotation;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        rootNetworkObject = GetComponentInParent<NetworkObject>();

        if (rootNetworkObject != null)
        {
            rootNetworkTransform = rootNetworkObject.GetComponent<NetworkTransform>();
            rootTransform = rootNetworkObject.transform;
        }

        capsuleLocalPosition = transform.localPosition;
        capsuleLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        if (rootNetworkObject != null && !rootNetworkObject.IsOwner)
            return;

        if (rootTransform.position.y < killY)
        {
            Respawn();
        }
    }

    private void Respawn()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("RespawnOnFall: Spawn point not assigned.");
            return;
        }

        Debug.Log("RespawnOnFall triggered");

        // Notify server that this runner has fallen
        var networkObject = GetComponentInParent<NetworkObject>();
        if (networkObject != null && NetworkManager.Singleton.IsServer)
        {
            var tracker = FindFirstObjectByType<RunnerRoundTracker>();

            if (tracker != null)
            {
                tracker.NotifyRunnerFellServerRpc(networkObject.OwnerClientId);
            }
        }

        if (characterController != null)
            characterController.enabled = false;

        if (rootNetworkTransform != null && rootNetworkObject.IsOwner)
        {
            rootNetworkTransform.Teleport(
                spawnPoint.position,
                spawnPoint.rotation,
                rootTransform.localScale
            );
        }
        else
        {
            rootTransform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation
            );
        }

        transform.localPosition = capsuleLocalPosition;
        transform.localRotation = capsuleLocalRotation;

        if (characterController != null)
            characterController.enabled = true;
    }

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }
}