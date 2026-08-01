using UnityEngine;

/// <summary>
/// Draws a glowing "light zone" around a chapter's playable area so the player
/// can always see where the current chapter's boundary is. Builds 4 bright
/// ground strips lying flat along the boundary edges (painted-line style),
/// 4 corner point lights, and an optional soft ground glow.
///
/// The glow only appears once the player PHYSICALLY ARRIVES inside the chapter
/// zone (presence check in Update) AND the story has reached that chapter. The
/// zone lights up when you walk into it — not earlier. It turns off when the
/// player leaves the area.
///
/// Place this on the same GameObject as a <see cref="ChapterBoundary"/> (it reads
/// the boundary's trigger collider to size the visuals). It can also be placed
/// standalone on any GameObject with a collider (e.g. a save room) — then use
/// <see cref="chapter"/> directly. Visuals are created at runtime using unlit
/// transparent materials — no scene art or prefabs needed.
/// </summary>
public class ChapterZoneHighlight : MonoBehaviour
{
    [Header("Chapter")]
    [Tooltip("Chapter number this zone belongs to. Only used when no ChapterBoundary " +
             "is present on this GameObject (standalone mode, e.g. save rooms).")]
    public int chapter = 0;

    [Header("Glow Color")]
    [Tooltip("Base color of the zone glow. Auto-picked per chapter in Reset() but overridable.")]
    public Color glowColor = new Color(0.4f, 0.9f, 0.6f, 0.35f);

    [Header("Corner Lights")]
    [Tooltip("Add a point light at each corner of the zone.")]
    public bool cornerLights = true;

    [Tooltip("Corner light intensity.")]
    public float lightIntensity = 0.7f;

    [Tooltip("Corner light range.")]
    public float lightRange = 6f;

    [Header("Ground Glow")]
    [Tooltip("Show a soft translucent glow on the ground over the whole zone.")]
    public bool showGroundGlow = true;

    [Tooltip("Ground glow opacity.")]
    [Range(0f, 1f)] public float groundAlpha = 0.12f;

    [Header("Ground Edge Strips")]
    [Tooltip("Show 4 bright strips lying flat on the ground along the 4 edges (painted-line style). Visible from any viewing angle — including straight along the X axis where the vertical front/back panels collapse to a thin line.")]
    public bool showGroundStrips = true;

    [Tooltip("Width of each ground edge strip (world units).")]
    public float stripWidth = 2.5f;

    [Tooltip("How high above the ground the strips are placed (world units).")]
    public float stripHeight = 0.1f;

    [Tooltip("Opacity of the ground edge strips.")]
    [Range(0f, 1f)] public float stripAlpha = 0.4f;

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
    private Material _groundMat;
    private Material _stripMat;
    private Light[] _cornerLights;
    private float _animTime;
    private bool _playerInside;
    private float _presenceTimer;
    private GameObject _cachedPlayer;

    private static Mesh _quadMesh;
    private static Mesh _doubleSidedQuadMesh;
    private static Mesh _cylinderMesh;

