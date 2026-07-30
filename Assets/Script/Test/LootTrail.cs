using System.Collections;
using UnityEngine;
using cowsins;

/// <summary>
/// Trail effect runtime cho loot khi zombie drop: TrailRenderer (vệt mờ
/// phía sau) + child ParticleSystem (glow sparkles) bám theo loot khi bay.
/// Tự fade khi loot hạ cánh (LootPop landed) và tự dọn dẹp particle khi
/// loot bị destroy/pickup. Settings được truyền từ zombie qua
/// <see cref="LootTrailSettings"/> thông qua <see cref="LootDropHelper"/>.
/// </summary>
[RequireComponent(typeof(LootPop))]
public class LootTrail : MonoBehaviour
{
    [Header("Trail Renderer")]
    [Tooltip("Thời gian sống của vệt trail (giây).")]
    public float trailTime = 0.45f;

    [Tooltip("Bán kính đầu vệt (lúc spawn).")]
    public float startWidth = 0.35f;

    [Tooltip("Bán kính cuối vệt (mờ dần).")]
    public float endWidth = 0.02f;

    [Header("Glow Particle")]
    [Tooltip("Bật particle glow bám theo loot.")]
    public bool enableGlow = true;

    [Tooltip("Số particle tối đa phát ra trong đời.")]
    public int glowMaxParticles = 60;

    [Tooltip("Tốc độ phát particle (particle/giây).")]
    public float glowEmissionRate = 35f;

    [Tooltip("Kích thước particle bắt đầu.")]
    public float glowStartSize = 0.18f;

    [Tooltip("Thời gian sống mỗi particle (giây).")]
    public float glowStartLifetime = 0.5f;

    [Header("Colors")]
    [Tooltip("Màu trail mặc định (vàng) — dùng cho Coin.")]
    public Color coinColor = new Color(1f, 0.85f, 0.25f, 1f);

    [Tooltip("Màu trail cho Experience (tím).")]
    public Color xpColor = new Color(0.7f, 0.25f, 1f, 1f);

    [Tooltip("Màu trail fallback (loot khác).")]
    public Color defaultColor = new Color(1f, 0.7f, 0.4f, 1f);

    private TrailRenderer trail;
    private ParticleSystem glow;
    private LootPop pop;
    private Color color;
    private bool fading;
    private bool initialized;

    void Awake()
    {
        pop = GetComponent<LootPop>();
    }

    /// <summary>
    /// Áp dụng settings từ zombie (enemy) và build trail + glow.
    /// Phải được gọi ngay sau AddComponent, trước khi OnEnable chạy.
    /// </summary>
    public void Initialize(LootTrailSettings settings)
    {
        if (settings == null) return;

        trailTime = settings.trailTime;
        startWidth = settings.startWidth;
        endWidth = settings.endWidth;
        enableGlow = settings.enableGlow;
        glowMaxParticles = settings.glowMaxParticles;
        glowEmissionRate = settings.glowEmissionRate;
        glowStartSize = settings.glowStartSize;
        glowStartLifetime = settings.glowStartLifetime;
        coinColor = settings.coinColor;
        xpColor = settings.xpColor;
        defaultColor = settings.defaultColor;

        Build();
    }

    void OnEnable()
    {
        if (!initialized) return;
        // Bật trail/glow khi loot active. LootPop.Launch() set enabled=true.
        if (trail != null) trail.enabled = true;
        if (glow != null) glow.Play(true);
    }

    void OnDisable()
    {
        if (trail != null) trail.enabled = false;
        if (glow != null) glow.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void OnDestroy()
    {
        // Particle child sẽ tự destroy theo parent, nhưng clear trail để
        // tránh vệt linger trên pool.
        if (trail != null)
        {
            trail.Clear();
            if (trail.material != null && trail.material.name == "LootTrail_Runtime")
            {
                Destroy(trail.material);
            }
        }
        if (glow != null)
        {
            var renderer = glow.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.material != null && renderer.material.name == "LootGlow_Runtime")
            {
                Destroy(renderer.material);
            }
        }
    }

    void Update()
    {
        // Khi loot hạ cánh (LootPop.landed), fade trail và tắt glow dần.
        if (pop != null && pop.Landed && !fading)
        {
            fading = true;
            StartCoroutine(FadeOutRoutine());
        }
    }

    /// <summary>True nếu LootPop đã hạ cánh (expose qua property).</summary>
    public bool IsLanded => pop != null && pop.Landed;

    void Build()
    {
        color = ResolveColor();
        BuildTrail();
        if (enableGlow) BuildGlow();
        initialized = true;
    }

