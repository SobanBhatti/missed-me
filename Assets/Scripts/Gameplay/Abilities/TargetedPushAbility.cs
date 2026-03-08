using UnityEngine;
using UnityEngine.InputSystem;

public class TargetedPushAbility : MonoBehaviour
{
    public float range = 40f;
    public float pushForce = 16f;
    public float upwardForce = 4f;
    public float cooldown = 2f;
    public LayerMask runnerLayer;

    private float _lastUseTime;
    private Camera _camera;

    void Awake()
    {
        // Camera is on MainCamera, which is a sibling (CameraRoot -> MainCamera)
        // PlayerCapsule and CameraRoot are both children of NetworkPlayer
        // So we need to search in the parent, not children
        Transform parent = transform.parent;
        if (parent != null)
        {
            _camera = parent.GetComponentInChildren<Camera>();
        }
        
        // Fallback: search in scene if not found
        if (_camera == null)
        {
            _camera = FindFirstObjectByType<Camera>();
        }
        
        if (_camera == null)
        {
            Debug.LogError("TargetedPushAbility: No camera found! Push ability will not work.");
        }
    }

    void Update()
    {
        if (Keyboard.current.qKey.wasPressedThisFrame && Time.time > _lastUseTime + cooldown)
        {
            TryPush();
            _lastUseTime = Time.time;
        }
    }

    void TryPush()
    {
        if (_camera == null)
        {
            Debug.LogError("TargetedPushAbility: Camera is null! Cannot perform push.");
            return;
        }
        
        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
        RaycastHit hit;

        if (Physics.SphereCast(ray, 0.75f, out hit, range, runnerLayer))
        {
            KnockbackReceiver receiver = hit.collider.GetComponentInParent<KnockbackReceiver>();

            if (receiver != null)
            {
                Vector3 direction = receiver.transform.position - _camera.transform.position;
                direction.y = 0f;
                direction.Normalize();

                Vector3 force = direction * pushForce + Vector3.up * upwardForce;

                receiver.ApplyKnockback(force);
            }
        }
    }
}