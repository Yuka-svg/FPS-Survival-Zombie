using UnityEngine;
using cowsins;
using System.Collections;
using System.Collections.Generic;

public class EndlessAirdropManager : MonoBehaviour
{
    public static EndlessAirdropManager Instance;

    [Header("Airdrop Settings")]
    public float spawnInterval = 300f;
    [Range(0f, 1f)]
    public float intervalRandomRange = 0.2f;
    public float dropHeight = 15f;
    public GameObject[] lootboxPrefabs;

    [Header("GiftBox Drop (Endless Mode)")]
    [Tooltip("GiftBox prefab mà zombie có thể drop khi chết. Chỉ cần gán ở đây, tất cả enemy tự dùng chung.")]
    public GameObject giftBoxPrefab;

    [Header("Airdrop Markers in Scene")]
    [Tooltip("Các AirdropMarker có sẵn trong scene. Lootbox sẽ rơi ngay tại vị trí các marker này.")]
    public GameObject[] airdropMarkers;

    private float _timer;
    private Transform _player;
    private bool _dropPending;
    private GameObject _activeMarker;
    private GameObject _activeLootbox;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (giftBoxPrefab != null)
            LootDropHelper.SharedGiftBoxPrefab = giftBoxPrefab;
    }

    private void Start()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.SetMode(GameMode.Endless);
        ResetTimer();
        FindPlayer();
        FindAirdropMarkers();
        foreach (var marker in airdropMarkers)
            SetLightActive(marker, false);
    }

    private void FindAirdropMarkers()
    {
        if (airdropMarkers == null || airdropMarkers.Length == 0)
        {
            GameObject[] found = GameObject.FindGameObjectsWithTag("Untagged");
            var list = new List<GameObject>();
            foreach (var go in found)
            {
                if (go.name.StartsWith("AirdropMarker"))
                    list.Add(go);
            }
            airdropMarkers = list.ToArray();
        }
    }

    private void ResetTimer()
    {
        float range = spawnInterval * intervalRandomRange;
        _timer = spawnInterval + Random.Range(-range, range);
    }

    private void FindPlayer()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
    }

    private void Update()
    {
        if (_player == null)
        {
            FindPlayer();
            if (_player == null) return;
        }

        if (_dropPending) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            ResetTimer();
            StartAirdrop();
        }

        if (_activeLootbox == null && _activeMarker != null)
        {
            SetLightActive(_activeMarker, false);
            _activeMarker = null;
        }
    }

    private void StartAirdrop()
    {
        GameObject marker = GetAvailableMarker();
        if (marker == null)
        {
            Debug.LogWarning("[Airdrop] No available AirdropMarker found!");
            return;
        }

        _dropPending = true;
        Vector3 markerPos = marker.transform.position;
        SetLightActive(marker, true);
        _activeMarker = marker;

        float dropFrom = Mathf.Min(dropHeight, 15f);
        Vector3 spawnPos = markerPos + Vector3.up * dropFrom;

        GameObject selectedPrefab = lootboxPrefabs[Random.Range(0, lootboxPrefabs.Length)];
        GameObject lootbox = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);
        _activeLootbox = lootbox;
        lootbox.layer = LayerMask.NameToLayer("Interactable");

        Rigidbody rb = lootbox.GetComponent<Rigidbody>();
        if (rb == null)
            rb = lootbox.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 50f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.freezeRotation = true;

        var lb = lootbox.GetComponent<Lootbox>();
        if (lb != null)
            lb.Price = 0;

        _dropPending = false;

        StartCoroutine(TurnOffLightOnLanded(marker, lootbox));
    }

    private IEnumerator TurnOffLightOnLanded(GameObject marker, GameObject lootbox)
    {
        Rigidbody rb = lootbox != null ? lootbox.GetComponent<Rigidbody>() : null;
        float groundedTime = 0f;
        while (lootbox != null)
        {
            if (rb != null && rb.linearVelocity.magnitude < 0.1f)
            {
                groundedTime += Time.deltaTime;
                if (groundedTime >= 0.2f)
                    break;
            }
            else
            {
                groundedTime = 0f;
            }
            yield return null;
        }
        SetLightActive(marker, false);
    }

    private GameObject GetAvailableMarker()
    {
        foreach (var marker in airdropMarkers)
        {
            if (marker != null && marker != _activeMarker)
                return marker;
        }
        return null;
    }

    private void SetLightActive(GameObject marker, bool active)
    {
        if (marker == null) return;
        var light = marker.GetComponentInChildren<Light>(true);
        if (light != null)
            light.gameObject.SetActive(active);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
        if (airdropMarkers != null)
        {
            foreach (var marker in airdropMarkers)
            {
                if (marker != null)
                    Gizmos.DrawSphere(marker.transform.position, 1f);
            }
        }
    }
}
