using UnityEngine;

public class RespawnOnFall : MonoBehaviour
{
    [Header("Spawn Point (Optional - will be found automatically if not assigned)")]
    public Transform spawnPoint;
    public float killY = -10f;

    private Rigidbody rb;
    private CharacterController cc;
    private Transform cachedSpawnPoint;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // If spawn point not assigned, find it dynamically
        if (spawnPoint == null)
        {
            FindSpawnPoint();
        }
        else
        {
            cachedSpawnPoint = spawnPoint;
        }
    }

    private void FindSpawnPoint()
    {
        // Try to find spawn point based on player's initial position
        // This works because NetworkGameManager spawns players at spawn points
        Vector3 currentPos = transform.position;
        
        // Find all spawn points in the scene
        Transform[] allSpawnPoints = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Transform closestSpawnPoint = null;
        float closestDistance = float.MaxValue;
        
        // Look for spawn points by name pattern (RunnerSpawn_* or SabotagerSpawn_*)
        foreach (Transform t in allSpawnPoints)
        {
            if (t.name.Contains("Spawn_") && t.parent != null)
            {
                // Check if it's a spawn point (has a parent named RunnerSpawns or SabotagerSpawns)
                string parentName = t.parent.name;
                if (parentName == "RunnerSpawns" || parentName == "SabotagerSpawns")
                {
                    float distance = Vector3.Distance(currentPos, t.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestSpawnPoint = t;
                    }
                }
            }
        }
        
        if (closestSpawnPoint != null)
        {
            spawnPoint = closestSpawnPoint;
            cachedSpawnPoint = closestSpawnPoint;
            Debug.Log($"RespawnOnFall: Found spawn point '{closestSpawnPoint.name}' at distance {closestDistance:F2}");
        }
        else
        {
            Debug.LogWarning("RespawnOnFall: Could not find spawn point automatically. Player will respawn at (0,0,0) if they fall.");
        }
    }

    private void Update()
    {
        if (transform.position.y < killY)
            Respawn();
    }

    private void Respawn()
    {
        Transform respawnPoint = spawnPoint != null ? spawnPoint : cachedSpawnPoint;
        
        if (respawnPoint == null)
        {
            // Last resort: try to find it again
            FindSpawnPoint();
            respawnPoint = spawnPoint != null ? spawnPoint : cachedSpawnPoint;
        }
        
        if (respawnPoint == null)
        {
            Debug.LogError("RespawnOnFall: No spawn point available! Respawn failed.");
            return;
        }

        if (cc != null) cc.enabled = false;

        transform.position = respawnPoint.position;
        transform.rotation = respawnPoint.rotation;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cc != null) cc.enabled = true;
    }
    
    /// <summary>
    /// Set the spawn point programmatically (called by NetworkGameManager when player spawns)
    /// </summary>
    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        cachedSpawnPoint = newSpawnPoint;
    }
}