    private void Reset()
    {
        // Auto-pick a per-chapter color so each zone reads distinctly.
        var cb = GetComponent<ChapterBoundary>();
        int ch = cb != null ? cb.chapter : chapter;
        switch (ch)
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
        if (_groundMat != null) Destroy(_groundMat);
        if (_stripMat != null) Destroy(_stripMat);
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
    /// Resolve whether this zone should be visible. A zone is lit only while the
    /// player has physically arrived inside this chapter's area AND the story
    /// has reached this chapter. Completed/future chapters stay dark until the
    /// player actually walks in.
    /// </summary>
    private void EvaluateVisibility()
    {
        bool visible = false;
        var sm = StoryManager.Instance;
        if (sm != null && _playerInside)
        {
            int ch = _boundary != null ? _boundary.chapter : chapter;
            visible = sm.CurrentChapter == ch;
        }

        if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
    }

    private GameObject GetPlayer()
    {
        if (_cachedPlayer == null)
            _cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        return _cachedPlayer;
    }

    /// <summary>True while the player is inside this chapter's trigger collider.</summary>
    private bool IsPlayerInside()
    {
        if (_triggerCol == null) return false;
        var player = GetPlayer();
        if (player == null) return false;
        return _triggerCol.bounds.Contains(player.transform.position);
    }

    private void Start()
    {
        // Fallback: OnEnable may have run before StoryManager.Awake.
        Subscribe();
        _playerInside = IsPlayerInside();
        EvaluateVisibility();
    }

    private static Mesh GetQuadMesh()
    {
        if (_quadMesh != null) return _quadMesh;
        // Create a double-sided quad mesh for visibility from all angles
        _quadMesh = new Mesh();
        
        // Create 6 vertices (3 for front face, 3 for back face)
        Vector3[] vertices = new Vector3[6];
        vertices[0] = new Vector3(-0.5f, -0.5f, 0f);  // Front face
        vertices[1] = new Vector3(0.5f, -0.5f, 0f);
        vertices[2] = new Vector3(-0.5f, 0.5f, 0f);
        vertices[3] = new Vector3(0.5f, -0.5f, 0f);  // Back face (wound opposite)
        vertices[4] = new Vector3(0.5f, 0.5f, 0f);
        vertices[5] = new Vector3(-0.5f, 0.5f, 0f);
        
        // UV coordinates
        Vector2[] uv = new Vector2[6];
        uv[0] = new Vector2(0f, 0f);
        uv[1] = new Vector2(1f, 0f);
        uv[2] = new Vector2(0f, 1f);
        uv[3] = new Vector2(1f, 0f);
        uv[4] = new Vector2(1f, 1f);
        uv[5] = new Vector2(0f, 1f);
        
        // Triangles
        int[] triangles = new int[6];
        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 2;
        triangles[3] = 3;
        triangles[4] = 4;
        triangles[5] = 5;
        
        // Normals (pointing outward from both sides)
        Vector3[] normals = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            normals[i] = Vector3.back;
        }
        
        _quadMesh.vertices = vertices;
        _quadMesh.uv = uv;
        _quadMesh.triangles = triangles;
        _quadMesh.normals = normals;
        _quadMesh.RecalculateBounds();
        
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

        // Standalone mode (no ChapterBoundary) still needs a collider to size the visuals.
        if (_triggerCol == null) return;

        // Destroy any previously created zone-glow root, INCLUDING visuals that
        // were baked into the scene from a Play-mode save (5 baked "ZoneGlow"
        // hierarchies were found in the scene — without this cleanup every
        // chapter renders a duplicate glow set: baked + runtime).
        if (_root != null)
        {
            DestroyImmediate(_root);
            _root = null;
        }
        // Also clean up stray glow children that are not tracked by _root
        // (baked scene objects or leftovers from a partial rebuild).
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            if (child.name == "ZoneGlow" ||
                child.name.StartsWith("GlowPanel_") ||
                child.name.StartsWith("GlowLight_") ||
                child.name.StartsWith("GlowStrip_") ||
                child.name == "GlowGround")
            {
                DestroyImmediate(child.gameObject);
            }
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

        // ---- 4 corner point lights ----
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
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
                lightGO.transform.localPosition = cornerCenters[i] + Vector3.up * 0.5f;
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

        // ---- 4 ground edge strips (painted-line style) ----
        // These lie flat on the ground along the 4 edges so the boundary stays
        // visible from ANY viewing angle. Vertical panels collapse to a thin
        // line when looking straight along the X axis, but a strip on the
        // ground always shows its full edge length.
        if (showGroundStrips)
        {
            Vector3[] stripCenters = {
                new Vector3(center.x - halfX, stripHeight, center.z),   // X- edge, strip spans Z
                new Vector3(center.x + halfX, stripHeight, center.z),   // X+ edge, strip spans Z
                new Vector3(center.x, stripHeight, center.z - halfZ),   // Z- edge, strip spans X
                new Vector3(center.x, stripHeight, center.z + halfZ),   // Z+ edge, strip spans X
            };
            Vector3[] stripSizes = {
                new Vector3(stripWidth, size.z, 1f),   // X-: long along Z
                new Vector3(stripWidth, size.z, 1f),   // X+: long along Z
                new Vector3(size.x, stripWidth, 1f),   // Z-: long along X
                new Vector3(size.x, stripWidth, 1f),   // Z+: long along X
            };

            _stripMat = new Material(unlitShader) { name = "ZoneGlowStrip_Runtime" };
            _stripMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, stripAlpha);
            _stripMat.SetFloat("_Cull", 0f);
            _stripMat.SetFloat("_Mode", 3);

            for (int i = 0; i < 4; i++)
            {
                var stripGO = new GameObject($"GlowStrip_{i}");
                stripGO.transform.SetParent(_root.transform, false);
                stripGO.transform.localPosition = stripCenters[i];
                stripGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                stripGO.transform.localScale = stripSizes[i];

                var sFilter = stripGO.AddComponent<MeshFilter>();
                sFilter.sharedMesh = GetQuadMesh();
                var sRenderer = stripGO.AddComponent<MeshRenderer>();
                sRenderer.sharedMaterial = _stripMat;
                sRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sRenderer.receiveShadows = false;
            }
        }
    }

    private void Update()
    {
        // Periodic presence check (mirrors ChapterBoundary.Update): the glow
        // only turns on once the player physically walks into the zone, and
        // turns off when they leave. A plain OnTriggerEnter is not enough —
        // teleports (chapter transitions, respawns) bypass trigger events, so
        // we poll the player's position every second.
        _presenceTimer += Time.unscaledDeltaTime;
        if (_presenceTimer >= 1f)
        {
            _presenceTimer = 0f;
            bool inside = IsPlayerInside();
            if (inside != _playerInside)
            {
                _playerInside = inside;
                EvaluateVisibility();
            }
        }

        if (!pulse || _root == null || !_root.activeSelf) return;

        _animTime += Time.deltaTime * pulseSpeed;
        float pulseScale = 1f + Mathf.Sin(_animTime) * pulseAmount;

        // Pulse ground strips so the painted edges breathe.
        if (_stripMat != null)
            _stripMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, stripAlpha * pulseScale);

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
