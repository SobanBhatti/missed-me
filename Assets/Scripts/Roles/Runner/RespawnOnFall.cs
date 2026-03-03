using UnityEngine;

public class RespawnOnFall : MonoBehaviour
{
    public Transform spawnPoint;
    public float killY = -10f;

    Rigidbody rb;
    CharacterController cc;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (transform.position.y < killY)
            Respawn();
    }

    private void Respawn()
    {
        if (cc != null) cc.enabled = false;

        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cc != null) cc.enabled = true;
    }
}