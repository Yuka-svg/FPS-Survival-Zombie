using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Day/night lighting system for story mode.
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
    public DayNightKeyframe[] keyframes;

    [Header("Post Processing")]
    [Tooltip("Day PostProcess volume (global). Weight is driven by keyframes.")]
    public PostProcessVolume dayVolume;

    [Tooltip("Night PostProcess volume (global). Weight is 1 - dayVolumeWeight.")]
    public PostProcessVolume nightVolume;

    [Header("Debug")]
    public bool logChapterChanges = true;

    [Header("Post Processing Support")]
    public PostProcessResources postProcessResources;

    public float CurrentDayWeight { get; private set; } = 1.0f;

    private bool _blending;
    private float _blendFromTime;
    private float _blendToTime;
    private float _blendElapsed;

    private MaterialPropertyBlock _skyBlock;
    private float _sunBaseYaw;
    [System.NonSerialized] private bool _hasCapturedSunYaw;

    private PostProcessProfile _runtimeDayProfile;
    private PostProcessProfile _runtimeNightProfile;
    [System.NonSerialized] private Camera _lastConfiguredCam;
    [System.NonSerialized] private bool _isCameraPpInitialized;

    private void Reset()
    {
        keyframes = GetDefaultKeyframes();
    }

    [ContextMenu("Reset to Default Keyframes")]
    public void ResetDefaultKeyframes()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Reset Default Keyframes");
#endif
        keyframes = GetDefaultKeyframes();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Upgrade Keyframes to 9-Grid (Fix Dark Scene)")]
    public void UpgradeKeyframesTo9Grid()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Upgrade Keyframes to 9-Grid");
