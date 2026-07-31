using UnityEngine;

/// <summary>
/// Draws a glowing "light zone" around a chapter's playable area so the player
/// can always see where the current chapter's boundary is. Builds 4 translucent
/// vertical glow panels along the boundary edges, 4 corner light pillars, and an
/// optional soft ground glow. The visuals only render while this chapter is the
/// current StoryManager chapter.
///
/// Place this on the same GameObject as a <see cref="ChapterBoundary"/> (it reads
/// the boundary's trigger collider to size the visuals). Visuals are created at
/// runtime using unlit transparent materials — no scene art or prefabs needed.
/// </summary>
[RequireComponent(typeof(ChapterBoundary))]
public class ChapterZoneHighlight : MonoBehaviour
{
    [Header("Glow Color")]
    [Tooltip("Base color of the zone glow. Auto-picked per chapter in Reset() but overridable.")]
    public Color glowColor = new Color(0.4f, 0.9f, 0.6f, 0.35f);

    [Header("Edge Panels")]
    [Tooltip("Height of the 4 vertical glow panels (world units).")]
    public float panelHeight = 6f;

    [Tooltip("Thickness of the 4 vertical glow panels (world units).")]
    public float panelThickness = 0.6f;

    [Tooltip("Opacity multiplier for the edge panels.")]
    [Range(0f, 1f)] public float panelAlpha = 0.35f;

    [Header("Corner Lights")]
    [Tooltip("Add a point light at each corner of the zone.")]
    public bool cornerLights = true;

    [Tooltip("Corner light intensity.")]
    public float lightIntensity = 1.2f;

    [Tooltip("Corner light range.")]
    public float lightRange = 8f;

    [Header("Ground Glow")]
    [Tooltip("Show a soft translucent glow on the ground over the whole zone.")]
    public bool showGroundGlow = true;

    [Tooltip("Ground glow opacity.")]
    [Range(0f, 1f)] public float groundAlpha = 0.12f;

    [Header("Anim")]
    [Tooltip("Pulse the glow panels.")]
    public bool pulse = true;

    [Tooltip("Pulse speed.")]
    public float pulseSpeed = 1.5f;

    [Tooltip("Pulse strength (0-1).")]
    [Range(0f, 1f)] public float pulseAmount = 0.15f;

    // ---- Runtime state ----
    private ChapterBoundary _boundary;
    private Collider _triggerCol;
    private GameObject _root;
    private Material _panelMat;
    private Material _groundMat;
    private Light[] _cornerLights;
    private MeshRenderer[] _panelRenderers;
    private float _animTime;

    private static Mesh _quadMesh;
    private static Mesh _cylinderMesh;

    private void Reset()
    {
        // Auto-pick a per-chapter color so each zone reads distinctly.
        var cb = GetComponent<ChapterBoundary>();
        if (cb == null) return;
        switch (cb.chapter)
        {
            case 1: glowColor = new Color(0.35f, 0.9f, 0.5f, 0.35f); break;  // green - safe camp
            case 2: glowColor = new Color(0.35f, 0.6f, 1f, 0.35f); break;    // blue - hospital
            case 3: glowColor = new Color(1f, 0.7f, 0.3f, 0.35f); break;     // orange - construction
            case 4: glowColor = new Color(0.85f, 0.45f, 1f, 0.35f); break;   // purple - residential
            case 5: glowColor = new Color(1f, 0.4f, 0.4f, 0.35f); break;     // red - apartment bridge
            default: glowColor = new Color(0.4f, 0.9f, 0.6f, 0.35f); break;
        }
    }

    private void OnEnable()
    {
        Subscribe();
        BuildVisuals();
        EvaluateVisibility();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
    }

    private void OnDestroy()
    {
        if (_panelMat != null) Destroy(_panelMat);
        if (_groundMat != null) Destroy(_groundMat);
    }

    private void Subscribe()
    {
        if (StoryManager.Instance == null) return;
        StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        EvaluateVisibility();
    }

    /// <summary>
    /// Resolve whether this zone should be visible. A zone is lit only while it
    /// is the CURRENT chapter — completed chapters go dark (the player already
    /// knows them), future chapters stay dark (not reachable yet).
    /// </summary>
    private void EvaluateVisibility()
    {
        bool visible = false;
        var sm = StoryManager.Instance;
        if (sm != null && _boundary != null)
            visible = sm.CurrentChapter == _boundary.chapter;

        if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
    }

