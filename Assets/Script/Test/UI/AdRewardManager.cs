using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;
using GoogleMobileAds.Api;

public class AdRewardManager : MonoBehaviour
{
    private static AdRewardManager _instance;
    public static AdRewardManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<AdRewardManager>();
            return _instance;
        }
    }

    private VisualElement _doc;
    private VisualElement _panel;
    private VisualElement _card;
    private Label _titleLabel;
    private Label _timerLabel;
    private Label _rewardLabel;
    private VisualElement _rewardBadge;
    private VisualElement _rewardIcon;
    private VisualElement _adContainer;
    private VisualElement _adPlayingOverlay;
    private Button _watchButton;
    private Button _closeButton;
    private bool _ready;
    private bool _isPanelOpen;
    private Coroutine _adCoroutine;
    private Transform _currentPlayer;
    private PlayerControl _playerControl;
    private float _previousTimeScale = 1f;

    [Header("AdMob Settings")]
    [Tooltip("AdMob Rewarded Ad Unit ID cho Android.")]
    public string androidAdUnitId = "ca-app-pub-3940256099942544/5224354917";
    [Tooltip("AdMob Rewarded Ad Unit ID cho iOS.")]
    public string iosAdUnitId = "ca-app-pub-3940256099942544/1712485313";

    [Header("Reward Amounts")]
    public int coinAmount = 150;
    public float expAmount = 75f;
    public int ammoMagazines = 2;
    public int healthAmount = 40;

    private RewardedAd _rewardedAd;
    private bool _isAdLoading;
    private bool _isAdReady;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        InitializeAdMob();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void OnEnable() { SetupUI(); }

    private void OnDisable()
    {
        if (_watchButton != null) _watchButton.clicked -= StartAd;
        if (_closeButton != null) _closeButton.clicked -= ClosePanel;
        if (_card != null) _card.generateVisualContent -= OnGenerateCardBackground;
        DestroyAd();
    }

    private void SetupUI()
    {
        if (_doc == null)
        {
            var docComp = GetComponent<UIDocument>();
            if (docComp != null) _doc = docComp.rootVisualElement;
        }
        if (_doc == null)
        {
            var go = GameObject.Find("GameUICanvas");
            if (go != null)
            {
                var docComp = go.GetComponent<UIDocument>();
                if (docComp != null) _doc = docComp.rootVisualElement;
            }
        }
        if (_doc == null) return;

        var root = _doc;
        _panel = root.Q("AdRewardPanel");
        if (_panel == null) return;

        _card = _panel.Q("AdCard");
        _titleLabel = _panel.Q<Label>("AdTitle");
        _timerLabel = _panel.Q<Label>("AdTimer");
        _rewardLabel = _panel.Q<Label>("AdRewardText");
        _rewardBadge = _panel.Q("AdRewardBadge");
        _rewardIcon = _panel.Q("AdRewardIcon");
        _adContainer = _panel.Q("AdContent");
        _adPlayingOverlay = _panel.Q("AdPlayingOverlay");
        _watchButton = _panel.Q<Button>("WatchAdButton");
        _closeButton = _panel.Q<Button>("AdCloseButton");

        _panel.style.display = DisplayStyle.None;

        if (_card != null)
        {
            _card.generateVisualContent -= OnGenerateCardBackground;
            _card.generateVisualContent += OnGenerateCardBackground;
        }

        if (_watchButton != null)
        {
            _watchButton.clicked -= StartAd;
            _watchButton.clicked += StartAd;
        }
        if (_closeButton != null)
        {
            _closeButton.clicked -= ClosePanel;
            _closeButton.clicked += ClosePanel;
        }

        _ready = true;
    }

    private void InitializeAdMob()
    {
        try
        {
            MobileAds.Initialize(initStatus =>
            {
                Debug.Log("AdMob initialized: " + initStatus);
                LoadRewardedAd();
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("AdMob init failed (expected in Editor): " + e.Message);
        }
    }

    private string GetAdUnitId()
    {
#if UNITY_ANDROID
        return androidAdUnitId;
#elif UNITY_IOS
        return iosAdUnitId;
#else
        return androidAdUnitId;
#endif
    }

    public void LoadRewardedAd()
    {
        if (_isAdLoading) return;
        _isAdLoading = true;

        DestroyAd();

        var adRequest = new AdRequest();

        RewardedAd.Load(GetAdUnitId(), adRequest, (RewardedAd ad, LoadAdError error) =>
        {
            _isAdLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogError("Rewarded ad failed to load: " + error);
                _isAdReady = false;
                return;
            }

            _rewardedAd = ad;
            _isAdReady = true;
            Debug.Log("Rewarded ad loaded.");

            _rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Rewarded ad closed.");
                _isAdReady = false;
                _rewardedAd = null;
            };

            _rewardedAd.OnAdFullScreenContentFailed += (adError) =>
            {
                Debug.LogError("Rewarded ad failed to show: " + adError);
                _isAdReady = false;
                _rewardedAd = null;
                ClosePanel();
            };
        });
    }

    public bool ShowAd(Transform player)
    {
        Debug.Log("[AdReward] ShowAd called. Player=" + (player != null ? player.name : "null") + " _ready=" + _ready);

        if (_isPanelOpen) return false;

        if (PanelManager.Instance != null && !PanelManager.Instance.CanOpenPanel("AdReward"))
        {
            _currentPlayer = null;
            _playerControl = null;
            return false;
        }

        _currentPlayer = player;
        _playerControl = player != null ? player.GetComponentInChildren<PlayerControl>() : null;

        if (!_ready)
        {
            SetupUI();
            if (!_ready) return false;
        }

        _isPanelOpen = true;

        // Use PanelManager to properly register the panel and handle pause/control/cursor
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("AdReward", _panel, _card, ClosePanel);
        }
        else
        {
            // Fallback if PanelManager not available
            _panel.style.display = DisplayStyle.Flex;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_playerControl != null) _playerControl.LoseControl();
            PauseMenu.isPaused = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        if (_titleLabel != null) _titleLabel.text = "QUẢNG CÁO";
        if (_timerLabel != null)
        {
            if (_isAdReady)
                _timerLabel.text = "Đang phát quảng cáo...";
            else
                _timerLabel.text = "Đang tải quảng cáo...";
        }
        if (_rewardLabel != null) _rewardLabel.text = "";
        if (_rewardBadge != null) _rewardBadge.style.display = DisplayStyle.None;
        ClearRewardIconClasses();

        var placeholder = _panel != null ? _panel.Q("ad-placeholder") : null;
        if (placeholder != null) placeholder.style.display = DisplayStyle.Flex;

        if (_watchButton != null) _watchButton.style.display = DisplayStyle.None;
        if (_closeButton != null) _closeButton.style.display = DisplayStyle.None;
        if (_adContainer != null) _adContainer.RemoveFromClassList("ad-playing");
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.None;

        // Auto-play ad
        StartAd();
        return true;
    }

    private void ClearRewardIconClasses()
    {
        if (_rewardIcon == null) return;
        _rewardIcon.RemoveFromClassList("reward-icon-coin");
        _rewardIcon.RemoveFromClassList("reward-icon-exp");
        _rewardIcon.RemoveFromClassList("reward-icon-ammo");
        _rewardIcon.RemoveFromClassList("reward-icon-health");
    }

    private void StartAd()
    {
        if (_adCoroutine != null)
        {
            StopCoroutine(_adCoroutine);
            _adCoroutine = null;
        }

        if (_watchButton != null) _watchButton.style.display = DisplayStyle.None;
        if (_adContainer != null) _adContainer.AddToClassList("ad-playing");
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.Flex;

#if UNITY_EDITOR
        _adCoroutine = StartCoroutine(EditorSimulateAd());
#else
        if (_isAdReady && _rewardedAd != null && _rewardedAd.CanShowAd())
        {
            _rewardedAd.Show(reward =>
            {
                GrantRandomReward();
                if (_closeButton != null) _closeButton.style.display = DisplayStyle.Flex;
            });
        }
        else
        {
            if (_timerLabel != null) _timerLabel.text = "Đang tải quảng cáo...";
            if (_watchButton != null) _watchButton.SetEnabled(false);
            if (!_isAdLoading) LoadRewardedAd();
            _adCoroutine = StartCoroutine(WaitForAdThenShow());
        }
#endif
    }

    private IEnumerator WaitForAdThenShow()
    {
        float timeout = 15f;
        while (!_isAdReady && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_isAdReady && _rewardedAd != null && _rewardedAd.CanShowAd())
        {
            if (_watchButton != null) _watchButton.style.display = DisplayStyle.None;
            if (_adContainer != null) _adContainer.AddToClassList("ad-playing");

            _rewardedAd.Show(reward =>
            {
                GrantRandomReward();
                if (_closeButton != null) _closeButton.style.display = DisplayStyle.Flex;
            });
        }
        else
        {
            if (_timerLabel != null) _timerLabel.text = "Không thể tải quảng cáo!";
            if (_closeButton != null) _closeButton.style.display = DisplayStyle.Flex;
        }
        _adCoroutine = null;
    }

    private IEnumerator EditorSimulateAd()
    {
        float timer = 5f;
        while (timer > 0)
        {
            if (_timerLabel != null)
                _timerLabel.text = $"Quảng cáo kết thúc sau {timer:F0}s";
            timer -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (_timerLabel != null) _timerLabel.text = "";
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.None;
        GrantRandomReward();
        if (_closeButton != null) _closeButton.style.display = DisplayStyle.Flex;
        _adCoroutine = null;
    }

    private void GrantRandomReward()
    {
        if (!_isPanelOpen) return;

        string[] rewards = { "Coin", "Exp", "Ammo", "Health" };
        string selected = rewards[Random.Range(0, rewards.Length)];

        ClearRewardIconClasses();

        switch (selected)
        {
            case "Coin":
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-coin");
                if (CoinManager.Instance != null)
                {
                    CoinManager.Instance.AddCoins(coinAmount, false);
                    if (_rewardLabel != null)
                        _rewardLabel.text = $"+{coinAmount} COINS";
                }
                break;

            case "Exp":
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-exp");
                if (ExperienceManager.Instance != null)
                {
                    ExperienceManager.Instance.AddExperience(expAmount);
                    if (_rewardLabel != null)
                        _rewardLabel.text = $"+{expAmount} EXP";
                }
                break;

            case "Ammo":
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-ammo");
                if (_currentPlayer != null)
                {
                    var wRef = _currentPlayer.GetComponent<IWeaponReferenceProvider>();
                    if (wRef != null && wRef.Id != null)
                    {
                        int amount = Mathf.Max(10, wRef.Id.magazineSize * ammoMagazines);
                        wRef.Id.totalBullets += amount;
                        var wEvents = _currentPlayer.GetComponent<IWeaponEventsProvider>();
                        if (wEvents != null && wEvents.Events != null)
                            wEvents.Events.OnAmmoChanged?.Invoke(false);
                        if (_rewardLabel != null)
                            _rewardLabel.text = $"+{amount} ĐẠN";
                    }
                }
                break;

            case "Health":
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-health");
                if (_currentPlayer != null)
                {
                    var stats = _currentPlayer.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        stats.Heal(healthAmount);
                        if (_rewardLabel != null)
                            _rewardLabel.text = $"+{healthAmount} HP";
                    }
                }
                break;
        }

        if (_rewardBadge != null) _rewardBadge.style.display = DisplayStyle.Flex;
        var placeholder = _panel != null ? _panel.Q("ad-placeholder") : null;
        if (placeholder != null) placeholder.style.display = DisplayStyle.None;

        LoadRewardedAd();
    }

    private void ClosePanel()
    {
        if (!_isPanelOpen) return;
        _isPanelOpen = false;

        if (_adCoroutine != null)
        {
            StopCoroutine(_adCoroutine);
            _adCoroutine = null;
        }

        DestroyAd();

        // Use PanelManager to properly close and restore state
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("AdReward", _panel, _card);
        }
        else
        {
            // Fallback
            if (_panel != null)
                _panel.style.display = DisplayStyle.None;
            Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            if (_playerControl != null) _playerControl.GrantControl();
            PauseMenu.isPaused = false;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        _currentPlayer = null;
        _playerControl = null;
    }

    private void DestroyAd()
    {
        if (_rewardedAd != null)
        {
            _rewardedAd.Destroy();
            _rewardedAd = null;
        }
        _isAdReady = false;
    }

    private void OnGenerateCardBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 32f;

        // 1. Draw solid dark blue-gray translucent background shape
        Color fillCol = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.9f);
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

        // 2. Draw outer border with gold breathing glow
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 0.45f);
        Color strokeCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, pulse);
        painter.strokeColor = strokeCol;
        painter.lineWidth = 1.5f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 3. Draw inner offset double-line border
        float d = 3.5f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.15f);
            painter.strokeColor = innerCol;
            painter.lineWidth = 1.0f;
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
    }
}
