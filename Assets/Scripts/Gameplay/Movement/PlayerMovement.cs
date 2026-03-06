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
        
        if (IsOwner)
        {
            // Wait a frame for NetworkTransform to sync initial position
            hasSpawned = false;
            
            // Use the prefab's expected local position
            // With CharacterController center at (0, 0, 0) and height 2,
            // CharacterController extends from y=-1 to y=1 relative to PlayerCapsule
            // To align CharacterController bottom with ground (y=0 world), PlayerCapsule must be at (0, 1, 0)
            expectedLocalPosition = new Vector3(0f, 1f, 0f);
            
            // Ensure CharacterController center matches mesh pivot (centered)
            if (controller != null)
            {
                controller.center = new Vector3(0f, 0f, 0f);
            }
            
            // Ensure PlayerCapsule is at correct local position relative to root
            // This fixes any issues from NetworkTransform syncing
            if (rootTransform != null)
            {
                transform.localPosition = expectedLocalPosition;
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
                
                // CRITICAL FIX: Sync CharacterController's internal position with transform
                // CharacterController.Move() can reset transform.position if internal position doesn't match
                if (controller != null)
                {
                    controller.enabled = false;
                    controller.transform.position = transform.position;
                    controller.enabled = true;
                    // Do a zero-move to update isGrounded state after re-enabling
                    // CharacterController needs Move() to be called to detect ground
                    controller.Move(Vector3.zero);
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

        // Check grounded state at START of frame (from previous frame's Move())
        // CharacterController.isGrounded updates after Move() is called
        bool isGrounded = controller.isGrounded;
        
        // Reset vertical velocity when grounded
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // Check for jump input
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;

        // Move horizontally
        Vector3 horizontalMove = moveDirection * speed * Time.deltaTime;
        controller.Move(horizontalMove);

        // Move vertically
        Vector3 verticalMove = velocity * Time.deltaTime;
        controller.Move(verticalMove);
        
        // isGrounded will be updated for next frame after Move() calls
        
        // Track if we actually moved this frame
        movedThisFrame = horizontalMove.magnitude > 0.001f || Mathf.Abs(verticalMove.y) > 0.001f;
        
        // Only sync root position if we actually moved AND capsule position is reasonable
        // CRITICAL: Don't update root if capsule position is clearly wrong (like 0,0,0 after spawn)
        if (movedThisFrame && rootTransform != null && transform.position.magnitude > 10f)
        {
            // Calculate where root should be to keep PlayerCapsule at expected local position
            Vector3 targetRootPosition = transform.position - expectedLocalPosition;
            
            // Update root position to match PlayerCapsule's movement
            rootTransform.position = targetRootPosition;
            
            // Ensure PlayerCapsule's local position stays correct
            transform.localPosition = expectedLocalPosition;
        }
        else if (movedThisFrame && rootTransform != null && transform.position.magnitude <= 10f)
        {
            // Capsule position is wrong - sync it back to correct position
            transform.position = rootTransform.position + expectedLocalPosition;
            
            // Sync CharacterController
            if (controller != null)
            {
                controller.enabled = false;
                controller.transform.position = transform.position;
                controller.enabled = true;
            }
        }
    }
}