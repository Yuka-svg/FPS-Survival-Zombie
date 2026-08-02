using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;
using GoogleMobileAds.Api;

public class AdRewardManager : MonoBehaviour
{
    public enum AdUIState { Idle, Preview, Playing, Claimed }

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
    private bool _previousAudioListenerPause;

    private AdUIState _currentState = AdUIState.Idle;
    private GiftBox _currentGiftBox;
    private GiftBox.GiftRewardType _cachedType;
    private int _cachedAmount;
    private bool _hasClaimedReward;

    // Mobile Thread-Safe Callbacks (#else)
    private volatile bool _isRewardEarned;
    private volatile bool _pendingAdClosed;
    private volatile bool _pendingAdFailed;
    private float _adClosedTimer;

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
        UnsubscribeUIEvents();
        _currentState = AdUIState.Idle;
        _currentGiftBox = null;
        DestroyAd();
    }

    private void UnsubscribeUIEvents()
    {
        if (_watchButton != null) _watchButton.clicked -= OnWatchButtonClicked;
        if (_closeButton != null) _closeButton.clicked -= OnCloseButtonClicked;
        if (_card != null) _card.generateVisualContent -= OnGenerateCardBackground;

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
            _watchButton.clicked -= OnWatchButtonClicked;
            _watchButton.clicked += OnWatchButtonClicked;
        }
        if (_closeButton != null)
        {
            _closeButton.clicked -= OnCloseButtonClicked;
            _closeButton.clicked += OnCloseButtonClicked;
        }


        SetButtonState(_watchButton, false, false, false, "XEM QUẢNG CÁO");
        SetButtonState(_closeButton, false, false, false, "BỎ QUA");

        _ready = true;
    }

    private void Update()
    {
        if (_isRewardEarned && !_hasClaimedReward)
        {
            _hasClaimedReward = true;
            OnAdCompletedSuccessfully();
        }

        if (_pendingAdClosed)
        {
            if (_hasClaimedReward || _isRewardEarned)
            {
                _pendingAdClosed = false;
            }
            else
            {
                _adClosedTimer -= Time.unscaledDeltaTime;
                if (_adClosedTimer <= 0f)
                {
                    _pendingAdClosed = false;
                    ClosePanelInternal();
                }
            }
        }

        if (_pendingAdFailed)
        {
            _pendingAdFailed = false;
            OnAdFailed();
        }
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
                _pendingAdClosed = true;
                _adClosedTimer = 0.5f; // 0.5s Grace period for reward callback handshake
            };

            _rewardedAd.OnAdFullScreenContentFailed += (adError) =>
            {
                Debug.LogError("Rewarded ad failed to show: " + adError);
                _isAdReady = false;
                _rewardedAd = null;
                _pendingAdFailed = true;
            };
        });
    }

    public bool ShowAd(Transform player)
    {
        return ShowAd(player, null);
    }

    public bool ShowAd(Transform player, GiftBox giftBox)
    {
        Debug.Log("[AdReward] ShowAd called. Player=" + (player != null ? player.name : "null") + " GiftBox=" + (giftBox != null ? giftBox.name : "null"));

        if (_isPanelOpen || _currentState != AdUIState.Idle) return false;

        if (PanelManager.Instance != null && !PanelManager.Instance.CanOpenPanel("AdReward"))
        {
            _currentPlayer = null;
            _playerControl = null;
            return false;
        }

        _currentPlayer = player;
        _playerControl = player != null ? player.GetComponentInChildren<PlayerControl>() : null;
        _currentGiftBox = giftBox;
        _hasClaimedReward = false;
        _isRewardEarned = false;
        _pendingAdClosed = false;
        _pendingAdFailed = false;

        if (!_ready)
        {
            SetupUI();
            if (!_ready) return false;
        }

        _isPanelOpen = true;
        _currentState = AdUIState.Preview;

        if (giftBox != null)
        {
            (_cachedType, _cachedAmount) = giftBox.GetOrGenerateReward(player, coinAmount, expAmount, ammoMagazines, healthAmount);
        }
        else
        {
            _cachedType = GiftBox.GiftRewardType.Coin;
            _cachedAmount = coinAmount;
        }

        // Open Panel via PanelManager
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("AdReward", _panel, _card, OnEscapeCloseRequested);
        }
        else
        {
            _panel.style.display = DisplayStyle.Flex;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_playerControl != null) _playerControl.LoseControl();
            PauseMenu.isPaused = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        // Setup PREVIEW UI State
        if (_card != null) _card.style.display = DisplayStyle.Flex;

        if (_titleLabel != null) _titleLabel.text = "QUẢNG CÁO";
        if (_timerLabel != null) _timerLabel.text = "Nhấn XEM QUẢNG CÁO để nhận quà!";

        UpdateRewardBadgeUI(_cachedType, _cachedAmount);

        var placeholder = _panel != null ? _panel.Q("ad-placeholder") : null;
        if (placeholder != null) placeholder.style.display = DisplayStyle.Flex;

        SetButtonState(_watchButton, true, false, false, "XEM QUẢNG CÁO");
        SetButtonState(_closeButton, true, false, true, "BỎ QUA");

        if (_adContainer != null) _adContainer.RemoveFromClassList("ad-playing");
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.None;

        return true;
    }

    private void UpdateRewardBadgeUI(GiftBox.GiftRewardType type, int amount)
    {
        ClearRewardIconClasses();

        switch (type)
        {
            case GiftBox.GiftRewardType.Coin:
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-coin");
                if (_rewardLabel != null) _rewardLabel.text = $"+{amount} COINS";
                break;

            case GiftBox.GiftRewardType.Exp:
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-exp");
                if (_rewardLabel != null) _rewardLabel.text = $"+{amount} EXP";
                break;

            case GiftBox.GiftRewardType.Ammo:
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-ammo");
                if (_rewardLabel != null) _rewardLabel.text = $"+{amount} ĐẠN";
                break;

            case GiftBox.GiftRewardType.Health:
                if (_rewardIcon != null) _rewardIcon.AddToClassList("reward-icon-health");
                if (_rewardLabel != null) _rewardLabel.text = $"+{amount} HP";
                break;

            default:
                if (_rewardLabel != null) _rewardLabel.text = "+THƯỞNG";
                break;
        }

        if (_rewardBadge != null) _rewardBadge.style.display = DisplayStyle.Flex;
    }

    private void ClearRewardIconClasses()
    {
        if (_rewardIcon == null) return;
        _rewardIcon.RemoveFromClassList("reward-icon-coin");
        _rewardIcon.RemoveFromClassList("reward-icon-exp");
        _rewardIcon.RemoveFromClassList("reward-icon-ammo");
        _rewardIcon.RemoveFromClassList("reward-icon-health");
    }

    private void OnWatchButtonClicked()
    {
        if (_currentState != AdUIState.Preview) return;
        StartAd();
    }

    private void OnCloseButtonClicked()
    {
        ClosePanelInternal();
    }

    private void OnEscapeCloseRequested()
    {
        if (_currentState == AdUIState.Idle) return;

        if (_isAdLoading)
        {
            ClosePanelInternal();
            return;
        }

        if (_currentState == AdUIState.Preview || _currentState == AdUIState.Claimed)
        {
            ClosePanelInternal();
            return;
        }

        if (_currentState == AdUIState.Playing)
        {
            ClosePanelInternal();
            return;
        }
    }

    private void StartAd()
    {
        _currentState = AdUIState.Playing;
        _previousAudioListenerPause = AudioListener.pause;

        if (_adCoroutine != null)
        {
            StopCoroutine(_adCoroutine);
            _adCoroutine = null;
        }

        if (_watchButton != null) _watchButton.style.display = DisplayStyle.None;
        if (_closeButton != null) _closeButton.style.display = DisplayStyle.None;

        if (MusicManager.Instance != null) MusicManager.Instance.PauseMusic();
        AudioListener.pause = true;

        if (_adContainer != null) _adContainer.AddToClassList("ad-playing");
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.Flex;

        if (_isAdReady && _rewardedAd != null && _rewardedAd.CanShowAd())
        {
            if (_panel != null) _panel.style.display = DisplayStyle.None;

            _rewardedAd.Show(reward =>
            {
                if (this != null) _isRewardEarned = true;
            });
        }
        else
        {
            if (_timerLabel != null) _timerLabel.text = "Đang tải quảng cáo...";
            if (!_isAdLoading) LoadRewardedAd();
            _adCoroutine = StartCoroutine(WaitForAdThenShow());
        }
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
            if (_panel != null) _panel.style.display = DisplayStyle.None;

            _rewardedAd.Show(reward =>
            {
                if (this != null) _isRewardEarned = true;
            });
        }
        else
        {
            OnAdFailed();
        }
        _adCoroutine = null;
    }

    private void OnAdCompletedSuccessfully()
    {
        _currentState = AdUIState.Claimed;

        ApplyCachedReward();

        ClosePanelInternal();

        LoadRewardedAd();
    }

    private void OnAdFailed()
    {
        _currentState = AdUIState.Preview;


        if (_panel != null) _panel.style.display = DisplayStyle.Flex;
        if (_card != null) _card.style.display = DisplayStyle.Flex;

        _panel?.AddToClassList("visible");
        _card?.AddToClassList("visible");

        if (_timerLabel != null) _timerLabel.text = "Không thể tải quảng cáo!";
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.None;
        if (_adContainer != null) _adContainer.RemoveFromClassList("ad-playing");

        AudioListener.pause = _previousAudioListenerPause;
        if (MusicManager.Instance != null) MusicManager.Instance.ResumeMusic();

        SetButtonState(_watchButton, true, false, false, "THỬ LẠI");
        SetButtonState(_closeButton, true, false, true, "BỎ QUA");

        LoadRewardedAd();
    }

    private void SetButtonState(Button btn, bool isVisible, bool isSingle, bool isSecondary, string text)
    {
        if (btn == null) return;
        btn.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        btn.text = text;

        if (isSingle) btn.AddToClassList("ad-button-single");
        else btn.RemoveFromClassList("ad-button-single");

        if (isSecondary) btn.AddToClassList("btn-secondary");
        else btn.RemoveFromClassList("btn-secondary");
    }

    private void ApplyCachedReward()
    {
        if (_hasClaimedReward && _currentState != AdUIState.Claimed) return;
        _hasClaimedReward = true;

        switch (_cachedType)
        {
            case GiftBox.GiftRewardType.Coin:
                if (CoinManager.Instance != null)
                    CoinManager.Instance.AddCoins(_cachedAmount, false);
                break;

            case GiftBox.GiftRewardType.Exp:
                if (ExperienceManager.Instance != null)
                    ExperienceManager.Instance.AddExperience(_cachedAmount);
                break;

            case GiftBox.GiftRewardType.Health:
                if (_currentPlayer != null)
                {
                    var stats = _currentPlayer.GetComponent<PlayerStats>();
                    if (stats != null) stats.Heal(_cachedAmount);
                }
                break;

            case GiftBox.GiftRewardType.Ammo:
                if (_currentPlayer != null)
                {
                    var wRef = _currentPlayer.GetComponent<IWeaponReferenceProvider>();
                    bool granted = false;

                    if (wRef != null)
                    {
                        if (wRef.Id != null && wRef.Id.weapon != null && wRef.Id.weapon.shootStyle != cowsins.ShootStyle.Melee && wRef.Id.weapon.limitedMagazines)
                        {
                            wRef.Id.totalBullets += _cachedAmount;
                            granted = true;
                        }
                        else if (wRef.Inventory != null)
                        {
                            foreach (var w in wRef.Inventory)
                            {
                                if (w != null && w.weapon != null && w.weapon.shootStyle != cowsins.ShootStyle.Melee && w.weapon.limitedMagazines)
                                {
                                    w.totalBullets += _cachedAmount;
                                    granted = true;
                                    break;
                                }
                            }
                        }

                        if (granted)
                        {
                            var wEvents = _currentPlayer.GetComponent<IWeaponEventsProvider>();
                            if (wEvents != null && wEvents.Events != null)
                                wEvents.Events.OnAmmoChanged?.Invoke(false);
                        }
                    }

                    if (!granted && CoinManager.Instance != null)
                    {
                        CoinManager.Instance.AddCoins(_cachedAmount, false);
                    }
                }
                break;
        }
    }

    private void ClosePanelInternal()
    {
        if (_currentState == AdUIState.Idle && !_isPanelOpen) return;

        AdUIState prevState = _currentState;
        _currentState = AdUIState.Idle;
        _isPanelOpen = false;

        if (_adCoroutine != null)
        {
            StopCoroutine(_adCoroutine);
            _adCoroutine = null;
        }

        DestroyAd();

        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.ResumeMusic();
        }
        AudioListener.pause = _previousAudioListenerPause;


        if (_card != null) _card.style.display = DisplayStyle.Flex;

        if (_currentGiftBox != null && _currentGiftBox.gameObject != null)
        {
            if (prevState == AdUIState.Claimed)
            {
                _currentGiftBox.OnAdCompletedAndClaimed();
            }
            else
            {
                _currentGiftBox.OnAdCancelled();
            }
            _currentGiftBox = null;
        }

        // Close UI via PanelManager Immediate
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanelImmediate("AdReward", _panel, _card);
        }
        else
        {
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