#endif
        keyframes = GetDefaultKeyframes();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private DayNightKeyframe[] GetDefaultKeyframes()
    {
        return new DayNightKeyframe[]
        {
            new DayNightKeyframe { hour = 0f, sunEulerX = 300f, sunColor = new Color(0.20f, 0.25f, 0.40f, 1f), sunIntensity = 0.00f, fillColor = new Color(0.35f, 0.45f, 0.65f, 1f), fillIntensity = 0.25f, fogEnabled = true, fogColor = new Color(0.06f, 0.07f, 0.10f, 1f), fogDensity = 0.022f, ambientColor = new Color(0.35f, 0.40f, 0.58f, 1f), ambientIntensity = 0.85f, skyTopColor = new Color(0.03f, 0.04f, 0.10f, 1f), skyBottomColor = new Color(0.08f, 0.10f, 0.18f, 1f), dayVolumeWeight = 0.0f },
            new DayNightKeyframe { hour = 5f, sunEulerX = 350f, sunColor = new Color(0.95f, 0.75f, 0.48f, 1f), sunIntensity = 0.00f, fillColor = new Color(0.40f, 0.50f, 0.70f, 1f), fillIntensity = 0.28f, fogEnabled = true, fogColor = new Color(0.15f, 0.15f, 0.20f, 1f), fogDensity = 0.015f, ambientColor = new Color(0.38f, 0.45f, 0.60f, 1f), ambientIntensity = 0.90f, skyTopColor = new Color(0.10f, 0.12f, 0.20f, 1f), skyBottomColor = new Color(0.20f, 0.25f, 0.35f, 1f), dayVolumeWeight = 0.0f },
            new DayNightKeyframe { hour = 6f, sunEulerX = 10f, sunColor = new Color(0.95f, 0.78f, 0.52f, 1f), sunIntensity = 1.20f, fillColor = new Color(0.6f, 0.6f, 0.8f, 1f), fillIntensity = 0.00f, fogEnabled = true, fogColor = new Color(0.46f, 0.3f, 0.26f, 1f), fogDensity = 0.01f, ambientColor = new Color(0.55f, 0.50f, 0.45f, 1f), ambientIntensity = 1.15f, skyTopColor = new Color(0.73f, 0.38f, 0.38f, 1f), skyBottomColor = new Color(0.88f, 0.71f, 0.51f, 1f), dayVolumeWeight = 0.5f },
            new DayNightKeyframe { hour = 12f, sunEulerX = 90f, sunColor = new Color(0.95f, 0.93f, 0.88f, 1f), sunIntensity = 1.40f, fillColor = new Color(0.7f, 0.75f, 0.85f, 1f), fillIntensity = 0.00f, fogEnabled = true, fogColor = new Color(0.6f, 0.65f, 0.7f, 1f), fogDensity = 0.005f, ambientColor = new Color(0.65f, 0.65f, 0.65f, 1f), ambientIntensity = 1.25f, skyTopColor = new Color(0.35f, 0.55f, 0.85f, 1f), skyBottomColor = new Color(0.75f, 0.85f, 0.95f, 1f), dayVolumeWeight = 1.0f },
            new DayNightKeyframe { hour = 16f, sunEulerX = 140f, sunColor = new Color(0.95f, 0.75f, 0.45f, 1f), sunIntensity = 0.85f, fillColor = new Color(0.6f, 0.55f, 0.75f, 1f), fillIntensity = 0.00f, fogEnabled = true, fogColor = new Color(0.52f, 0.35f, 0.28f, 1f), fogDensity = 0.008f, ambientColor = new Color(0.58f, 0.48f, 0.40f, 1f), ambientIntensity = 1.10f, skyTopColor = new Color(0.55f, 0.35f, 0.55f, 1f), skyBottomColor = new Color(0.85f, 0.60f, 0.40f, 1f), dayVolumeWeight = 0.75f },
            new DayNightKeyframe { hour = 18f, sunEulerX = 170f, sunColor = new Color(0.90f, 0.52f, 0.28f, 1f), sunIntensity = 0.35f, fillColor = new Color(0.5f, 0.4f, 0.6f, 1f), fillIntensity = 0.00f, fogEnabled = true, fogColor = new Color(0.5f, 0.28f, 0.2f, 1f), fogDensity = 0.012f, ambientColor = new Color(0.48f, 0.38f, 0.35f, 1f), ambientIntensity = 0.85f, skyTopColor = new Color(0.6f, 0.25f, 0.3f, 1f), skyBottomColor = new Color(0.9f, 0.5f, 0.25f, 1f), dayVolumeWeight = 0.35f },
            new DayNightKeyframe { hour = 19f, sunEulerX = 185f, sunColor = new Color(0.80f, 0.40f, 0.25f, 1f), sunIntensity = 0.05f, fillColor = new Color(0.40f, 0.45f, 0.65f, 1f), fillIntensity = 0.25f, fogEnabled = true, fogColor = new Color(0.12f, 0.12f, 0.15f, 1f), fogDensity = 0.016f, ambientColor = new Color(0.38f, 0.42f, 0.58f, 1f), ambientIntensity = 0.90f, skyTopColor = new Color(0.08f, 0.08f, 0.16f, 1f), skyBottomColor = new Color(0.20f, 0.20f, 0.30f, 1f), dayVolumeWeight = 0.10f },
            new DayNightKeyframe { hour = 20f, sunEulerX = 210f, sunColor = new Color(0.30f, 0.32f, 0.50f, 1f), sunIntensity = 0.00f, fillColor = new Color(0.35f, 0.45f, 0.65f, 1f), fillIntensity = 0.25f, fogEnabled = true, fogColor = new Color(0.08f, 0.09f, 0.12f, 1f), fogDensity = 0.018f, ambientColor = new Color(0.35f, 0.40f, 0.58f, 1f), ambientIntensity = 0.85f, skyTopColor = new Color(0.04f, 0.05f, 0.12f, 1f), skyBottomColor = new Color(0.12f, 0.15f, 0.25f, 1f), dayVolumeWeight = 0.0f },
            new DayNightKeyframe { hour = 22f, sunEulerX = 240f, sunColor = new Color(0.25f, 0.28f, 0.45f, 1f), sunIntensity = 0.00f, fillColor = new Color(0.35f, 0.45f, 0.65f, 1f), fillIntensity = 0.25f, fogEnabled = true, fogColor = new Color(0.07f, 0.08f, 0.11f, 1f), fogDensity = 0.020f, ambientColor = new Color(0.35f, 0.40f, 0.58f, 1f), ambientIntensity = 0.85f, skyTopColor = new Color(0.035f, 0.045f, 0.11f, 1f), skyBottomColor = new Color(0.10f, 0.12f, 0.20f, 1f), dayVolumeWeight = 0.0f }
        };
    }

    private void Awake()
    {
        Instance = this;

        if (keyframes == null || keyframes.Length < 9)
        {
            keyframes = GetDefaultKeyframes();
        }

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
            if (!_hasCapturedSunYaw)
            {
                _sunBaseYaw = sunLight.transform.localEulerAngles.y;
                _hasCapturedSunYaw = true;
            }
        }

        _skyBlock = new MaterialPropertyBlock();

        ApplyEvaluatedState(Evaluate(timeOfDay));
    }

    private void OnDestroy()
    {
        if (dayVolume != null && dayVolume.profile == _runtimeDayProfile) dayVolume.profile = null;
        if (nightVolume != null && nightVolume.profile == _runtimeNightProfile) nightVolume.profile = null;

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

    private void ConfigureVolumeProfile(PostProcessVolume volume, ref PostProcessProfile runtimeProfile, float postExposureValue, float priorityValue)
    {
        if (volume == null) return;

        volume.isGlobal = true;
        volume.priority = priorityValue;

        if (runtimeProfile != null)
        {
            if (Application.isPlaying) Destroy(runtimeProfile);
            else DestroyImmediate(runtimeProfile);
            runtimeProfile = null;
        }

        if (volume.sharedProfile != null)
        {
            runtimeProfile = Instantiate(volume.sharedProfile);
        }
        else
        {
            runtimeProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
        }
        runtimeProfile.name = volume.name + "_RuntimeProfile";

        ColorGrading colorGrading = runtimeProfile.GetSetting<ColorGrading>();
        if (colorGrading == null)
        {
            colorGrading = runtimeProfile.AddSettings<ColorGrading>();
        }
        colorGrading.enabled.Override(true);
        colorGrading.gradingMode.Override(GradingMode.HighDefinitionRange);
        colorGrading.tonemapper.Override(Tonemapper.ACES);
        colorGrading.postExposure.Override(postExposureValue);

        Bloom bloom = runtimeProfile.GetSetting<Bloom>();
        if (bloom == null)
        {
            bloom = runtimeProfile.AddSettings<Bloom>();
        }
        bloom.enabled.Override(true);
        bloom.intensity.Override(0.20f);
        bloom.threshold.Override(1.20f);
        bloom.clamp.Override(2.5f);

        volume.profile = runtimeProfile;
    }

    public void EnsureMainCameraConfigured()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        _lastConfiguredCam = mainCam;
        if (mainCam.name.Contains("Minimap") || mainCam.name.Contains("UI")) return;

        mainCam.allowHDR = true;

        PostProcessLayer layer = mainCam.GetComponent<PostProcessLayer>();
        if (layer == null)
        {
            layer = mainCam.gameObject.AddComponent<PostProcessLayer>();
        }

        layer.volumeTrigger = mainCam.transform;

        int dayLayer = dayVolume != null ? dayVolume.gameObject.layer : -1;
        int nightLayer = nightVolume != null ? nightVolume.gameObject.layer : -1;

        int mask = 0;
        if (dayLayer >= 0 && dayLayer < 32) mask |= (1 << dayLayer);
        if (nightLayer >= 0 && nightLayer < 32) mask |= (1 << nightLayer);

        int ppLayerIndex = LayerMask.NameToLayer("PostProcessing");
        if (ppLayerIndex >= 0 && ppLayerIndex < 32) mask |= (1 << ppLayerIndex);

        layer.volumeLayer = mask != 0 ? mask : ~0;

        PostProcessResources res = postProcessResources;
        if (res == null) res = Resources.Load<PostProcessResources>("PostProcessResources");
        if (res == null)
        {
            var allRes = Resources.FindObjectsOfTypeAll<PostProcessResources>();
            if (allRes != null && allRes.Length > 0) res = allRes[0];
        }
        if (res != null)
        {
            layer.Init(res);
            _isCameraPpInitialized = true;
        }
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
        ConfigureVolumeProfile(dayVolume, ref _runtimeDayProfile, +0.20f, 100f);
        ConfigureVolumeProfile(nightVolume, ref _runtimeNightProfile, 0.00f, 100f);
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

    public DayNightKeyframe Evaluate(float hour)
    {
        if (keyframes == null || keyframes.Length == 0)
            return default(DayNightKeyframe);

        hour = Mathf.Repeat(hour, 24f);
        int n = keyframes.Length;
        if (n == 1) return keyframes[0];

        int i0 = n - 1;
        int i1 = 0;

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            float h0 = keyframes[i].hour;
            float h1 = keyframes[next].hour;
            float h1Norm = h1 <= h0 ? h1 + 24f : h1;
            float hourNorm = (hour < h0 && h1 <= h0) ? hour + 24f : hour;

            if (hourNorm >= h0 && hourNorm < h1Norm)
            {
                i0 = i;
                i1 = next;
                break;
            }
        }

        DayNightKeyframe k0 = keyframes[i0];
        DayNightKeyframe k1 = keyframes[i1];

        float k0H = k0.hour;
        float k1H = k1.hour;
        if (k1H <= k0H) k1H += 24f;

        float evalHour = hour;
        if (evalHour < k0H) evalHour += 24f;

        float span = k1H - k0H;
        float t = span > 0.0001f ? (evalHour - k0H) / span : 0f;
        t = Mathf.Clamp01(t);

        return Lerp(k0, k1, t);
    }

    private static DayNightKeyframe Lerp(DayNightKeyframe a, DayNightKeyframe b, float t)
    {
        float h0 = a.hour;
        float h1 = b.hour;
        if (h1 < h0) h1 += 24f;
        float h = Mathf.Repeat(Mathf.Lerp(h0, h1, t), 24f);

        return new DayNightKeyframe
        {
            hour = h,
            sunEulerX = Mathf.LerpAngle(a.sunEulerX, b.sunEulerX, t),
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

        if (!_hasCapturedSunYaw && sunLight != null)
        {
            _sunBaseYaw = sunLight.transform.localEulerAngles.y;
            _hasCapturedSunYaw = true;
        }

        float sunPitch = s.sunEulerX;
        float sunYaw = _sunBaseYaw;
        bool isSunActive = s.sunIntensity > 0.001f;

        if (sunLight != null)
        {
            sunLight.transform.localRotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
            sunLight.color = s.sunColor;
            sunLight.intensity = isSunActive ? s.sunIntensity : 0f;
            sunLight.flare = null;
            sunLight.shadows = LightShadows.Soft;
            sunLight.enabled = isSunActive;
        }

        float dayFactor = s.dayVolumeWeight;
        CurrentDayWeight = dayFactor;

        if (fillLight != null)
        {
            bool isFillActive = s.fillIntensity > 0.001f && !isSunActive;
            fillLight.transform.localRotation = Quaternion.Euler(50f, sunYaw + 180f, 0f);
            fillLight.color = s.fillColor;
            fillLight.intensity = isFillActive ? s.fillIntensity : 0f;
            fillLight.flare = null;
            fillLight.shadows = LightShadows.None;
            fillLight.enabled = isFillActive;
        }

        Color skyAmb = s.ambientColor * s.ambientIntensity;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyAmb;
        RenderSettings.ambientEquatorColor = Color.Lerp(skyAmb * 0.80f, skyAmb * 0.90f, dayFactor);
        RenderSettings.ambientGroundColor = Color.Lerp(skyAmb * 0.70f, skyAmb * 0.88f, dayFactor);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.40f, 0.85f, dayFactor);

        RenderSettings.fog = s.fogEnabled;
        RenderSettings.fogColor = s.fogColor;
        RenderSettings.fogDensity = s.fogDensity;

        if (skydomeRenderer != null)
        {
            if (_skyBlock == null) _skyBlock = new MaterialPropertyBlock();
            skydomeRenderer.GetPropertyBlock(_skyBlock);
            _skyBlock.SetColor(skyTopColorProp, s.skyTopColor);
            _skyBlock.SetColor(skyBottomColorProp, s.skyBottomColor);
            skydomeRenderer.SetPropertyBlock(_skyBlock);
        }

        if (Application.isPlaying)
        {
            Camera currentMain = Camera.main;
            if (currentMain != null && (currentMain != _lastConfiguredCam || !_isCameraPpInitialized))
            {
                EnsureMainCameraConfigured();
            }
        }
    }
}

