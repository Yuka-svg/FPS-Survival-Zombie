using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class HealthFlashWidget : MonoBehaviour
{
    public Sprite vignetteSprite;
    public float fadeOutSpeed = 0.5f;

    public Color damageColor = new Color(1f, 0.382f, 0.382f, 0.71f);
    public Color healColor = new Color(0.637f, 1f, 0.683f, 0.78f);
    public Color coinColor = new Color(1f, 0.946f, 0.382f, 0.851f);
    public Color xpColor = new Color(0.5f, 0.099f, 1f, 0.604f);

    private VisualElement _flash;
    private CowsinsHUDAdapter _adapter;
    private IVisualElementScheduledItem _fadeSched;
    private float _lastHp = -1f;
    private int _lastCoins = -1;
    private float _lastXpFill = -1f;
    private int _lastLevel = -1;

    private float _lastTimeScale = 1f;
    private float _unpauseTime = -999f;
    private float _suppressDuration = 3.0f;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc != null)
            _flash = doc.rootVisualElement.Q("HealthFlash");
        if (_flash == null)
        {
            Debug.LogError("HealthFlashWidget: #HealthFlash not found in UIDocument");
            enabled = false;
            return;
        }
        if (vignetteSprite != null)
            _flash.style.backgroundImage = new StyleBackground(vignetteSprite);
    }

    private void OnEnable()
    {
        StartCoroutine(Bind());
    }

    private IEnumerator Bind()
    {
        float timeout = 12f;
        while (CowsinsHUDAdapter.Instance == null && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter == null) yield break;
        _lastHp = _adapter.Health;
        _lastCoins = _adapter.Coins;
        _lastXpFill = _adapter.XpFill;
        _lastLevel = _adapter.PlayerLevel;
        _adapter.OnHealthChanged += OnHealth;
        _adapter.OnCoinsChanged += OnCoins;
        _adapter.OnXpChanged += OnXp;
    }

    private void OnDisable()
    {
        if (_adapter != null)
        {
            _adapter.OnHealthChanged -= OnHealth;
            _adapter.OnCoinsChanged -= OnCoins;
            _adapter.OnXpChanged -= OnXp;
        }
        _fadeSched?.Pause();
        _fadeSched = null;
        StopAllCoroutines();
    }

    private void Update()
    {
        // Detect when the game unpauses (transitions from timeScale = 0 to timeScale > 0)
        if (Time.timeScale > 0f && _lastTimeScale == 0f)
        {
            _unpauseTime = Time.realtimeSinceStartup;
        }
        _lastTimeScale = Time.timeScale;
    }

    private void OnHealth(float hp, float max, bool damaged)
    {
        if (damaged || (_lastHp >= 0f && hp < _lastHp - 0.001f)) Flash(damageColor);
        else if (_lastHp >= 0f && hp > _lastHp + 0.001f) Flash(healColor);
        _lastHp = hp;
    }

    private void OnCoins(int coins)
    {
        if (_lastCoins >= 0 && coins > _lastCoins) Flash(coinColor);
        _lastCoins = coins;
    }

    private void OnXp(int level, float fill)
    {
        bool gained = (_lastLevel >= 0 && level > _lastLevel) || (_lastXpFill >= 0f && fill > _lastXpFill + 0.001f);
        if (gained) Flash(xpColor);
        _lastXpFill = fill;
        _lastLevel = level;
    }

    private void Flash(Color color)
    {
        if (_flash == null) return;
        if (Time.timeScale == 0f) return;

        // Instant unpause check to catch physics trigger events in the same frame
        if (_lastTimeScale == 0f)
        {
            _unpauseTime = Time.realtimeSinceStartup;
            _lastTimeScale = 1f;
        }

        // Suppress flashes in the immediate 3.0s window after unpausing
        if (Time.realtimeSinceStartup - _unpauseTime < _suppressDuration) return;

        _fadeSched?.Pause();
        _flash.style.unityBackgroundImageTintColor = color;
        _flash.AddToClassList("flash");

        _fadeSched = _flash.schedule.Execute(() =>
        {
            _flash.RemoveFromClassList("flash");
        }).StartingIn(32);
    }
}
