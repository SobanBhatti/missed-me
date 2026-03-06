using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2.5f;
    public Transform cameraRoot;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private bool cursorLocked = false;
    private Transform rootTransform; // Root NetworkPlayer transform
    private bool hasSpawned = false;
    private Vector3 expectedLocalPosition; // Expected local position of PlayerCapsule (should be constant)
    private bool movedThisFrame = false; // Track if we moved this frame

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        // Get the root NetworkPlayer transform (parent of PlayerCapsule)
        rootTransform = transform.parent;
        
        if (rootTransform == null)
        {
            Debug.LogError("PlayerMovement: PlayerCapsule must be a child of NetworkPlayer root!");
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        Debug.Log($"[CLIENT] PlayerMovement.OnNetworkSpawn - IsOwner: {IsOwner}, IsSpawned: {IsSpawned}");
        
        if (IsOwner)
        {
            // Wait a frame for NetworkTransform to sync initial position
            hasSpawned = false;
            
            // Use the prefab's expected local position (0, 1, 0) - don't read runtime value
            // This ensures consistency regardless of NetworkTransform sync timing
            expectedLocalPosition = new Vector3(0f, 1f, 0f);
            
            // Ensure PlayerCapsule is at correct local position relative to root
            // This fixes any issues from NetworkTransform syncing
            if (rootTransform != null)
            {
                Debug.Log($"[CLIENT] PlayerMovement.OnNetworkSpawn - Before setting local pos - Root position: {rootTransform.position}, Capsule world: {transform.position}, Capsule local: {transform.localPosition}");
                transform.localPosition = expectedLocalPosition;
                Debug.Log($"[CLIENT] PlayerMovement.OnNetworkSpawn - After setting local pos - Root position: {rootTransform.position}, Capsule world: {transform.position}, Capsule local: {transform.localPosition}");
                
                // CRITICAL: If root is at origin (0,0,0), NetworkTransform hasn't synced yet
                // This happens because owner clients don't receive position updates from server
                // We need to wait for NetworkTransform to initialize, or the server needs to ensure
                // position is synced before client spawns
                if (rootTransform.position.magnitude < 0.1f)
                {
                    Debug.LogWarning($"[CLIENT] PlayerMovement.OnNetworkSpawn: Root is at origin! NetworkTransform may not have synced. Position: {rootTransform.position}");
                }
            }
            else
            {
                Debug.LogError("[CLIENT] PlayerMovement.OnNetworkSpawn - rootTransform is NULL!");
            }
            
            LockCursor();
        }
    }

    private void OnEnable()
    {
        if (IsOwner)
        {
            LockCursor();
        }
    }

    private void OnDisable()
    {
        if (IsOwner)
        {
            UnlockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // Wait one frame after spawn to let NetworkTransform sync initial position
        if (!hasSpawned)
        {
            hasSpawned = true;
            // Ensure PlayerCapsule's local position is correct
            if (rootTransform != null)
            {
                transform.localPosition = expectedLocalPosition;
                
                // Debug: Log positions to verify spawn
                Debug.Log($"[CLIENT] PlayerMovement: First frame after spawn - Root position: {rootTransform.position}, Capsule world: {transform.position}, Capsule local: {transform.localPosition}");
                
                // CRITICAL FIX: If root is at origin (0,0,0), NetworkTransform didn't sync
                // This happens when client is owner - owner doesn't receive position from server
                // We need to get the spawn position from NetworkGameManager or use a different approach
                if (rootTransform.position.magnitude < 1f)
                {
                    Debug.LogError($"[CLIENT] PlayerMovement: Root is at origin ({rootTransform.position})! NetworkTransform failed to sync. This is why you spawn at wrong position.");
                    
                    // Try to get spawn position from NetworkGameManager
                    // Note: This is a workaround - ideally NetworkTransform should handle this
                    var gameManager = FindFirstObjectByType<NetworkGameManager>();
                    if (gameManager != null && IsOwner)
                    {
                        // Get the spawn point for this client
                        // This is a hack - we need a better way to get spawn position
                        Debug.LogWarning("PlayerMovement: Attempting to find spawn position from NetworkGameManager...");
                        // We can't easily get spawn position here without refactoring
                        // The real fix needs to be in NetworkGameManager or NetworkTransform setup
                    }
                }
            }
            return;
        }

        // Lock cursor if it's not locked (in case user clicks outside window)
        if (cursorLocked && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }

        // Toggle cursor lock with Escape key
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (cursorLocked)
            {
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }

        HandleMouseLook();
        HandleMovement();
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null || !cursorLocked)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        // Scale sensitivity - mouse delta is in pixels per frame
        // Multiply by 0.01f to convert to a reasonable sensitivity scale
        float sensitivity = mouseSensitivity * 0.01f;
        
        float mouseX = mouseDelta.x * sensitivity;
        float mouseY = mouseDelta.y * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        if (cameraRoot != null)
        {
            cameraRoot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // Rotate the root NetworkPlayer (not PlayerCapsule) so NetworkTransform syncs correctly
        if (rootTransform != null)
        {
            rootTransform.Rotate(Vector3.up * mouseX);
        }
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
        {
            movedThisFrame = false;
            return;
        }

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1;
        if (Keyboard.current.sKey.isPressed) input.y -= 1;
        if (Keyboard.current.aKey.isPressed) input.x -= 1;
        if (Keyboard.current.dKey.isPressed) input.x += 1;

        // Use root transform's forward/right for movement direction
        Vector3 moveDirection = Vector3.zero;
        if (rootTransform != null)
        {
            moveDirection = rootTransform.right * input.x + rootTransform.forward * input.y;
        }
        else
        {
            // Fallback to local transform if root not found
            moveDirection = transform.right * input.x + transform.forward * input.y;
        }

        float speed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

        Vector3 horizontalMove = moveDirection * speed * Time.deltaTime;
        controller.Move(horizontalMove);

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 verticalMove = velocity * Time.deltaTime;
        controller.Move(verticalMove);
        
        // Track if we actually moved this frame
        movedThisFrame = horizontalMove.magnitude > 0.001f || Mathf.Abs(verticalMove.y) > 0.001f;
        
        // Only sync root position if we actually moved
        if (movedThisFrame && rootTransform != null)
        {
            // Calculate where root should be to keep PlayerCapsule at expected local position
            Vector3 targetRootPosition = transform.position - expectedLocalPosition;
            
            // Update root position to match PlayerCapsule's movement
            rootTransform.position = targetRootPosition;
            
            // Ensure PlayerCapsule's local position stays correct
            transform.localPosition = expectedLocalPosition;
        }
    }
}