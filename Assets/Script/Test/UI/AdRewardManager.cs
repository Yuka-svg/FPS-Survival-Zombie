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

    private UIDocument _doc;
    private VisualElement _panel;
    private Label _titleLabel;
    private Label _timerLabel;
    private Label _rewardLabel;
    private VisualElement _adContainer;
    private VisualElement _adPlayingOverlay;
    private Button _watchButton;
    private Button _closeButton;
    private bool _ready;
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
        DestroyAd();
    }

    private void SetupUI()
    {
        if (_doc == null) _doc = GetComponent<UIDocument>();
        if (_doc == null)
        {
            var go = GameObject.Find("GameUICanvas");
            if (go != null) _doc = go.GetComponent<UIDocument>();
        }
        if (_doc == null || _doc.rootVisualElement == null) return;

        var root = _doc.rootVisualElement;
        _panel = root.Q("AdRewardPanel");
        if (_panel == null) return;

        _titleLabel = _panel.Q<Label>("AdTitle");
        _timerLabel = _panel.Q<Label>("AdTimer");
        _rewardLabel = _panel.Q<Label>("AdRewardText");
        _adContainer = _panel.Q("AdContent");
        _adPlayingOverlay = _panel.Q("AdPlayingOverlay");
        _watchButton = _panel.Q<Button>("WatchAdButton");
        _closeButton = _panel.Q<Button>("AdCloseButton");

        _panel.style.display = DisplayStyle.None;

        if (_watchButton != null) _watchButton.clicked += StartAd;
        if (_closeButton != null) _closeButton.clicked += ClosePanel;

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

    public void ShowAd(Transform player)
    {
        Debug.Log("[AdReward] ShowAd called. Player=" + (player != null ? player.name : "null") + " _ready=" + _ready);

        _currentPlayer = player;
        _playerControl = player != null ? player.GetComponentInChildren<PlayerControl>() : null;

        if (!_ready)
        {
            SetupUI();
            if (!_ready) return;
        }

        // Use PanelManager to properly register the panel and handle pause/control/cursor
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("AdReward", _panel, null, () =>
            {
                Debug.Log("[AdReward] Panel close callback triggered.");
            });
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
        _watchButton.style.display = DisplayStyle.None;
        if (_closeButton != null) _closeButton.style.display = DisplayStyle.None;
        if (_adContainer != null) _adContainer.RemoveFromClassList("ad-playing");
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.None;

        // Auto-play ad
        StartAd();
    }

    private void StartAd()
    {
        if (_watchButton != null) _watchButton.style.display = DisplayStyle.None;
        if (_adContainer != null) _adContainer.AddToClassList("ad-playing");
        if (_adPlayingOverlay != null) _adPlayingOverlay.style.display = DisplayStyle.Flex;

#if UNITY_EDITOR
        StartCoroutine(EditorSimulateAd());
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
            StartCoroutine(WaitForAdThenShow());
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
    }

    private void GrantRandomReward()
    {
        string[] rewards = { "Coin", "Exp", "Ammo", "Health" };
        string selected = rewards[Random.Range(0, rewards.Length)];

        switch (selected)
        {
            case "Coin":
                if (CoinManager.Instance != null)
                {
                    CoinManager.Instance.AddCoins(coinAmount, false);
                    if (_rewardLabel != null)
                        _rewardLabel.text = $"+{coinAmount} Coins!";
                }
                break;

            case "Exp":
                if (ExperienceManager.Instance != null)
                {
                    ExperienceManager.Instance.AddExperience(expAmount);
                    if (_rewardLabel != null)
                        _rewardLabel.text = $"+{expAmount} EXP!";
                }
                break;

            case "Ammo":
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
                            _rewardLabel.text = $"+{amount} đạn!";
                    }
                }
                break;

            case "Health":
                if (_currentPlayer != null)
                {
                    var stats = _currentPlayer.GetComponent<PlayerStats>();
                    if (stats != null)
                    {
                        stats.Heal(healthAmount);
                        if (_rewardLabel != null)
                            _rewardLabel.text = $"+{healthAmount} HP!";
                    }
                }
                break;
        }

        LoadRewardedAd();
    }

    private void ClosePanel()
    {
        if (_isAdReady && _rewardedAd != null)
            _rewardedAd.Destroy();
        _isAdReady = false;
        _rewardedAd = null;

        // Use PanelManager to properly close and restore state
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("AdReward", _panel, null);
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
}
