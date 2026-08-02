using UnityEngine;
using cowsins;
using System.Collections.Generic;

/// <summary>
/// Draws a glowing "light zone" around a chapter's playable area so the player
/// can always see where the current chapter's boundary is. Builds 4 bright
/// ground strips lying flat along the boundary edges (painted-line style),
/// 4 corner point lights, and an optional soft ground glow.
///
/// The glow behaves as a boundary MARKER: it shines at full strength while the
/// player is OUTSIDE the chapter zone (so the edge is easy to spot from afar),
/// and dims to <see cref="insideDimFactor"/> while the player is inside. It only
/// engages once the story has reached that chapter; completed/future chapters
/// stay dark regardless of position.
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
    [Tooltip("Add a point light at each corner of the zone. Off by default — lights spill into the zone interior; the flat ground glow + strips are the intended boundary visuals.")]
    public bool cornerLights = false;

    [Tooltip("Corner light intensity.")]
    public float lightIntensity = 0.7f;

    [Tooltip("Corner light range.")]
    public float lightRange = 6f;

    [Header("Ground Glow")]
    [Tooltip("Show a soft translucent glow on the ground over the whole zone. Off by default — the full-zone quad tints the floor with the glow color; the edge strips are the intended boundary visuals.")]
    public bool showGroundGlow = false;

    [Tooltip("Ground glow opacity.")]
    [Range(0f, 1f)] public float groundAlpha = 0.12f;

    [Header("Visual Size (Standalone)")]
    [Tooltip("Visual footprint override, decoupled from the gameplay collider. " +
             "Zero = size the visuals from the trigger collider (chapter-boundary mode). " +
             "Used on save rooms so the glow is compact while the trigger stays big.")]
    public Vector3 visualSize = Vector3.zero;

    [Tooltip("Ground the glow visuals to the floor (raycast down), so strips/lights " +
             "hug the ground instead of floating at the collider's mid height.")]
    public bool stickToGround = true;

    [Tooltip("Height of the glow visuals above the ground when stickToGround is on.")]
    public float groundOffset = 0.08f;

    [Header("Ground Edge Strips")]
    [Tooltip("Show 4 bright strips lying flat on the ground along the 4 edges (painted-line style). Visible from any viewing angle — including straight along the X axis where the vertical front/back panels collapse to a thin line.")]
    public bool showGroundStrips = true;

    [Tooltip("Width of each ground edge strip (world units).")]
    public float stripWidth = 1.5f;

    [Tooltip("How high above the ground the strips are placed (world units).")]
    public float stripHeight = 0.1f;

    [Tooltip("Opacity of the ground edge strips.")]
    [Range(0f, 1f)] public float stripAlpha = 0.32f;

    [Header("Soft Edge Quality")]
    [Tooltip("Apply smooth alpha gradients (soft falloff at both edges of each strip, " +
             "instead of hard flat rectangles) via procedurally generated textures.")]
    public bool softEdges = true;

    [Tooltip("Add a soft inner halo hugging the 4 edges — a gentle glow that fades " +
             "toward the inside of the zone, so the boundary reads as light spilling " +
             "in from the edge rather than painted lines.")]
    public bool rimGlow = false;

    [Tooltip("Width of the inner halo rim, as a fraction of the zone size (clamped to min 3m).")]
    [Range(0.05f, 0.5f)] public float rimWidthFactor = 0.18f;

    [Tooltip("Opacity of the inner halo rim.")]
    [Range(0f, 1f)] public float rimAlpha = 0.12f;

    [Header("Inside Dimming")]
    [Tooltip("Brightness multiplier applied while the player is INSIDE the zone. " +
             "The glow shines at full strength only when the player is OUTSIDE, " +
             "so the boundary reads as a marker from afar and stays subtle inside.")]
    [Range(0f, 1f)] public float insideDimFactor = 0.35f;

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
    private Material _rimMat;
    private Texture2D _stripTex;
    private Texture2D _rimTex;
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
        if (_rimMat != null) Destroy(_rimMat);
        if (_stripTex != null) Destroy(_stripTex);
        if (_rimTex != null) Destroy(_rimTex);
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
    /// Resolve whether this zone's visuals should exist at all. The glow is a
    /// boundary MARKER for the current chapter: it is built (full brightness)
    /// while the player is OUTSIDE the zone, and dims to
    /// <see cref="insideDimFactor"/> while they're inside — the dimming is
    /// applied every frame in Update, so the root object stays alive as long as
    /// the story is on this chapter. Completed/future chapters stay dark
    /// regardless of position.
    /// </summary>
    private void EvaluateVisibility()
    {
        bool visible = false;
        var sm = StoryManager.Instance;
        if (sm != null)
        {
            int ch = _boundary != null ? _boundary.chapter : chapter;
            visible = sm.CurrentChapter == ch;
        }

        if (_root != null && _root.activeSelf != visible)
        {
            if (visible) SnapGlowToGround();
            _root.SetActive(visible);
        }
    }

    /// <summary>
    /// Raycast straight down at the given world XZ and return the first solid
    /// floor hit, skipping the player, save rooms and this zone's own children
    /// (a standing player would otherwise occlude the ray and float the glow).
    /// </summary>
    private float RaycastGround(Vector3 xz)
    {
        Vector3 origin = new Vector3(xz.x, transform.position.y + 50f, xz.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        float y = transform.position.y - 0.2f;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            var go = hit.collider.gameObject;
            if (go.GetComponentInParent<PlayerMovement>() != null) continue;
            if (go.GetComponentInParent<SaveRoom>() != null) continue;
            if (go.transform.IsChildOf(transform)) continue;
            y = hit.point.y;
            break;
        }
        return y;
    }

    /// <summary>
    /// World Y of the floor under the ZONE (center + 4 edge midpoints),
    /// independent of where the player currently stands. Sampling at the
    /// player's feet is wrong: when the player is outside the zone (the case
    /// where the glow is visible) their feet may be on a roof, a bridge or the
    /// far side of a hill, which would pin the glow in the air.
    ///
    /// The 5 samples are collapsed to the MEDIAN OF THE 3 LOWEST. This rejects
    /// tall outliers (building roofs, tunnel mouths, hills — e.g. Ch1's zone
    /// spans a quarantine wall at y=1.2, a tunnel at y=4.8 and a hill at y=7.6)
    /// that would otherwise lift the whole glow off the ground, while a single
    /// deep pit (Ch3's y=-2.5 trench) can't drag it down either.
    /// </summary>
    private float GetZoneGroundWorldY()
    {
        if (_triggerCol == null) return transform.position.y;
        Vector3 zoneCenter = _triggerCol.bounds.center;
        Vector3 zoneSize = _triggerCol.bounds.size;
        Vector3[] samples =
        {
            new Vector3(zoneCenter.x, 0f, zoneCenter.z),
            new Vector3(zoneCenter.x - zoneSize.x * 0.45f, 0f, zoneCenter.z),
            new Vector3(zoneCenter.x + zoneSize.x * 0.45f, 0f, zoneCenter.z),
            new Vector3(zoneCenter.x, 0f, zoneCenter.z - zoneSize.z * 0.45f),
            new Vector3(zoneCenter.x, 0f, zoneCenter.z + zoneSize.z * 0.45f),
        };
        List<float> floorYs = new List<float>(samples.Length);
        foreach (Vector3 sample in samples)
            floorYs.Add(RaycastGround(sample));
        floorYs.Sort();
        return floorYs[1];
    }

    /// <summary>
    /// Re-pin all glow visuals to the zone's ground. Called each time the glow
    /// becomes visible (player steps out of the zone / story reaches the
    /// chapter), so large or uneven zones always hug the ground they belong to.
    /// </summary>
    private void SnapGlowToGround()
    {
        if (!stickToGround || _root == null) return;
        float groundLocalY = GetZoneGroundWorldY() - transform.position.y;
        float stripY = groundLocalY + groundOffset;
        float glowY = groundLocalY + 0.04f;
        float lightY = groundLocalY + 0.35f;
        foreach (Transform child in _root.transform)
        {
            Vector3 p = child.localPosition;
            if (child.name == "GlowGround" || child.name.StartsWith("GlowRim_"))
                child.localPosition = new Vector3(p.x, glowY, p.z);
            else if (child.name.StartsWith("GlowStrip_"))
                child.localPosition = new Vector3(p.x, stripY, p.z);
            else if (child.name.StartsWith("GlowLight_"))
                child.localPosition = new Vector3(p.x, lightY, p.z);
        }
    }

    private GameObject GetPlayer()
    {
        if (_cachedPlayer == null)
            _cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        return _cachedPlayer;
    }

    /// <summary>
    /// True while the player is inside this chapter's trigger collider.
    /// Compared in XZ only (ignores Y): the colliders are tall (chapter bounds
    /// span y=-10..20, save rooms ~4m), so a 3D bounds.Contains would keep
    /// reporting "inside" while the player stands on roofs or flies high above
    /// the zone — and would report "outside" for elevated walkable areas (Ch5's
    /// bridge sits at y~24, far above the save-room collider's top). The player
    /// is a ground creature; the boundary question is purely horizontal.
    /// </summary>
    private bool IsPlayerInside()
    {
        if (_triggerCol == null) return false;
        var player = GetPlayer();
        if (player == null) return false;
        Vector3 b = _triggerCol.bounds.center;
        Vector3 e = _triggerCol.bounds.extents;
        Vector3 p = player.transform.position;
        return Mathf.Abs(p.x - b.x) <= e.x && Mathf.Abs(p.z - b.z) <= e.z;
    }

    private void Start()
    {
        // Fallback: OnEnable may have run before StoryManager.Awake.
        Subscribe();
        _playerInside = IsPlayerInside();
        EvaluateVisibility();
    }

    /// <summary>
    /// 1D alpha gradient for the edge glow: 0 at both outer edges, peaking in
    /// the middle (soft bell profile). Applied along the V axis — LineRenderer
    /// maps U along the line's length and V across its width, so a V-wise
    /// gradient melts the two long edges of the closed loop into the ground
    /// instead of hard rectangles.
    /// </summary>
    private static Texture2D MakeSoftStripTexture()
    {
        const int h = 64;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            float a = Mathf.Sin(t * Mathf.PI);
            a = a * a; // Squared sine: wider soft shoulder, gentler peak.
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 1D alpha gradient for the halo rim: 0 deep inside the zone, ramping up
    /// near the edge, then melting to 0 right at the boundary line itself — a
    /// glow that spills inward from the border instead of a hard edge.
    /// </summary>
    private static Texture2D MakeRimTexture()
    {
        const int h = 64;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            float v = y / (h - 1f);
            float a = Mathf.SmoothStep(0.25f, 0.85f, v) * (1f - Mathf.SmoothStep(0.85f, 1f, v));
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
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
        if (visualSize != Vector3.zero)
        {
            // Standalone/save-room mode: compact visual footprint decoupled
            // from the gameplay collider. X/Z are converted to LOCAL space
            // (bounds.center is world; child positions below are local).
            // Y is re-derived from the ground via raycast.
            center = new Vector3(
                _triggerCol.bounds.center.x - transform.position.x,
                transform.position.y,
                _triggerCol.bounds.center.z - transform.position.z);
            size = new Vector3(visualSize.x, visualSize.y, visualSize.z);
        }
        else if (_triggerCol is BoxCollider bc)
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

        // ---- Ground level (world Y of the floor under the zone) ----
        // Used to pin strips/lights/ground glow flat against the ground instead
        // of floating at the collider's mid height (save rooms sit at y=1 with
        // a 4-tall collider, so naive placement puts visuals ~1m in the air).
        // Samples the zone center + 4 edge midpoints and takes the MEDIAN: a
        // single center raycast can land on a building roof (Ch2's center is
        // over a commercial block); the median of the edge samples pulls it
        // back down to the street. Never sampled at the player's feet — the
        // glow must pin to the ZONE's ground, not whatever the player happens
        // to stand on (roofs, bridges), or it floats in the air.
        float groundWorldY = stickToGround ? GetZoneGroundWorldY() : 0f;
        // Local Y (relative to this transform) of the ground, plus offsets.
        float groundLocalY = groundWorldY - transform.position.y;
        float stripY = groundLocalY + (stickToGround ? groundOffset : stripHeight);
        float glowY = groundLocalY + 0.04f;
        float lightY = groundLocalY + 0.35f;

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");

        // ---- 4 corner point lights ----
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        _cornerLights = new Light[4];
        if (cornerLights)
        {
            Vector3[] cornerCenters = {
                new Vector3(center.x - halfX, lightY, center.z - halfZ),
                new Vector3(center.x + halfX, lightY, center.z - halfZ),
                new Vector3(center.x - halfX, lightY, center.z + halfZ),
                new Vector3(center.x + halfX, lightY, center.z + halfZ),
            };
            for (int i = 0; i < 4; i++)
            {
                var lightGO = new GameObject($"GlowLight_{i}");
                lightGO.transform.SetParent(_root.transform, false);
                lightGO.transform.localPosition = cornerCenters[i];
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
            groundGO.transform.localPosition = new Vector3(center.x, glowY, center.z);
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

        // ---- Ground edge glow (continuous closed loop) ----
        // A single LineRenderer draws a closed rectangle around the zone
        // footprint. One renderer (vs 4 separate quads) means the boundary is
        // seamless at the corners: no double-bright overlaps and no visible
        // breaks where individual strips used to meet.
        if (showGroundStrips)
        {
            _stripMat = new Material(unlitShader) { name = "ZoneGlowStrip_Runtime" };
            _stripMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, stripAlpha);
            _stripMat.SetFloat("_Cull", 0f);
            _stripMat.SetFloat("_Mode", 3);
            if (softEdges)
            {
                _stripTex = MakeSoftStripTexture();
                _stripMat.mainTexture = _stripTex;
            }

            var stripGO = new GameObject("GlowStrip");
            stripGO.transform.SetParent(_root.transform, false);
            stripGO.transform.localPosition = new Vector3(center.x, stripY, center.z);
            // TransformZ alignment draws the line flat in the transform's XY
            // plane; rotating 90° about X lays that plane down onto the ground,
            // exactly like the old strip quads.
            stripGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var line = stripGO.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            // Alignment Local keeps the line inside the transform's XY plane;
            // after the 90°-about-X rotation that plane lies flat on the ground
            // (local X stays world X, local Y becomes world Z), so the corner
            // points are written as (x, z, 0).
            line.SetPositions(new[]
            {
                new Vector3(-halfX, -halfZ, 0f),
                new Vector3(+halfX, -halfZ, 0f),
                new Vector3(+halfX, +halfZ, 0f),
                new Vector3(-halfX, +halfZ, 0f),
            });
            line.widthMultiplier = stripWidth;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.TransformZ;
            line.sharedMaterial = _stripMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

            // ---- Inner halo rim (soft spill along the 4 edges) ----
            if (rimGlow)
            {
                float rimW = Mathf.Max(3f, Mathf.Min(size.x, size.z) * rimWidthFactor);
                // Rim quads lie flat just under the strips, inset half their
                // width from each edge so they hug the boundary from inside.
                Vector3[] rimCenters = {
                    new Vector3(center.x - halfX + rimW * 0.5f, glowY, center.z),  // X- edge
                    new Vector3(center.x + halfX - rimW * 0.5f, glowY, center.z),  // X+ edge
                    new Vector3(center.x, glowY, center.z - halfZ + rimW * 0.5f),  // Z- edge
                    new Vector3(center.x, glowY, center.z + halfZ - rimW * 0.5f),  // Z+ edge
                };
                // Quads on the X edges run along Z; with these rotations the
                // quad's v-axis (texture Y) always points along the WORLD axis
                // leading INTO the zone (+X for X edges, +Z for Z edges).
                Vector3[] rimSizes = {
                    new Vector3(rimW, size.z, 1f),
                    new Vector3(rimW, size.z, 1f),
                    new Vector3(size.x, rimW, 1f),
                    new Vector3(size.x, rimW, 1f),
                };
                Quaternion[] rimRots = {
                    Quaternion.Euler(90f, 0f, -90f),
                    Quaternion.Euler(90f, 0f, -90f),
                    Quaternion.Euler(90f, 0f, 0f),
                    Quaternion.Euler(90f, 0f, 0f),
                };
                // v=1 then lands at +X/+Z — the OUTER side on X+/Z+ edges and
                // the INNER side on X-/Z-. Flip V on the latter pair so the
                // bright end of the gradient always sits at the outer boundary.
                bool[] rimFlipV = { true, false, true, false };

                _rimMat = new Material(unlitShader) { name = "ZoneGlowRim_Runtime" };
                _rimMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, rimAlpha);
                _rimMat.SetFloat("_Cull", 0f);
                _rimMat.SetFloat("_Mode", 3);
                if (softEdges)
                {
                    _rimTex = MakeRimTexture();
                    _rimMat.mainTexture = _rimTex;
                }

                for (int i = 0; i < 4; i++)
                {
                    var rimGO = new GameObject($"GlowRim_{i}");
                    rimGO.transform.SetParent(_root.transform, false);
                    rimGO.transform.localPosition = rimCenters[i];
                    rimGO.transform.localRotation = rimRots[i];
                    rimGO.transform.localScale = rimSizes[i];

                    var rFilter = rimGO.AddComponent<MeshFilter>();
                    rFilter.sharedMesh = GetQuadMesh();
                    var rRenderer = rimGO.AddComponent<MeshRenderer>();
                    rRenderer.sharedMaterial = _rimMat;
                    rRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rRenderer.receiveShadows = false;
                    if (rimFlipV[i])
                    {
                        var mpb = new MaterialPropertyBlock();
                        mpb.SetVector("_MainTex_ST", new Vector4(1f, -1f, 0f, 0f));
                        rRenderer.SetPropertyBlock(mpb);
                    }
                }
            }
    }

    private void Update()
    {
        // Periodic presence check (mirrors ChapterBoundary.Update): the glow
        // shines at full strength while the player is OUTSIDE the zone and dims
        // to insideDimFactor while they're inside. A plain OnTriggerEnter is not
        // enough — teleports (chapter transitions, respawns) bypass trigger
        // events, so we poll the player's position every second.
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

        // Push the current dim state into the visuals every frame (cheap, also
        // handles the case where no pulse is configured).
        float dim = _playerInside ? insideDimFactor : 1f;
        float targetAlpha = stripAlpha * dim;
        float targetRimAlpha = rimAlpha * dim;
        float targetIntensity = lightIntensity * dim;

        if (pulse)
        {
            _animTime += Time.deltaTime * pulseSpeed;
            float pulseScale = 1f + Mathf.Sin(_animTime) * pulseAmount;
            targetAlpha *= pulseScale;
            targetRimAlpha *= pulseScale;
            targetIntensity *= pulseScale;
        }

        // Pulse ground strips so the painted edges breathe.
        if (_stripMat != null)
            _stripMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, targetAlpha);

        // The halo rim breathes in sync with the strips.
        if (_rimMat != null)
            _rimMat.color = new Color(glowColor.r, glowColor.g, glowColor.b, targetRimAlpha);

        // Pulse corner light intensity.
        if (_cornerLights != null)
        {
            foreach (var l in _cornerLights)
            {
                if (l != null) l.intensity = targetIntensity;
            }
        }
    }
}
