using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Day/night lighting system for story mode.
///
/// Combines two behaviors:
/// - Per-chapter time-of-day anchor: when StoryManager.OnChapterChanged fires,
///   the cycle jumps to a configurable start hour for that chapter (e.g. Ch1
///   starts at dawn, Ch4 starts at night). This gives each chapter a distinct
///   narrative mood.
/// - Continuous cycle: after the chapter anchor is set, time advances at
///   `cycleSpeed` game-hours per real second, so lighting keeps drifting while
///   the player is in the chapter. Set `cycleSpeed = 0` to freeze time.
///
/// Lighting is driven by an array of `DayNightKeyframe`s sorted by hour. The
/// system linearly interpolates between the two surrounding keyframes every
/// frame and applies the result to:
/// - The sun DirectionalLight (rotation, color, intensity, shadows)
/// - An optional fill DirectionalLight (color, intensity)
/// - RenderSettings (fog enabled/color/density, ambient color/intensity)
/// - The SyntyStudios/SkyGradient skydome material (_ColorTop, _ColorBottom)
/// - Two PostProcess volumes (day/night weight)
///
/// All visual state is driven from a single source of truth (this component)
/// so designers can tune the entire day/night look from one Inspector.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance;

    [System.Serializable]
    public struct DayNightKeyframe
    {
        [Tooltip("Hour of day in 24h format (0-24). Keyframes should be sorted ascending by hour.")]
        [Range(0f, 24f)]
        public float hour;

        [Header("Sun (main Directional Light)")]
        [Tooltip("Sun local Euler X rotation (pitch). 0 = horizon, 90 = overhead, negative or >180 = below horizon (night).")]
        public float sunEulerX;

        [Tooltip("Sun light color at this hour.")]
        public Color sunColor;

        [Tooltip("Sun light intensity at this hour. 0 = sun off (deep night).")]
        public float sunIntensity;

        [Header("Fill Light (secondary Directional Light)")]
        [Tooltip("Fill light color at this hour.")]
        public Color fillColor;

        [Tooltip("Fill light intensity at this hour.")]
        public float fillIntensity;

        [Header("Fog")]
        [Tooltip("Enable RenderSettings.fog at this hour.")]
        public bool fogEnabled;

        [Tooltip("RenderSettings.fog color at this hour.")]
        public Color fogColor;

        [Tooltip("RenderSettings.fog density at this hour (exponential fog).")]
        [Range(0f, 0.2f)]
        public float fogDensity;

        [Header("Ambient")]
        [Tooltip("RenderSettings.ambientLight color at this hour.")]
        public Color ambientColor;

        [Tooltip("RenderSettings.ambientIntensity at this hour.")]
        [Range(0f, 4f)]
        public float ambientIntensity;

        [Header("Skydome (SyntyStudios/SkyGradient)")]
        [Tooltip("Skydome material _ColorTop at this hour.")]
        public Color skyTopColor;

        [Tooltip("Skydome material _ColorBottom at this hour.")]
        public Color skyBottomColor;

        [Header("Post Processing")]
        [Tooltip("Weight (0-1) of the Day PostProcess volume at this hour. Night weight is 1 - dayWeight.")]
        [Range(0f, 1f)]
        public float dayVolumeWeight;
    }

    [Header("Lights")]
    [Tooltip("The main DirectionalLight that acts as the sun. If null, falls back to RenderSettings.sun / first directional light.")]
    public Light sunLight;

    [Tooltip("Secondary DirectionalLight used as fill (e.g. moonlight at night). Optional.")]
    public Light fillLight;

    [Header("Skydome")]
    [Tooltip("MeshRenderer whose material uses SyntyStudios/SkyGradient shader. Its _ColorTop/_ColorBottom are driven by keyframes. Optional.")]
    public MeshRenderer skydomeRenderer;

    [Tooltip("Material property name for the top color of the sky gradient.")]
    public string skyTopColorProp = "_ColorTop";

    [Tooltip("Material property name for the bottom color of the sky gradient.")]
    public string skyBottomColorProp = "_ColorBottom";

    [Header("Time")]
    [Tooltip("Current time of day in 24h format (0-24). Driven by the cycle; can be set manually for debugging.")]
    [Range(0f, 24f)]
    public float timeOfDay = 6f;

    [Tooltip("Game-hours elapsed per real second. 0.1 = a full 24h day takes 240s. Set 0 to freeze time.")]
    [Range(0f, 2f)]
    public float cycleSpeed = 0.1f;

    [Header("Chapter Anchors")]
    [Tooltip("Start hour for each chapter (index 0 = Ch1). When OnChapterChanged fires, timeOfDay jumps here. Length must match number of chapters (5).")]
    public float[] chapterStartHours = new float[]
    {
        6f,   // Ch1 — dawn
        12f,  // Ch2 — noon
        18f,  // Ch3 — dusk
        22f,  // Ch4 — night
        2f    // Ch5 — deep night
    };

    [Tooltip("If true, snap timeOfDay instantly when a chapter changes. If false, blend over chapterBlendDuration seconds.")]
    public bool snapOnChapterChange = false;

    [Tooltip("Duration of the smooth blend when snapOnChapterChange is false.")]
    public float chapterBlendDuration = 4f;

    [Header("Keyframes")]
    [Tooltip("Day/night keyframes sorted ascending by hour. The cycle wraps from the last back to the first.")]
    public DayNightKeyframe[] keyframes = new DayNightKeyframe[]
    {
        new DayNightKeyframe // Dawn
        {
            hour = 6f,
            sunEulerX = 10f,
            sunColor = new Color(0.95f, 0.75f, 0.48f, 1f),
            sunIntensity = 0.75f,
            fillColor = new Color(0.6f, 0.6f, 0.8f, 1f),
            fillIntensity = 0.15f,
            fogEnabled = true,
            fogColor = new Color(0.46f, 0.3f, 0.26f, 1f),
            fogDensity = 0.01f,
            ambientColor = new Color(0.35f, 0.32f, 0.28f, 1f),
            ambientIntensity = 1f,
            skyTopColor = new Color(0.73f, 0.38f, 0.38f, 1f),
            skyBottomColor = new Color(0.88f, 0.71f, 0.51f, 1f),
            dayVolumeWeight = 0.7f
        },
        new DayNightKeyframe // Noon
        {
            hour = 12f,
            sunEulerX = 90f,
            sunColor = new Color(0.92f, 0.90f, 0.84f, 1f),
            sunIntensity = 0.85f,
            fillColor = new Color(0.7f, 0.75f, 0.85f, 1f),
            fillIntensity = 0.2f,
            fogEnabled = true,
            fogColor = new Color(0.6f, 0.65f, 0.7f, 1f),
            fogDensity = 0.005f,
            ambientColor = new Color(0.5f, 0.5f, 0.5f, 1f),
            ambientIntensity = 1.1f,
            skyTopColor = new Color(0.35f, 0.55f, 0.85f, 1f),
            skyBottomColor = new Color(0.75f, 0.85f, 0.95f, 1f),
            dayVolumeWeight = 1f
        },
        new DayNightKeyframe // Dusk
        {
            hour = 18f,
            sunEulerX = 170f,
            sunColor = new Color(0.90f, 0.52f, 0.28f, 1f),
            sunIntensity = 0.70f,
            fillColor = new Color(0.5f, 0.4f, 0.6f, 1f),
            fillIntensity = 0.25f,
            fogEnabled = true,
            fogColor = new Color(0.5f, 0.28f, 0.2f, 1f),
            fogDensity = 0.012f,
            ambientColor = new Color(0.3f, 0.22f, 0.2f, 1f),
            ambientIntensity = 0.9f,
            skyTopColor = new Color(0.6f, 0.25f, 0.3f, 1f),
            skyBottomColor = new Color(0.9f, 0.5f, 0.25f, 1f),
            dayVolumeWeight = 0.4f
        },
        new DayNightKeyframe // Night
        {
            hour = 22f,
            sunEulerX = 220f,
            sunColor = new Color(0.25f, 0.3f, 0.45f, 1f),
            sunIntensity = 0.15f,
            fillColor = new Color(0.3f, 0.35f, 0.55f, 1f),
            fillIntensity = 0.3f,
            fogEnabled = true,
            fogColor = new Color(0.08f, 0.09f, 0.12f, 1f),
            fogDensity = 0.02f,
            ambientColor = new Color(0.1f, 0.12f, 0.18f, 1f),
            ambientIntensity = 0.5f,
            skyTopColor = new Color(0.03f, 0.04f, 0.08f, 1f),
            skyBottomColor = new Color(0.1f, 0.12f, 0.2f, 1f),
            dayVolumeWeight = 0f
        },
        new DayNightKeyframe // Deep night wrap point
        {
            hour = 24f,
            sunEulerX = 350f,
            sunColor = new Color(0.2f, 0.25f, 0.4f, 1f),
            sunIntensity = 0.1f,
            fillColor = new Color(0.25f, 0.3f, 0.5f, 1f),
            fillIntensity = 0.25f,
            fogEnabled = true,
            fogColor = new Color(0.06f, 0.07f, 0.1f, 1f),
            fogDensity = 0.022f,
            ambientColor = new Color(0.08f, 0.1f, 0.16f, 1f),
            ambientIntensity = 0.45f,
            skyTopColor = new Color(0.02f, 0.03f, 0.06f, 1f),
            skyBottomColor = new Color(0.08f, 0.1f, 0.18f, 1f),
            dayVolumeWeight = 0f
        }
    };

    [Header("Post Processing")]
    [Tooltip("Day PostProcess volume (global). Weight is driven by keyframes.")]
    public PostProcessVolume dayVolume;

    [Tooltip("Night PostProcess volume (global). Weight is 1 - dayVolumeWeight.")]
    public PostProcessVolume nightVolume;

    [Header("Debug")]
    public bool logChapterChanges = true;

    [Header("Post Processing Support")]
    public PostProcessResources postProcessResources;

    // ---- Blend state for smooth chapter transitions ----
    private bool _blending;
    private float _blendFromTime;
    private float _blendToTime;
    private float _blendElapsed;

    // ---- Cached skydome material instance (MaterialPropertyBlock avoids touching the shared asset) ----
    private MaterialPropertyBlock _skyBlock;

    // ---- Cached sun base yaw (azimuth) captured at Awake so we can drive pitch
    // (X) without inheriting the unstable Y/Z euler representation that flips
    // when X crosses 90°/180° (gimbal lock). Setting localRotation via
    // Quaternion.Euler(x, yaw, 0) every frame keeps the sun direction stable. ----
    private float _sunBaseYaw;

    private PostProcessProfile _runtimeDayProfile;
    private PostProcessProfile _runtimeNightProfile;
    private Camera _cachedMainCam;
    private bool _isCameraPpInitialized = false;

    private void OnDestroy()
    {
        if (_runtimeDayProfile != null)
        {
            if (Application.isPlaying) Destroy(_runtimeDayProfile);
            else DestroyImmediate(_runtimeDayProfile);
            _runtimeDayProfile = null;
        }
        if (_runtimeNightProfile != null)
        {
            if (Application.isPlaying) Destroy(_runtimeNightProfile);
            else DestroyImmediate(_runtimeNightProfile);
            _runtimeNightProfile = null;
        }
    }

    private void ConfigureVolumeProfile(PostProcessVolume volume, ref PostProcessProfile runtimeProfile, float postExposureVal)
    {
        if (volume == null) return;

        if (runtimeProfile != null)
        {
            if (Application.isPlaying) Destroy(runtimeProfile);
            else DestroyImmediate(runtimeProfile);
            runtimeProfile = null;
        }

        if (volume.sharedProfile != null)
            runtimeProfile = Instantiate(volume.sharedProfile);
        else
            runtimeProfile = ScriptableObject.CreateInstance<PostProcessProfile>();

        Bloom bloom = runtimeProfile.GetSetting<Bloom>();
        if (bloom == null) bloom = runtimeProfile.AddSettings<Bloom>();
        bloom.enabled.Override(true);
        bloom.threshold.Override(1.20f); // 1.20f threshold in FP16 HDR space (0% diffuse bloom haze)
        bloom.intensity.Override(0.04f); // Ultra-soft 4% ambient glow
        bloom.clamp.Override(2.0f);      // Tight 2.0f specular bloom ceiling

        ColorGrading colorGrading = runtimeProfile.GetSetting<ColorGrading>();
        if (colorGrading == null) colorGrading = runtimeProfile.AddSettings<ColorGrading>();
        colorGrading.enabled.Override(true);
        colorGrading.gradingMode.Override(GradingMode.HighDefinitionRange); // Mandatory for ACES Tonemapping!
        colorGrading.tonemapper.Override(Tonemapper.ACES);                   // Filmic ACES Tonemapping compresses HDR highlights cleanly!
        colorGrading.postExposure.Override(postExposureVal);                 // Day: -0.40 EV, Night: -0.70 EV

        volume.profile = runtimeProfile; // Isolated runtime profile instance
        volume.isGlobal = true;
        volume.priority = 100f;          // Equal priority 100f for zero-cost weight blending
        volume.gameObject.SetActive(true); // Active 24/7 for zero-flicker weight blending
    }

    private void EnsurePostProcessResourcesLoaded()
    {
        if (postProcessResources == null)
        {
            postProcessResources = Resources.Load<PostProcessResources>("PostProcessResources");
        }
        if (postProcessResources == null)
        {
            var loadedRes = Resources.FindObjectsOfTypeAll<PostProcessResources>();
            if (loadedRes != null && loadedRes.Length > 0)
                postProcessResources = loadedRes[0];
        }
    }

    private void EnsureMainCameraConfigured()
    {
        Camera cam = Camera.main;
        if (cam == null || cam.orthographic) return;

        if (cam.name.Contains("Minimap") || cam.name.Contains("UI")) return;

        if (_cachedMainCam == cam && _isCameraPpInitialized) return;

        cam.allowHDR = true; // Mandatory FP16 HDR Target allocation for ACES Tonemapping!

        var ppLayer = cam.GetComponent<PostProcessLayer>();
        if (ppLayer == null)
        {
            ppLayer = cam.gameObject.AddComponent<PostProcessLayer>();
            ppLayer.volumeTrigger = cam.transform;
        }

        if (ppLayer != null)
        {
            ppLayer.enabled = true;
            if (ppLayer.volumeTrigger == null) ppLayer.volumeTrigger = cam.transform;

            int mask = 0;
            if (dayVolume != null) mask |= (1 << dayVolume.gameObject.layer);
            if (nightVolume != null) mask |= (1 << nightVolume.gameObject.layer);
            if (mask == 0) mask = 1 << gameObject.layer;
            ppLayer.volumeLayer = mask; // Pure Volume Layer Mask (excludes Layer 0 Default)

            EnsurePostProcessResourcesLoaded();
            if (postProcessResources != null)
            {
                ppLayer.Init(postProcessResources);
                _cachedMainCam = cam;
                _isCameraPpInitialized = true; // Atomic Init Guard: ONLY set cache once Init succeeds!
            }
        }

        var flareLayer = cam.GetComponent<FlareLayer>();
        if (flareLayer != null && flareLayer.enabled)
            flareLayer.enabled = false;
    }

    private void Awake()
    {
        Instance = this;

        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
            if (sunLight == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in lights)
                {
                    if (l.type == LightType.Directional) { sunLight = l; break; }
                }
            }
        }

        if (sunLight != null)
        {
            sunLight.type = LightType.Directional;
            sunLight.renderMode = LightRenderMode.ForcePixel;
            _sunBaseYaw = sunLight.transform.localEulerAngles.y;
        }

        _skyBlock = new MaterialPropertyBlock();
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

    private void Start()
    {
        Subscribe();
        ConfigureVolumeProfile(dayVolume, ref _runtimeDayProfile, -0.40f);
        ConfigureVolumeProfile(nightVolume, ref _runtimeNightProfile, -0.70f);
        EnsureMainCameraConfigured();
        ApplyEvaluatedState(Evaluate(timeOfDay));
    }

    private void Subscribe()
    {
        if (StoryManager.Instance == null) return;
        StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        if (newChapter < 1) return;
        int idx = newChapter - 1;
        if (chapterStartHours == null || idx < 0 || idx >= chapterStartHours.Length)
        {
            if (logChapterChanges)
                Debug.LogWarning($"[DayNightCycle] No chapterStartHours entry for chapter {newChapter}; leaving time as-is.");
            return;
        }

        float target = chapterStartHours[idx];
        if (logChapterChanges)
            Debug.Log($"[DayNightCycle] Chapter {oldChapter} -> {newChapter}: setting time-of-day to {target}h.");

        if (snapOnChapterChange || chapterBlendDuration <= 0f)
        {
            timeOfDay = target;
            _blending = false;
        }
        else
        {
            _blending = true;
            _blendFromTime = timeOfDay;
            _blendToTime = target;
            _blendElapsed = 0f;
        }
    }

    private void Update()
    {
        if (_blending)
        {
            _blendElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_blendElapsed / chapterBlendDuration);
            t = t * t * (3f - 2f * t);
            timeOfDay = Mathf.Lerp(_blendFromTime, _blendToTime, t);
            if (t >= 1f) _blending = false;
        }
        else if (cycleSpeed > 0f)
        {
            timeOfDay += cycleSpeed * Time.deltaTime;
            if (timeOfDay >= 24f) timeOfDay -= 24f;
            if (timeOfDay < 0f) timeOfDay += 24f;
        }

        var state = Evaluate(timeOfDay);
        ApplyEvaluatedState(state);
    }

    private DayNightKeyframe Evaluate(float hour)
    {
        if (keyframes == null || keyframes.Length == 0)
            return default;

        if (keyframes.Length == 1)
            return keyframes[0];

        int n = keyframes.Length;
        int i0 = -1, i1 = -1;
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float h0 = keyframes[i].hour;
            float h1 = keyframes[next].hour;
            if (h1 <= h0)
            {
                if (hour >= h0 || hour < h1) { i0 = i; i1 = next; break; }
            }
            else if (hour >= h0 && hour < h1)
            {
                i0 = i; i1 = next; break;
            }
        }

        if (i0 < 0)
            return keyframes[n - 1];

        var k0 = keyframes[i0];
        var k1 = keyframes[i1];

        float span = k1.hour - k0.hour;
        if (span <= 0f) span += 24f;
        float t = (hour - k0.hour);
        if (t < 0f) t += 24f;
        t = Mathf.Clamp01(t / span);

        return Lerp(k0, k1, t);
    }

    private static DayNightKeyframe Lerp(DayNightKeyframe a, DayNightKeyframe b, float t)
    {
        return new DayNightKeyframe
        {
            hour = Mathf.Lerp(a.hour, b.hour, t),
            sunEulerX = Mathf.Lerp(a.sunEulerX, b.sunEulerX, t),
            sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            fillColor = Color.Lerp(a.fillColor, b.fillColor, t),
            fillIntensity = Mathf.Lerp(a.fillIntensity, b.fillIntensity, t),
            fogEnabled = t < 0.5f ? a.fogEnabled : b.fogEnabled,
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
            ambientColor = Color.Lerp(a.ambientColor, b.ambientColor, t),
            ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
            skyTopColor = Color.Lerp(a.skyTopColor, b.skyTopColor, t),
            skyBottomColor = Color.Lerp(a.skyBottomColor, b.skyBottomColor, t),
            dayVolumeWeight = Mathf.Lerp(a.dayVolumeWeight, b.dayVolumeWeight, t)
        };
    }

    private void ApplyEvaluatedState(DayNightKeyframe s)
    {
        EnsureMainCameraConfigured();

        if (dayVolume != null)
        {
            dayVolume.gameObject.SetActive(true);
            dayVolume.weight = s.dayVolumeWeight;
        }
        if (nightVolume != null)
        {
            nightVolume.gameObject.SetActive(true);
            nightVolume.weight = 1f - s.dayVolumeWeight;
        }

        float sunScale = 0.65f;
        float ambScale = 0.70f;

        if (sunLight != null)
        {
            float rad = (s.sunEulerX - 90f) * Mathf.Deg2Rad;
            Vector3 sunDir = new Vector3(0f, -Mathf.Cos(rad), Mathf.Sin(rad));
            if (_sunBaseYaw != 0f)
                sunDir = Quaternion.AngleAxis(_sunBaseYaw, Vector3.up) * sunDir;
            Vector3 upRef = Mathf.Abs(Vector3.Dot(sunDir, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;
            sunLight.transform.localRotation = Quaternion.LookRotation(sunDir, upRef);
            sunLight.color = s.sunColor;
            sunLight.intensity = s.sunIntensity * sunScale;
            sunLight.flare = null;
            sunLight.shadows = (s.sunIntensity * sunScale) <= 0.05f ? LightShadows.None : LightShadows.Hard;
        }

        if (fillLight != null)
        {
            fillLight.color = s.fillColor;
            fillLight.intensity = s.fillIntensity * sunScale;
            fillLight.flare = null;
        }

        if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Trilight)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        float amb = s.ambientIntensity * ambScale;
        Color skyAmb = s.ambientColor * amb;
        RenderSettings.ambientSkyColor = skyAmb;
        RenderSettings.ambientEquatorColor = skyAmb * 0.75f;
        RenderSettings.ambientGroundColor = skyAmb * 0.50f;
        RenderSettings.ambientIntensity = 1.0f;
        RenderSettings.reflectionIntensity = Mathf.Clamp(amb * 0.25f, 0.03f, 0.12f);

        RenderSettings.fog = s.fogEnabled;
        RenderSettings.fogColor = s.fogColor;
        RenderSettings.fogDensity = s.fogDensity;

        if (skydomeRenderer != null)
        {
            if (_skyBlock == null) _skyBlock = new MaterialPropertyBlock();
            skydomeRenderer.GetPropertyBlock(_skyBlock);
            _skyBlock.SetColor(skyTopColorProp, s.skyTopColor * 0.75f);
            _skyBlock.SetColor(skyBottomColorProp, s.skyBottomColor * 0.75f);
            skydomeRenderer.SetPropertyBlock(_skyBlock);
        }
    }
}
