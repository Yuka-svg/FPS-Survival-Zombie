using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Auto-builds lightweight world-space beacons above every uncollected
/// collectible in the CURRENT chapter, so the player always knows where the
/// remaining journals/pickups are without wandering the whole map.
///
/// How it works:
/// - On start, scans every <see cref="Collectible"/> and assigns it to a chapter
///   by testing which <see cref="ChapterBoundary"/> trigger collider contains it
///   (falls back to the nearest boundary).
/// - Builds one small beacon (ground ring + floating book icon) as a CHILD of
///   each collectible. Because collectibles deactivate on pickup
///   (<see cref="Collectible.Collect"/>), the beacon hides itself automatically
///   once collected.
/// - Only beacons for the current chapter are visible; when the chapter changes
///   the beacons swap over. Beacons are also hidden once the player is close
///   (hideDistance) so they don't cover the pickup itself.
///
/// Create one empty GameObject with this component in the scene (e.g. under
/// "=== SYSTEMS ===").
/// </summary>
public class CollectibleBeaconManager : MonoBehaviour
{
    public static CollectibleBeaconManager Instance { get; private set; }

    [Header("Visibility")]
    [Tooltip("Hide the beacon once the player gets within this distance of the collectible.")]
    public float hideDistance = 3f;

    [Tooltip("If true, hide beacons for collectibles whose chapter is unknown (outside every boundary).")]
    public bool hideUnknownChapter = false;

    [Header("Ground Ring")]
    public bool showGroundRing = true;
    public float ringRadius = 1.2f;
    public Color ringColor = new Color(0.45f, 0.9f, 0.5f, 0.7f);

    [Header("Floating Icon")]
    public float iconHeight = 2.4f;
    public float iconBobAmplitude = 0.25f;
    public float iconBobSpeed = 2.2f;
    public float iconSize = 0.9f;
    public Color iconColor = new Color(0.55f, 0.95f, 0.6f, 1f);

    // ---- Runtime ----
    private readonly List<BeaconData> _beacons = new List<BeaconData>();
    private int _currentChapter = -1;

    private class BeaconData
    {
        public Collectible collectible;
        public int chapter;
        public GameObject ring;
        public GameObject icon;
        public Material ringMat;
        public Material iconMat;
        public float bobTime;
        public Transform player;
    }

