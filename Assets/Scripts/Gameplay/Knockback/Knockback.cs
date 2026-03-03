using UnityEngine;
using UnityEngine.InputSystem;

public class Knockback : MonoBehaviour
{
    public float pushForce = 12f;
    public float upwardForce = 4f;
    public float cooldown = 2f;

    private float _lastPushTime;
    private KnockbackReceiver _receiver;

    void Awake()
    {
        _receiver = GetComponent<KnockbackReceiver>();
    }

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && Time.time > _lastPushTime + cooldown)
        {
            Vector3 force = -transform.right * pushForce + Vector3.up * upwardForce;
            _receiver.ApplyKnockback(force);

            _lastPushTime = Time.time;
        }
    }
}