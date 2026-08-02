using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class EndlessBoundary : MonoBehaviour
{
    public Vector2 boundarySize = new Vector2(52f, 52f);
    public float wallHeight = 20f;
    public float wallThickness = 0.5f;

    private BoxCollider _trigger;
    private GameObject _player;
    private Vector3 _lastInsidePos;
    private bool _built;

    private void Reset()
    {
        var c = GetComponent<BoxCollider>();
        if (c) c.isTrigger = true;
    }

    private void Awake()
    {
        if (GameModeManager.CurrentMode != GameMode.Endless)
        {
            enabled = false;
            return;
        }

        wallHeight = 100f;
        _trigger = GetComponent<BoxCollider>();
        _trigger.isTrigger = true;
        _trigger.size = new Vector3(boundarySize.x, wallHeight, boundarySize.y);
        _trigger.center = Vector3.zero;

        if (_built) return;
        _built = true;

        float hx = boundarySize.x * 0.5f;
        float hz = boundarySize.y * 0.5f;

        Vector3[] centers = {
            new Vector3(0, 0, -hz),
            new Vector3(0, 0, hz),
            new Vector3(-hx, 0, 0),
            new Vector3(hx, 0, 0),
        };
        Vector3[] sizes = {
            new Vector3(hx * 2 + wallThickness, wallHeight, wallThickness),
            new Vector3(hx * 2 + wallThickness, wallHeight, wallThickness),
            new Vector3(wallThickness, wallHeight, hz * 2 + wallThickness),
            new Vector3(wallThickness, wallHeight, hz * 2 + wallThickness),
        };

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Wall_" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = centers[i];
            go.tag = "Boundary";

            var col = go.AddComponent<BoxCollider>();
            col.size = sizes[i];

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.size = sizes[i];
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }
    }

    private void Update()
    {
        if (GameModeManager.CurrentMode != GameMode.Endless) return;

        if (_player == null)
        {
            _player = GameObject.FindGameObjectWithTag("Player");
            return;
        }

        Vector3 pos = _player.transform.position;

        // Fall out of map protection: If player falls below Y = -50f, reset velocity and teleport back to Y = 1f
        if (pos.y < -50f)
        {
            var rb = _player.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Vector3 respawnPos = _lastInsidePos != Vector3.zero ? _lastInsidePos : pos;
            respawnPos.y = 1f;
            _player.transform.position = respawnPos;
            return;
        }

        if (!_trigger.bounds.Contains(pos))
        {
            var rb = _player.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Vector3 safePos = _lastInsidePos != Vector3.zero ? _lastInsidePos : transform.position;
            if (safePos.y < 1f) safePos.y = 1f;
            _player.transform.position = safePos;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
            _lastInsidePos = other.transform.position;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        float hx = boundarySize.x * 0.5f;
        float hz = boundarySize.y * 0.5f;
        Vector3 origin = transform.position;
        Vector3[] corners = {
            origin + new Vector3(-hx, 0, -hz),
            origin + new Vector3(hx, 0, -hz),
            origin + new Vector3(hx, 0, hz),
            origin + new Vector3(-hx, 0, hz),
        };
        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
}
