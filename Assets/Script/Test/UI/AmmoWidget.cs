using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class AmmoWidget : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private string _infiniteSymbol = "\u221E";
    [SerializeField] private Color _normalColor = new Color(0.92f, 0.88f, 0.78f, 1f);
    [SerializeField] private Color _lowColor = new Color(0.85f, 0.35f, 0.15f, 1f);
    [Range(0f, 1f)] [SerializeField] private float _lowFraction = 0.34f;
    [SerializeField] private float _punchScale = 1.14f;
    [SerializeField] private float _punchDuration = 0.12f;

    private VisualElement _root;
    private Label _ammoValue;
    private Label _ammoReserve;
    private VisualElement _heatBarTrack;
    private VisualElement _heatBarFill;
    private VisualElement _ammoPunchRoot;

    private Vector3 _home = Vector3.one;
    private Coroutine _punch;
    private bool _isLowAmmo;
    private VisualElement _bulletGauge;
    private Label _caliberLabel;
    private Label _fireModeLabel;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        _root = doc.rootVisualElement.Q("AmmoCluster");
        if (_root == null) { enabled = false; return; }
        _ammoValue = _root.Q<Label>("AmmoValue");
        _ammoReserve = _root.Q<Label>("AmmoReserve");
        _heatBarTrack = _root.Q("HeatBarTrack");
        _heatBarFill = _root.Q("HeatBarFill");
        _ammoPunchRoot = _root.Q("AmmoPunchRoot");
        _bulletGauge = _root.Q("BulletGauge");
        _caliberLabel = _root.Q<Label>("CaliberLabel");
        _fireModeLabel = _root.Q<Label>("FireModeLabel");

        _root.generateVisualContent += OnGenerateAmmoBackground;
        if (_bulletGauge != null) _bulletGauge.generateVisualContent += OnGenerateBulletGauge;
    }

    private void OnEnable() { StartCoroutine(Bind()); }

    private void OnDisable()
    {
        var a = CowsinsHUDAdapter.Instance;
        if (a != null) { a.OnAmmoChanged -= OnAmmo; a.OnHeatChanged -= OnHeat; a.OnFired -= OnFired; a.OnWeaponChanged -= OnWeaponChanged; }
        if (_root != null) _root.generateVisualContent -= OnGenerateAmmoBackground;
        if (_bulletGauge != null) _bulletGauge.generateVisualContent -= OnGenerateBulletGauge;
        StopAllCoroutines();
    }

    private IEnumerator Bind()
    {
        var th = UITheme.Active;
        if (th != null) { _normalColor = th.ammoNormal; _lowColor = th.ammoLow; _punchScale = th.ammoPunchScale; }
        while (CowsinsHUDAdapter.Instance == null) yield return null;
        var a = CowsinsHUDAdapter.Instance;
        a.OnAmmoChanged += OnAmmo;
        a.OnHeatChanged += OnHeat;
        a.OnFired += OnFired;
        a.OnWeaponChanged += OnWeaponChanged;
        OnWeaponChanged(a.WeaponName, a.WeaponIcon);
        OnAmmo(a.Ammo, a.Reserve);
        OnHeat(a.Heat);
    }

    private void OnWeaponChanged(string name, Sprite icon)
    {
        var a = CowsinsHUDAdapter.Instance;
        if (a == null) return;
        if (_caliberLabel != null) _caliberLabel.text = a.CaliberText;
        if (_fireModeLabel != null) _fireModeLabel.text = a.FireModeText;
    }

    private void OnAmmo(int mag, int reserve)
    {
        var a = CowsinsHUDAdapter.Instance;
        bool low = a != null && a.MagazineSize > 0 && mag <= Mathf.CeilToInt(a.MagazineSize * _lowFraction);
        _isLowAmmo = low;
        if (_ammoValue != null)
        {
            _ammoValue.text = mag.ToString();
        }
        if (_ammoReserve != null)
            _ammoReserve.text = (a != null && !a.LimitedReserve) ? _infiniteSymbol : reserve.ToString();

        if (_caliberLabel != null && a != null) _caliberLabel.text = a.CaliberText;
        if (_fireModeLabel != null && a != null) _fireModeLabel.text = a.FireModeText;

        if (_root != null)
        {
            _root.EnableInClassList("low-ammo", low);
            _root.MarkDirtyRepaint();
        }
        if (_bulletGauge != null)
        {
            _bulletGauge.MarkDirtyRepaint();
        }
    }

    private void OnHeat(float heat)
    {
        if (_heatBarTrack == null) return;
        bool show = heat > 0.001f;
        _heatBarTrack.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (_heatBarFill != null)
        {
            _heatBarFill.style.width = Length.Percent(Mathf.Clamp01(heat) * 100f);
            _heatBarFill.style.backgroundColor = Color.Lerp(_normalColor, _lowColor, Mathf.Clamp01(heat));
        }
    }

    private void OnFired()
    {
        if (_ammoPunchRoot == null || _punch != null) return;
        _punch = StartCoroutine(Punch());
    }

    private IEnumerator Punch()
    {
        float t = 0f;
        while (t < _punchDuration)
        {
            float p = t / _punchDuration;
            float s = Mathf.Lerp(_punchScale, 1f, p);
            _ammoPunchRoot.style.scale = new Scale(new Vector3(s, s, 1f));
            t += Time.deltaTime;
            yield return null;
        }
        _ammoPunchRoot.style.scale = new Scale(_home);
        _punch = null;
    }

    private void Update()
    {
        if (_isLowAmmo && _root != null)
        {
            _root.MarkDirtyRepaint();
        }
    }

    private void OnGenerateAmmoBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 12f;

        // 1. Draw solid dark blue-gray translucent background shape (0.85 alpha, reddish if low ammo)
        Color fillCol = _isLowAmmo ? new Color(60f / 255f, 15f / 255f, 15f / 255f, 0.85f)
                                  : new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
        painter.fillColor = fillCol;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Fill();

        // 2. Draw gold/black diagonal hazard warning stripes at top-left edge (symmetrical to HealthCluster)
        float badgeW = 40f;
        float badgeH = 4f;
        float startX = 16f;
        float startY = 2f;
        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 5f)
        {
            painter.strokeColor = _isLowAmmo ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.9f)
                                            : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 3f, startY + badgeH));
            painter.Stroke();

            painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset + 2f, startY));
            painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
            painter.Stroke();
        }

        // 3. Outer border (red pulsing if low ammo, faint gold if normal)
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 2.0f, 0.45f);
        Color strokeCol = _isLowAmmo ? new Color(229f / 255f, 72f / 255f, 60f / 255f, pulse)
                                    : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.25f);
        painter.strokeColor = strokeCol;
        painter.lineWidth = 1.2f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 4. Inner offset border (3px inset)
        float d = 3f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = _isLowAmmo ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.5f)
                                       : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.45f);
            painter.strokeColor = innerCol;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chamferSize, d));
            painter.LineTo(new Vector2(rect.width - d, d));
            painter.LineTo(new Vector2(rect.width - d, rect.height - chamferSize));
            painter.LineTo(new Vector2(rect.width - chamferSize, rect.height - d));
            painter.LineTo(new Vector2(d, rect.height - d));
            painter.LineTo(new Vector2(d, chamferSize));
            painter.ClosePath();
            painter.Stroke();
        }

        // 5. L-shaped corner metal brackets
        Color bracketColor = _isLowAmmo ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.95f)
                                       : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.9f);
        painter.strokeColor = bracketColor;
        painter.lineWidth = 3.5f;

        // Top-Left bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, chamferSize + 6f));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.LineTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(chamferSize + 6f, 0));
        painter.Stroke();

        // Top-Right bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - chamferSize - 6f, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, 6f));
        painter.Stroke();

        // Bottom-Right bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width, rect.height - chamferSize - 6f));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(rect.width - chamferSize - 6f, rect.height));
        painter.Stroke();

        // Bottom-Left bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(6f, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, rect.height - 6f));
        painter.Stroke();

        // 6. Draw 4 3D metallic gold corner rivets (screws)
        System.Action<Vector2> drawRivet = center =>
        {
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.4f, 0.4f), 2.2f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f);
            painter.BeginPath();
            painter.Arc(center, 1.8f, 0f, 360f);
            painter.Fill();

            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.4f, 0.4f), 0.4f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 7f;
        drawRivet(new Vector2(rOffset + 2f, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rOffset + 2f, rect.height - rOffset));
    }

    private void OnGenerateBulletGauge(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        var a = CowsinsHUDAdapter.Instance;
        float frac = (a != null && a.MagazineSize > 0) ? Mathf.Clamp01((float)a.Ammo / a.MagazineSize) : 1f;

        int totalDots = 10;
        float dotW = (rect.width - (totalDots - 1) * 3f) / totalDots;
        float activeCount = Mathf.Ceil(frac * totalDots);

        for (int i = 0; i < totalDots; i++)
        {
            float x = i * (dotW + 3f);
            bool active = i < activeCount;
            Color fill = active ? (_isLowAmmo ? _lowColor : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.9f))
                                : new Color(30f / 255f, 35f / 255f, 45f / 255f, 0.5f);

            painter.fillColor = fill;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x + 1f, 0));
            painter.LineTo(new Vector2(x + dotW, 0));
            painter.LineTo(new Vector2(x + dotW - 1f, rect.height));
            painter.LineTo(new Vector2(x, rect.height));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
