using UnityEngine;
using cowsins;

/// <summary>
/// Trigger zone inside a shop. When the player enters the trigger and presses
/// E (Interacting), the supplies are "collected" and CompanionManager is
/// notified. After collection, the collider is disabled so the prompt
/// disappears.
///
/// Part of the Chapter 3 follower recruitment arc:
///   Stage 2 (accept "giúp vào tiệm") → enable shop triggers →
///   player loots each shop (E) → siege event → Stage 3.
///
/// The GameObject MUST be on the "Interactable" layer (layer 9) and have a
/// trigger Collider so InteractManager can detect it. Alternatively, the
/// proximity E-press fallback works without InteractManager.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CompanionShopTrigger : MonoBehaviour
{
    [Header("Prompt")]
    [Tooltip("Text shown when the player is inside the trigger and can collect supplies.")]
    public string promptText = "Nhấn [E] để lấy nhu yếu phẩm";

    [Header("Proximity Fallback")]
    [Tooltip("If true, also allow E-press via proximity (not just InteractManager).")]
    public bool useProximityFallback = true;

    [Tooltip("Max distance for proximity interaction (only used if useProximityFallback).")]
    public float proximityDistance = 3f;

    private bool _available;     // True when Stage 2 is active and this shop can be looted.
    private bool _collected;     // True after this shop's supplies have been collected.
    private Collider _collider;
    private Transform _player;
    private cowsins.InputManager _playerInput;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null) _collider.isTrigger = true;
        // Disabled until Stage 2 is accepted.
        SetAvailable(false);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;
        if (_playerInput == null && _player != null)
        {
            var p = _player.gameObject;
            _playerInput = p.GetComponentInParent<cowsins.InputManager>();
            if (_playerInput == null && p.transform.parent != null)
                _playerInput = p.transform.parent.GetComponentInChildren<cowsins.InputManager>();
            if (_playerInput == null)
                _playerInput = p.GetComponentInChildren<cowsins.InputManager>();
        }
    }

    /// <summary>Enables/disables this shop trigger. Called by CompanionManager.</summary>
    public void SetAvailable(bool available)
    {
        _available = available;
        if (_collider != null) _collider.enabled = available && !_collected;
    }

    private void Update()
    {
        if (!_available || _collected) return;
        if (_player == null) { FindPlayer(); return; }

        // Proximity fallback: check E press when near the trigger.
        if (useProximityFallback)
        {
            bool ePressed = _playerInput != null
                ? _playerInput.StartInteraction
                : Input.GetKeyDown(KeyCode.E);
            if (ePressed)
            {
                float dist = Vector3.Distance(transform.position, _player.position);
                if (dist <= proximityDistance)
                {
                    Collect();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // InteractManager-style: if the player enters the trigger, they can
        // press E to collect. The actual E detection is in Update (proximity).
        // This hook is kept for future InteractManager integration.
    }

    /// <summary>Called when the player collects the supplies from this shop.</summary>
    private void Collect()
    {
        if (_collected || !_available) return;
        _collected = true;
        if (_collider != null) _collider.enabled = false;
        _available = false;

        if (CompanionManager.Instance != null)
        {
            CompanionManager.Instance.OnShopSuppliesCollected();
        }
        Debug.Log($"[CompanionShopTrigger] Supplies collected at {transform.position}.");
    }
}
