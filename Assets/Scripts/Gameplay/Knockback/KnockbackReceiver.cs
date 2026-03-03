using UnityEngine;

public class KnockbackReceiver : MonoBehaviour
{
    public float drag = 8f;

    private Vector3 _knockbackVelocity;

    public void ApplyKnockback(Vector3 force)
    {
        _knockbackVelocity = force;
    }

    public Vector3 ConsumeKnockback()
    {
        Vector3 current = _knockbackVelocity;

        _knockbackVelocity *= Mathf.Exp(-drag * Time.deltaTime);

        // Hard stop when small enough
        if (_knockbackVelocity.magnitude < 0.2f)
            _knockbackVelocity = Vector3.zero;

        return current;
    }
}