    private static Mesh _quadMesh;
    private static Mesh _cylinderMesh;
    private static Sprite _bookIcon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var b in _beacons)
        {
            if (b.ringMat != null) Destroy(b.ringMat);
            if (b.iconMat != null) Destroy(b.iconMat);
        }
    }

    private void Subscribe()
    {
        if (StoryManager.Instance == null) return;
        StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        _currentChapter = newChapter;
        RefreshVisibility();
    }

    private void Start()
    {
        // Fallback subscribe (OnEnable may run before StoryManager.Awake).
        Subscribe();
        ScanAndBuild();
    }

    /// <summary>
    /// Find every Collectible + ChapterBoundary in the scene, assign each
    /// collectible to a chapter, and build a beacon child on it.
    /// </summary>
    private void ScanAndBuild()
    {
        var collectibles = FindObjectsByType<Collectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var boundaries = FindObjectsByType<ChapterBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (collectibles == null || collectibles.Length == 0) return;

        // Precompute boundary colliders + bounds.
        var boundCols = new List<Collider>();
        foreach (var b in boundaries)
        {
            if (b == null) continue;
            var c = b.GetComponent<Collider>();
            if (c != null) boundCols.Add(c);
        }

        foreach (var col in collectibles)
        {
            if (col == null) continue;
            int chapter = AssignChapter(col.transform.position, boundaries, boundCols);
            BuildBeacon(col, chapter);
        }

        if (StoryManager.Instance != null)
            _currentChapter = StoryManager.Instance.CurrentChapter;
        RefreshVisibility();
        Debug.Log($"[CollectibleBeaconManager] Built {_beacons.Count} collectible beacons.");
    }

    /// <summary>
    /// Assign a chapter to a world position by testing every boundary trigger
    /// collider (containment), falling back to the nearest boundary center.
    /// Returns 0 if no boundary exists at all.
    /// </summary>
    private static int AssignChapter(Vector3 pos, ChapterBoundary[] boundaries, List<Collider> boundCols)
    {
        if (boundaries == null || boundaries.Length == 0) return 0;

        // 1) Containment test.
        for (int i = 0; i < boundaries.Length; i++)
        {
            if (boundaries[i] == null || boundCols[i] == null) continue;
            if (boundCols[i].bounds.Contains(pos))
                return boundaries[i].chapter;
        }

        // 2) Nearest boundary center.
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < boundaries.Length; i++)
        {
            if (boundaries[i] == null || boundCols[i] == null) continue;
            float d = Vector3.SqrMagnitude(boundCols[i].bounds.center - pos);
            if (d < bestDist)
            {
                bestDist = d;
                best = boundaries[i].chapter;
            }
        }
        return best;
    }

    /// <summary>
    /// Build the ring + icon visuals as children of the collectible GameObject
    /// (so they vanish when it's collected / deactivated).
    /// </summary>
    private void BuildBeacon(Collectible collectible, int chapter)
    {
        var data = new BeaconData { collectible = collectible, chapter = chapter, player = FindPlayer() };

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");

        // Ground ring (flat cylinder).
        if (showGroundRing)
        {
            var ringGO = new GameObject("Beacon_Ring");
            ringGO.transform.SetParent(collectible.transform, false);
            ringGO.transform.localPosition = Vector3.up * 0.05f;
            ringGO.transform.localScale = new Vector3(ringRadius * 2f, 0.02f, ringRadius * 2f);

            var filter = ringGO.AddComponent<MeshFilter>();
            filter.sharedMesh = GetCylinderMesh();
            var renderer = ringGO.AddComponent<MeshRenderer>();
            data.ringMat = new Material(unlitShader) { name = "CollectibleBeaconRing_Runtime" };
            data.ringMat.color = ringColor;
            renderer.sharedMaterial = data.ringMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            data.ring = ringGO;
        }

        // Floating book icon (billboard quad).
        {
            var iconGO = new GameObject("Beacon_Icon");
            iconGO.transform.SetParent(collectible.transform, false);
            iconGO.transform.localPosition = Vector3.up * iconHeight;
            iconGO.transform.localScale = new Vector3(iconSize, iconSize, iconSize);

            var filter = iconGO.AddComponent<MeshFilter>();
            filter.sharedMesh = GetQuadMesh();
            var renderer = iconGO.AddComponent<MeshRenderer>();
            data.iconMat = new Material(unlitShader) { name = "CollectibleBeaconIcon_Runtime" };
            data.iconMat.color = iconColor;
            if (GetBookIcon() != null)
                data.iconMat.SetTexture("_MainTex", GetBookIcon().texture);
            renderer.sharedMaterial = data.iconMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            data.icon = iconGO;
        }

        _beacons.Add(data);
    }

    private static Transform FindPlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.transform : null;
    }

    /// <summary>
    /// Show/hide every beacon based on the current chapter. A beacon is visible
    /// only if: its collectible is still active (not collected), and its chapter
    /// matches the current one (or chapter 0 = unknown, controlled by
    /// hideUnknownChapter).
    /// </summary>
    private void RefreshVisibility()
    {
        foreach (var b in _beacons)
        {
            if (b == null || b.collectible == null) continue;
            // Collectible deactivates on pickup; if it's inactive, hide beacon.
            bool collected = !b.collectible.gameObject.activeSelf;
            bool chapterOk = b.chapter == 0
                ? !hideUnknownChapter
                : b.chapter == _currentChapter;
            bool visible = !collected && chapterOk;
            if (b.ring != null) b.ring.SetActive(visible);
            if (b.icon != null) b.icon.SetActive(visible);
        }
    }

    private void Update()
    {
        if (_beacons.Count == 0) return;

        for (int i = 0; i < _beacons.Count; i++)
        {
            var b = _beacons[i];
            if (b == null || b.collectible == null) continue;
            if (b.icon == null || !b.icon.activeSelf) continue;

            b.bobTime += Time.deltaTime * iconBobSpeed;
            float bob = Mathf.Sin(b.bobTime) * iconBobAmplitude;
            b.icon.transform.localPosition = Vector3.up * (iconHeight + bob);

            // Billboard the icon toward the camera.
            if (Camera.main != null)
            {
                var cam = Camera.main.transform;
                b.icon.transform.rotation = Quaternion.LookRotation(
                    b.icon.transform.position - cam.position);
            }

            // Hide when close so the beacon doesn't cover the pickup.
            if (hideDistance > 0f)
            {
                if (b.player == null) b.player = FindPlayer();
                if (b.player != null)
                {
                    bool near = Vector3.Distance(b.player.position, b.collectible.transform.position) < hideDistance;
                    if (b.ring != null && b.ring.activeSelf == near) b.ring.SetActive(!near);
                    if (b.icon != null && b.icon.activeSelf == near) b.icon.SetActive(!near);
                }
            }
        }
    }

    private static Mesh GetQuadMesh()
    {
        if (_quadMesh != null) return _quadMesh;
        var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return _quadMesh;
    }

    private static Mesh GetCylinderMesh()
    {
        if (_cylinderMesh != null) return _cylinderMesh;
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _cylinderMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return _cylinderMesh;
    }

    private static Sprite GetBookIcon()
    {
        if (_bookIcon != null) return _bookIcon;
        _bookIcon = Resources.Load<Sprite>("BeaconIcons/icon_book");
        return _bookIcon;
    }
}