    private void Start()
    {
        // Fallback: OnEnable may have run before StoryManager.Awake.
        Subscribe();
        EvaluateVisibility();
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

    /// <summary>
    /// Build all zone visuals under a child "ZoneGlow" root. Idempotent — destroys
    /// any previous root first so it can be rebuilt safely.
    /// </summary>
    private void BuildVisuals()
    {
        _boundary = GetComponent<ChapterBoundary>();
        _triggerCol = GetComponent<Collider>();

        if (_boundary == null || _triggerCol == null) return;

        // Destroy existing root to stay idempotent.
        if (_root != null)
        {
            DestroyImmediate(_root);
            _root = null;
        }

        _root = new GameObject("ZoneGlow");
        _root.transform.SetParent(transform, false);
        _root.transform.localPosition = Vector3.zero;
        _root.transform.localRotation = Quaternion.identity;

        Vector3 center, size;
        if (_triggerCol is BoxCollider bc)
        {
            center = bc.center;
            size = bc.size;
        }
        else
        {
            // Fallback for non-box triggers.
            center = Vector3.zero;
            size = new Vector3(60f, 20f, 60f);
        }

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");

        // ---- 4 vertical edge glow panels ----
        _panelRenderers = new MeshRenderer[4];
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        Vector3[] edgeCenters = {
            new Vector3(center.x - halfX, 0f, center.z),   // X-
            new Vector3(center.x + halfX, 0f, center.z),   // X+
            new Vector3(center.x, 0f, center.z - halfZ),   // Z-
            new Vector3(center.x, 0f, center.z + halfZ),   // Z+
        };
        Vector3[] edgeSizes = {
            new Vector3(panelThickness, panelHeight, size.z),
            new Vector3(panelThickness, panelHeight, size.z),
            new Vector3(size.x, panelHeight, panelThickness),
            new Vector3(size.x, panelHeight, panelThickness),
        };

        _panelMat = new Material(unlitShader) { name = "ZoneGlowPanel_Runtime" };
        _panelMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, panelAlpha);

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"GlowPanel_{i}");
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = edgeCenters[i];
            go.transform.localScale = edgeSizes[i];

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetQuadMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _panelMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _panelRenderers[i] = renderer;
        }

        // ---- 4 corner point lights ----
        _cornerLights = new Light[4];
        if (cornerLights)
        {
            Vector3[] cornerCenters = {
                new Vector3(center.x - halfX, 0f, center.z - halfZ),
                new Vector3(center.x + halfX, 0f, center.z - halfZ),
                new Vector3(center.x - halfX, 0f, center.z + halfZ),
                new Vector3(center.x + halfX, 0f, center.z + halfZ),
            };
            for (int i = 0; i < 4; i++)
            {
                var lightGO = new GameObject($"GlowLight_{i}");
                lightGO.transform.SetParent(_root.transform, false);
                lightGO.transform.localPosition = cornerCenters[i] + Vector3.up * 2f;
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
                light.intensity = lightIntensity;
                light.range = lightRange;
                light.shadows = LightShadows.None;
                _cornerLights[i] = light;
            }
        }

        // ---- Ground glow quad ----
        if (showGroundGlow)
        {
            var groundGO = new GameObject("GlowGround");
            groundGO.transform.SetParent(_root.transform, false);
            groundGO.transform.localPosition = new Vector3(center.x, -size.y * 0.5f + 0.05f, center.z);
            groundGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            groundGO.transform.localScale = new Vector3(size.x, size.z, 1f);

            var gFilter = groundGO.AddComponent<MeshFilter>();
            gFilter.sharedMesh = GetQuadMesh();
            var gRenderer = groundGO.AddComponent<MeshRenderer>();
            _groundMat = new Material(unlitShader) { name = "ZoneGlowGround_Runtime" };
            _groundMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, groundAlpha);
            gRenderer.sharedMaterial = _groundMat;
            gRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gRenderer.receiveShadows = false;
        }
    }

    private void Update()
    {
        if (!pulse || _root == null || !_root.activeSelf) return;

        _animTime += Time.deltaTime * pulseSpeed;
        float pulseScale = 1f + Mathf.Sin(_animTime) * pulseAmount;

        // Pulse panel alpha subtly.
        if (_panelMat != null)
            _panelMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, panelAlpha * pulseScale);

        // Pulse corner light intensity.
        if (_cornerLights != null)
        {
            foreach (var l in _cornerLights)
            {
                if (l != null) l.intensity = lightIntensity * pulseScale;
            }
        }
    }
}
