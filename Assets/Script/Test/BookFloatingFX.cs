using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only animation for the journal book models ("BookModel" children of
/// Collectible pickups). Books gently bob up and down and slowly spin around
/// their vertical axis so they read as interactive pickups.
///
/// Self-installs at scene load (no scene/prefab edits needed): it scans the
/// scene for BookModel children under Collectibles and animates their local
/// transform every frame. Entries with a deactivated parent (journal already
/// collected) are skipped automatically.
/// </summary>
public class BookFloatingFX : MonoBehaviour
{
    [Header("Floating")]
    [Tooltip("How far the book bobs up and down (world units).")]
    public float floatAmplitude = 0.15f;

    [Tooltip("Bob speed in radians per second (2*pi = one full cycle per second).")]
    public float floatSpeed = 1.2f;

    [Header("Spin")]
    [Tooltip("Seconds for one full 360° rotation around the vertical axis.")]
    public float spinPeriod = 5f;

    private struct Entry
    {
        public Transform target;
        public Vector3 basePos;
        public Quaternion baseRot;
        public float phase;
    }

    private readonly List<Entry> _entries = new List<Entry>();

    private const string BookModelName = "BookModel";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { }

    /// <summary>
    /// Creates the FX host at scene load if it isn't already present (e.g. it
    /// was manually placed in the scene).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<BookFloatingFX>() == null)
        {
            var go = new GameObject("BookFloatingFX");
            go.AddComponent<BookFloatingFX>();
        }
    }

    private void Start()
    {
        var all = FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var go = all[i];
            if (go == null || go.name != BookModelName) continue;
            var parent = go.transform.parent;
            if (parent == null || parent.GetComponent<Collectible>() == null) continue;

            var entry = new Entry
            {
                target = go.transform,
                basePos = go.transform.localPosition,
                baseRot = go.transform.localRotation,
                phase = Random.Range(0f, Mathf.PI * 2f)
            };
            _entries.Add(entry);
        }
    }

    private void Update()
    {
        float t = Time.time;
        float spinDegrees = spinPeriod > 0.0001f ? 360f / spinPeriod : 0f;

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.target == null || !e.target.gameObject.activeInHierarchy) continue;

            float dy = Mathf.Sin(t * floatSpeed + e.phase) * floatAmplitude;
            e.target.localPosition = e.basePos + new Vector3(0f, dy, 0f);
            e.target.localRotation = e.baseRot * Quaternion.Euler(0f, t * spinDegrees, 0f);
        }
    }
}