    Color ResolveColor()
    {
        // Detect loại loot qua component để chọn màu phù hợp.
        if (GetComponent<Coin>() != null || GetComponent<CoinMarker>() != null)
            return coinColor;
        if (GetComponent<Experience>() != null || GetComponent<XPMarker>() != null)
            return xpColor;
        return defaultColor;
    }

    void BuildTrail()
    {
        trail = gameObject.GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();

        if (trail.material != null && trail.material.name == "LootTrail_Runtime")
        {
            Destroy(trail.material);
        }

        // Material runtime: Sprites/Default với blend additive cho vệt sáng.
        Shader shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        mat.name = "LootTrail_Runtime";
        trail.material = mat;

        trail.time = trailTime;
        trail.startWidth = startWidth;
        trail.endWidth = endWidth;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.minVertexDistance = 0.05f;
        trail.autodestruct = false;
        trail.emitting = true;
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.enabled = true;
        trail.Clear();
    }

    void BuildGlow()
    {
        var glowGo = new GameObject("LootGlow");
        glowGo.transform.SetParent(transform, false);
        glowGo.transform.localPosition = Vector3.zero;

        glow = glowGo.AddComponent<ParticleSystem>();
        var main = glow.main;
        main.loop = true;
        main.startColor = color;
        main.startSize = glowStartSize;
        main.startLifetime = glowStartLifetime;
        main.startSpeed = 0f;
        main.maxParticles = glowMaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = glow.emission;
        emission.rateOverTime = glowEmissionRate;

        // Shape: điểm nhỏ quanh loot.
        var shape = glow.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        // Color over lifetime: fade out.
        var colorOverLifetime = glow.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        // Size over lifetime: nhỏ dần.
        var sizeOverLifetime = glow.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Material runtime additive cho particle.
        var renderer = glow.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            if (renderer.material != null && renderer.material.name == "LootGlow_Runtime")
            {
                Destroy(renderer.material);
            }
            Shader pshader = Shader.Find("Sprites/Default");
            Material pmat = new Material(pshader);
            pmat.name = "LootGlow_Runtime";
            renderer.material = pmat;
        }

        glow.Play(true);
    }

    IEnumerator FadeOutRoutine()
    {
        // Tắt phát glow mới, để particle còn sống tự tắt.
        if (glow != null) glow.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Thu hẹp trail nhanh khi hạ cánh.
        if (trail != null)
        {
            trail.time = Mathf.Min(trail.time, 0.15f);
            trail.emitting = false;
        }

        // Chờ particle còn sống hết, rồi clear trail.
        float wait = glowStartLifetime + 0.1f;
        yield return new WaitForSeconds(wait);

        if (trail != null) trail.Clear();
    }
}

/// <summary>Marker tùy chọn để nhận diện Coin (nếu không dùng namespace cowsins).</summary>
public sealed class CoinMarker : MonoBehaviour { }

/// <summary>Marker tùy chọn để nhận diện Experience.</summary>
public sealed class XPMarker : MonoBehaviour { }

/// <summary>
/// Cấu hình trail effect, được chỉnh trên zombie (enemy) Inspector.
/// Truyền qua <see cref="LootDropHelper"/> khi drop loot.
/// </summary>
[System.Serializable]
public class LootTrailSettings
{
    [Header("Trail Renderer")]
    [Tooltip("Thời gian sống của vệt trail (giây).")]
    public float trailTime = 0.45f;

    [Tooltip("Bán kính đầu vệt (lúc spawn).")]
    public float startWidth = 0.35f;

    [Tooltip("Bán kính cuối vệt (mờ dần).")]
    public float endWidth = 0.02f;

    [Header("Glow Particle")]
    [Tooltip("Bật particle glow bám theo loot.")]
    public bool enableGlow = true;

    [Tooltip("Số particle tối đa phát ra trong đời.")]
    public int glowMaxParticles = 60;

    [Tooltip("Tốc độ phát particle (particle/giây).")]
    public float glowEmissionRate = 35f;

    [Tooltip("Kích thước particle bắt đầu.")]
    public float glowStartSize = 0.18f;

    [Tooltip("Thời gian sống mỗi particle (giây).")]
    public float glowStartLifetime = 0.5f;

    [Header("Colors")]
    [Tooltip("Màu trail mặc định (vàng) — dùng cho Coin.")]
    public Color coinColor = new Color(1f, 0.85f, 0.25f, 1f);

    [Tooltip("Màu trail cho Experience (tím).")]
    public Color xpColor = new Color(0.7f, 0.25f, 1f, 1f);

    [Tooltip("Màu trail fallback (loot khác).")]
    public Color defaultColor = new Color(1f, 0.7f, 0.4f, 1f);
}
