using System.Collections;
using UnityEngine;

/// <summary>
/// Icon displayed on the floating beacon marker. Auto picks an icon based on
/// the beacon's activation source (chapter = house, side quest = book, main
/// quest = skull/exclamation) unless overridden.
/// </summary>
public enum BeaconIconType
{
    Auto,
    Skull,
    Book,
    House,
    Exclamation,
    None
}

/// <summary>
/// World-space waypoint beacon that guides the player to the next quest
/// objective. Shows a glowing ground ring + a floating downward-pointing arrow
/// so the player can spot the destination from anywhere in the chapter.
///
/// The beacon auto-activates when its `showOnQuest` becomes the active quest
/// (or when `showOnChapter` matches the current chapter and no quest is
/// assigned — useful for guiding the player to the save room on chapter entry).
/// It auto-deactivates when the quest is completed or the chapter advances.
///
/// Place one QuestBeacon per key objective location. The beacon is purely
/// visual (no collider, no raycast target) so it never blocks gameplay.
/// </summary>
public class QuestBeacon : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Quest that activates this beacon. When this quest is active, the beacon shows. " +
             "Leave null to use showOnChapter instead.")]
    public QuestData showOnQuest;

    [Tooltip("If showOnQuest is null, show the beacon while the current chapter matches this number. " +
             "Useful for guiding the player to the save room when they first enter a chapter.")]
    public int showOnChapter = 0;

    [Tooltip("Side quest that activates this beacon. When this side quest is active in " +
             "SideQuestManager, the beacon shows. Used for side-quest objective markers " +
             "(typically green to distinguish from main-quest gold beacons).")]
    public QuestData showOnSideQuest;

    [Header("Follower Recruitment (Ch3)")]
    [Tooltip("If > 0, this beacon is manually activated/deactivated by CompanionManager " +
             "based on the follower recruitment arc stage. 2 = show while shop supplies " +
             "are being collected (Stage 2 active), 3 = show during zombie siege. " +
             "CompanionManager calls SetFollowerActive(true/false) to toggle. " +
             "Ignored if 0.")]
    public int showOnFollowerStage = 0;

    [Tooltip("Manual override flag set by CompanionManager. When true (and showOnFollowerStage > 0), " +
             "the beacon shows regardless of quest/chapter state. Read-only at runtime.")]
    public bool followerActive = false;

    [Tooltip("If true, hide the beacon once the player gets within this distance.")]
    public bool hideWhenClose = true;

    [Tooltip("Distance at which the beacon hides (if hideWhenClose is true).")]
    public float hideDistance = 4f;

    [Header("Ground Snap")]
    [Tooltip("If true, raycast downward from the beacon position to find the ground and snap all visuals to it. " +
             "Prevents the beacon from floating when the beacon origin is above the ground.")]
    public bool snapToGround = true;

    [Tooltip("How high above the beacon origin to start the ground raycast.")]
    public float groundRaycastStart = 5f;

    [Tooltip("How far below to raycast for the ground.")]
    public float groundRaycastDistance = 20f;

    [Tooltip("Layer mask for ground detection. Leave 0 to use default (everything).")]
    public LayerMask groundMask = 0;

    [Header("Ground Ring")]
    [Tooltip("Show a glowing pulsing ring on the ground.")]
    public bool showGroundRing = true;

    [Tooltip("Ground ring radius.")]
    public float ringRadius = 2f;

    [Tooltip("Ground ring color.")]
    public Color ringColor = new Color(1f, 0.85f, 0.3f, 0.6f);

    [Tooltip("Add an expanding ripple particle effect on the ground ring.")]
    public bool ringParticles = true;

    [Tooltip("Add a point light at the ring for ambient glow.")]
    public bool ringLight = true;

    [Header("Floating Arrow")]
    [Tooltip("Height of the floating arrow above the ground.")]
    public float arrowHeight = 4f;

    [Tooltip("Arrow bob amplitude.")]
    public float arrowBobAmplitude = 0.5f;

    [Tooltip("Arrow bob speed.")]
    public float arrowBobSpeed = 2f;

    [Tooltip("Arrow size (world units).")]
    public float arrowSize = 1.5f;

    [Tooltip("Arrow color.")]
    public Color arrowColor = new Color(1f, 0.9f, 0.4f, 1f);

    [Tooltip("Icon color (sprite above the arrow).")]
    public Color iconColor = new Color(1f, 0.9f, 0.4f, 1f);

    [Tooltip("Icon to display on the floating marker. Auto picks based on activation source " +
             "(chapter = house, side quest = book, main quest = skull).")]
    public BeaconIconType iconType = BeaconIconType.Auto;

    // ---- Backward-compatible aliases (used by StoryChapterBuilder editor scripts) ----
    /// <summary>Alias for arrowColor. Old builder scripts set beamColor.</summary>
    public Color beamColor
    {
        get => arrowColor;
        set { arrowColor = value; ringColor = value; }
    }

    /// <summary>Alias for arrowHeight. Old builder scripts set beamHeight.</summary>
    public float beamHeight
    {
        get => arrowHeight;
        set => arrowHeight = value;
    }

    private GameObject _ring;
    private GameObject _ringParticles;
    private GameObject _arrow;
    private GameObject _icon;
    private Light _ringLight;
    private Material _ringMat;
    private Material _arrowMat;
    private Material _iconMat;
    private bool _active;
    private float _bobTime;
    private Transform _player;
    private float _groundY; // Y of ground at beacon position

    // Cached base materials loaded once from Resources/Assets.
    private static Material _baseRingParticleMat;

    private void OnEnable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged += HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
        }
        if (SideQuestManager.Instance != null)
        {
            SideQuestManager.Instance.OnSideQuestActivated += HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestCompleted += HandleSideQuestChanged;
        }
        BuildVisuals();
        EvaluateActivation();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
        if (SideQuestManager.Instance != null)
        {
            SideQuestManager.Instance.OnSideQuestActivated -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestCompleted -= HandleSideQuestChanged;
        }
    }

    private void OnDestroy()
    {
        if (_ringMat != null) Destroy(_ringMat);
        if (_arrowMat != null) Destroy(_arrowMat);
        if (_iconMat != null) Destroy(_iconMat);
    }

    /// <summary>
    /// Resolve the effective icon type based on the Auto setting and the
    /// beacon's activation source (chapter, side quest, or main quest).
    /// </summary>
    private BeaconIconType ResolveIconType()
    {
        if (iconType != BeaconIconType.Auto) return iconType;

        if (showOnChapter > 0 && showOnQuest == null && showOnSideQuest == null)
            return BeaconIconType.House;
        if (showOnSideQuest != null)
            return BeaconIconType.Book;
        // Main quest: use skull for combat quests, exclamation for reach quests.
        // Simple heuristic: if the quest name contains "kill" or "bomb" use skull.
        if (showOnQuest != null && showOnQuest.title != null)
        {
            string t = showOnQuest.title.ToLower();
            if (t.Contains("bắn") || t.Contains("đối đầu") || t.Contains("dọn") || t.Contains("bomb"))
                return BeaconIconType.Skull;
        }
        return BeaconIconType.Exclamation;
    }

    /// <summary>
    /// Load the sprite for the given icon type from Resources/BeaconIcons.
    /// Returns null for None.
    /// </summary>
    private static Sprite LoadIconSprite(BeaconIconType type)
    {
        switch (type)
        {
            case BeaconIconType.Skull: return Resources.Load<Sprite>("BeaconIcons/icon_skull");
            case BeaconIconType.Book: return Resources.Load<Sprite>("BeaconIcons/icon_book");
            case BeaconIconType.House: return Resources.Load<Sprite>("BeaconIcons/icon_house");
            case BeaconIconType.Exclamation: return Resources.Load<Sprite>("BeaconIcons/icon_exclaim");
            default: return null;
        }
    }

    private void HandleQuestChanged(QuestData oldQuest, QuestData newQuest)
    {
        EvaluateActivation();
    }

    private void HandleChapterChanged(int oldCh, int newCh)
    {
        EvaluateActivation();
    }

    private void HandleSideQuestChanged(QuestData quest)
    {
        EvaluateActivation();
    }

    private void EvaluateActivation()
    {
        var sm = StoryManager.Instance;
        if (sm == null) { SetActive(false); return; }

        // Follower recruitment arc: manual override via followerActive flag.
        // When CompanionManager sets followerActive = true, show the beacon
        // regardless of quest/chapter state.
        if (showOnFollowerStage > 0)
        {
            SetActive(followerActive);
            return;
        }

        // Priority: side quest > main quest > chapter
        if (showOnSideQuest != null)
        {
            var sqm = SideQuestManager.Instance;
            bool active = sqm != null && sqm.IsActive(showOnSideQuest) && !sqm.IsCompleted(showOnSideQuest);
            SetActive(active);
            return;
        }

        if (showOnQuest != null)
        {
            // Show when this quest is the active quest.
            SetActive(sm.ActiveQuest == showOnQuest);
        }
        else if (showOnChapter > 0)
        {
            // Show when the current chapter matches and no quest-specific beacon
            // is handling things. This guides the player to the save room on entry.
            SetActive(sm.CurrentChapter == showOnChapter);
        }
        else
        {
            SetActive(false);
        }
    }

    /// <summary>
    /// Manually activates/deactivates this beacon for the follower recruitment arc.
    /// Only effective if showOnFollowerStage > 0. Called by CompanionManager.
    /// </summary>
    public void SetFollowerActive(bool active)
    {
        if (showOnFollowerStage <= 0) return;
        followerActive = active;
        EvaluateActivation();
    }

    private void SetActive(bool active)
    {
        _active = active;
        if (_ring != null) _ring.SetActive(active);
        if (_ringParticles != null) _ringParticles.SetActive(active);
        if (_arrow != null) _arrow.SetActive(active);
        if (_icon != null) _icon.SetActive(active);
        if (_ringLight != null) _ringLight.enabled = active;
    }

    /// <summary>
    /// Raycast downward from the beacon position to find the ground Y.
    /// Falls back to the beacon's own Y if no ground is hit.
    /// </summary>
    private float FindGroundY()
    {
        if (!snapToGround) return transform.position.y;

        Vector3 origin = transform.position + Vector3.up * groundRaycastStart;
        int mask = groundMask.value != 0 ? groundMask.value : ~0;
        // Ignore triggers when finding ground.
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundRaycastDistance, mask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return transform.position.y;
    }

    private void BuildVisuals()
    {
        // Destroy any existing beacon visuals so BuildVisuals is idempotent.
        if (_ring != null)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Beacon_"))
                    DestroyImmediate(child.gameObject);
            }
            _ring = null;
            _arrow = null;
            _icon = null;
            _ringParticles = null;
            _ringLight = null;
        }

        Shader unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");

        // Find ground Y once so all visuals are placed on the ground.
        _groundY = FindGroundY();

        // Load base materials once (cached statically so all beacons share the source).
        if (_baseRingParticleMat == null)
        {
            _baseRingParticleMat = Resources.Load<Material>("VFX/Beacon/BeaconRingParticle");
            if (_baseRingParticleMat == null && unlitShader != null)
                _baseRingParticleMat = new Material(unlitShader);
        }

        // Reusable primitive meshes (avoid CreatePrimitive which leaks GameObjects).
        Mesh cylinderMesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
        Mesh coneMesh = GetConeMesh();
        Mesh quadMesh = GetPrimitiveMesh(PrimitiveType.Quad);

        // ---- Ground ring (flat hollow-looking cylinder with pulse) ----
        if (showGroundRing)
        {
            _ring = new GameObject("Beacon_Ring");
            _ring.transform.SetParent(transform, false);
            _ring.transform.localPosition = new Vector3(0f, _groundY - transform.position.y + 0.05f, 0f);
            _ring.transform.localScale = new Vector3(ringRadius * 2f, 0.02f, ringRadius * 2f);

            var ringFilter = _ring.AddComponent<MeshFilter>();
            ringFilter.sharedMesh = cylinderMesh;
            var ringRenderer = _ring.AddComponent<MeshRenderer>();
            _ringMat = new Material(unlitShader) { name = "BeaconRing_Runtime" };
            _ringMat.color = ringColor;
            ringRenderer.material = _ringMat;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            // ---- Ring point light (ambient glow at ground) ----
            if (ringLight)
            {
                var lightGO = new GameObject("Beacon_Light");
                lightGO.transform.SetParent(transform, false);
                lightGO.transform.localPosition = new Vector3(0f, _groundY - transform.position.y + 0.5f, 0f);
                _ringLight = lightGO.AddComponent<Light>();
                _ringLight.type = LightType.Point;
                _ringLight.color = new Color(ringColor.r, ringColor.g, ringColor.b, 1f);
                _ringLight.intensity = 1.5f;
                _ringLight.range = 6f;
                _ringLight.shadows = LightShadows.None;
            }

            // ---- Ring particles (expanding ripple) ----
            if (ringParticles && _baseRingParticleMat != null)
            {
                _ringParticles = new GameObject("Beacon_RingParticles");
                _ringParticles.transform.SetParent(transform, false);
                _ringParticles.transform.localPosition = new Vector3(0f, _groundY - transform.position.y + 0.1f, 0f);
                var rps = _ringParticles.AddComponent<ParticleSystem>();
                var rmain = rps.main;
                rmain.loop = true;
                rmain.startLifetime = 2f;
                rmain.startSpeed = 0f;
                rmain.startSize = 0.1f;
                rmain.startColor = new Color(ringColor.r, ringColor.g, ringColor.b, 0.6f);
                rmain.maxParticles = 30;
                rmain.simulationSpace = ParticleSystemSimulationSpace.Local;
                var remit = rps.emission;
                remit.rateOverTime = 5f;
                var rshape = rps.shape;
                rshape.shapeType = ParticleSystemShapeType.Circle;
                rshape.radius = 0.1f;
                // Size grows over lifetime (ripple outward)
                var rsize = rps.sizeOverLifetime;
                rsize.enabled = true;
                rsize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.1f), new Keyframe(1f, ringRadius * 2f)));
                // Fade out
                var rcol = rps.colorOverLifetime;
                rcol.enabled = true;
                var rgrad = new Gradient();
                rgrad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(ringColor, 0f), new GradientColorKey(ringColor, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
                rcol.color = rgrad;
                var rrend = _ringParticles.GetComponent<ParticleSystemRenderer>();
                rrend.material = _baseRingParticleMat;
            }
        }

        // ---- Floating arrow (cone pointing down, bobbing) ----
        _arrow = new GameObject("Beacon_Arrow");
        _arrow.transform.SetParent(transform, false);
        _arrow.transform.localPosition = new Vector3(0f, _groundY - transform.position.y + arrowHeight, 0f);
        _arrow.transform.localScale = new Vector3(arrowSize, arrowSize, arrowSize);
        // Cone primitive points up by default; rotate 180° so it points down.
        _arrow.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);

        var arrowFilter = _arrow.AddComponent<MeshFilter>();
        arrowFilter.sharedMesh = coneMesh;
        var arrowRenderer = _arrow.AddComponent<MeshRenderer>();
        _arrowMat = new Material(unlitShader) { name = "BeaconArrow_Runtime" };
        _arrowMat.color = arrowColor;
        arrowRenderer.material = _arrowMat;
        arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arrowRenderer.receiveShadows = false;

        // ---- Optional icon sprite above the arrow ----
        BeaconIconType resolvedIcon = ResolveIconType();
        Sprite iconSprite = LoadIconSprite(resolvedIcon);
        if (resolvedIcon != BeaconIconType.None && iconSprite != null)
        {
            _icon = new GameObject("Beacon_Icon");
            _icon.transform.SetParent(transform, false);
            _icon.transform.localPosition = new Vector3(0f, _groundY - transform.position.y + arrowHeight + 1.2f, 0f);
            _icon.transform.localScale = new Vector3(arrowSize * 0.8f, arrowSize * 0.8f, arrowSize * 0.8f);

            var iconFilter = _icon.AddComponent<MeshFilter>();
            iconFilter.sharedMesh = quadMesh;
            var iconRenderer = _icon.AddComponent<MeshRenderer>();
            _iconMat = new Material(unlitShader) { name = "BeaconIcon_Runtime" };
            _iconMat.color = iconColor;
            _iconMat.SetTexture("_MainTex", iconSprite.texture);
            iconRenderer.material = _iconMat;
            iconRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            iconRenderer.receiveShadows = false;
        }
    }

    /// <summary>
    /// Returns the built-in primitive mesh without creating a GameObject.
    /// Uses a temporary primitive once and caches the mesh statically so
    /// subsequent calls don't leak GameObjects into the scene.
    /// </summary>
    private static Mesh _cylinderMesh;
    private static Mesh _coneMesh;
    private static Mesh _quadMesh;
    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        if (type == PrimitiveType.Cylinder && _cylinderMesh != null) return _cylinderMesh;
        if (type == PrimitiveType.Quad && _quadMesh != null) return _quadMesh;

        var temp = GameObject.CreatePrimitive(type);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        if (type == PrimitiveType.Cylinder) _cylinderMesh = mesh;
        else if (type == PrimitiveType.Quad) _quadMesh = mesh;
        return mesh;
    }

    /// <summary>
    /// Creates a cone mesh (pointing up, base at origin) procedurally.
    /// Unity has no built-in Cone primitive, so we build one with 16 segments.
    /// </summary>
    private static Mesh GetConeMesh()
    {
        if (_coneMesh != null) return _coneMesh;

        int segments = 16;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var normals = new System.Collections.Generic.List<Vector3>();

        float height = 1f;
        float radius = 0.5f;

        // Apex
        vertices.Add(new Vector3(0f, height, 0f));
        normals.Add(Vector3.up);

        // Base ring
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            normals.Add(Vector3.up);
        }

        // Side triangles (apex + 2 base verts)
        for (int i = 0; i < segments; i++)
        {
            int apex = 0;
            int a = 1 + i;
            int b = 1 + (i + 1) % segments;
            triangles.Add(apex);
            triangles.Add(b);
            triangles.Add(a);
        }

        // Base center
        int baseCenter = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, 0f));
        normals.Add(Vector3.down);

        // Base triangles
        for (int i = 0; i < segments; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % segments;
            triangles.Add(baseCenter);
            triangles.Add(a);
            triangles.Add(b);
        }

        _coneMesh = new Mesh { name = "BeaconCone" };
        _coneMesh.SetVertices(vertices);
        _coneMesh.SetTriangles(triangles, 0);
        _coneMesh.SetNormals(normals);
        _coneMesh.RecalculateBounds();
        return _coneMesh;
    }

    private void EnsurePlayer()
    {
        if (_player != null) return;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
    }

    private void Update()
    {
        if (!_active) return;

        EnsurePlayer();

        // Bob the arrow + icon.
        _bobTime += Time.deltaTime * arrowBobSpeed;
        float bob = Mathf.Sin(_bobTime) * arrowBobAmplitude;
        float groundOffset = _groundY - transform.position.y;

        if (_arrow != null)
        {
            _arrow.transform.localPosition = new Vector3(0f, groundOffset + arrowHeight + bob, 0f);
        }

        if (_icon != null)
        {
            _icon.transform.localPosition = new Vector3(0f, groundOffset + arrowHeight + 1.2f + bob, 0f);

            // Face the camera (billboard).
            if (Camera.main != null)
            {
                _icon.transform.rotation = Quaternion.LookRotation(
                    _icon.transform.position - Camera.main.transform.position);
            }
        }

        // Pulse the ring.
        if (_ring != null)
        {
            float pulse = 1f + Mathf.Sin(_bobTime * 1.5f) * 0.15f;
            _ring.transform.localScale = new Vector3(ringRadius * 2f * pulse, 0.02f, ringRadius * 2f * pulse);
        }

        // Hide when player is close.
        if (hideWhenClose && _player != null)
        {
            float dist = Vector3.Distance(
                new Vector3(_player.position.x, 0f, _player.position.z),
                new Vector3(transform.position.x, 0f, transform.position.z));
            bool show = dist > hideDistance;
            if (_ring != null) _ring.SetActive(show);
            if (_ringParticles != null) _ringParticles.SetActive(show);
            if (_arrow != null) _arrow.SetActive(show);
            if (_icon != null) _icon.SetActive(show);
            if (_ringLight != null) _ringLight.enabled = show;
        }
    }
}
