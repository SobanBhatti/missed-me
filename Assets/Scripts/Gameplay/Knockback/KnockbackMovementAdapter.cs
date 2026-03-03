using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(KnockbackReceiver))]
public class KnockbackMovementAdapter : MonoBehaviour
{
    private CharacterController _controller;
    private KnockbackReceiver _knockback;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _knockback = GetComponent<KnockbackReceiver>();
    }

    void LateUpdate()
    {
        Vector3 knockback = _knockback.ConsumeKnockback();

        if (knockback.magnitude > 0.01f)
        {
            _controller.Move(knockback * Time.deltaTime);
        }
    }
}