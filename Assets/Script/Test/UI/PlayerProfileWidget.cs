using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerProfileWidget : MonoBehaviour
{
    [Header("Panel Size (px @1920x1080)")]
    public float chipWidth = 280f;
    public float chipHeight = 48f;
    public float panelWidth = 460f;

    private static readonly Color ButtonColor = new Color(0.31f, 0.878f, 0.541f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.78f, 0.45f, 1f);
    private static readonly Color TextMuted = new Color(0.62f, 0.66f, 0.72f, 1f);

    private UIDocument _doc;
    private VisualElement _chip;
    private Label _chipUsername;
    private Label _chipStatus;
    private VisualElement _chipAvatar;
    private VisualElement _panel;
    private Label _panelUsername;
    private Label _panelId;
    private Label _panelBestScore;
    private Label _panelBestWave;
    private Label _panelAchievements;
    private VisualElement _achievementList;
    private bool _panelVisible;
    private Coroutine _transitionCoroutine;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) { enabled = false; return; }

        var root = _doc.rootVisualElement;
        _chip = root.Q("MainMenuModule_Profile");
        _chipUsername = root.Q<Label>("Username");
        _chipStatus = root.Q<Label>("Status");
        _chipAvatar = root.Q("Avatar");
        _panel = root.Q("ProfilePanel");
        _panelUsername = root.Q<Label>("PanelUsername");
        _panelId = root.Q<Label>("PanelId");
        _panelBestScore = root.Q<Label>("PanelBestScore");
        _panelBestWave = root.Q<Label>("PanelBestWave");
        _panelAchievements = root.Q<Label>("PanelAchievements");
        _achievementList = root.Q("AchList");

        if (_chip != null)
        {
            _chip.focusable = true;
            _chip.RegisterCallback<ClickEvent>(_ => TogglePanel());
        }
        var closeBtn = root.Q("ProfileCloseButton");
        if (closeBtn != null) closeBtn.RegisterCallback<ClickEvent>(_ => SetPanelVisible(false));

        if (_panel != null)
        {
            _panel.style.display = DisplayStyle.None;
            _panel.generateVisualContent += OnGeneratePanelBackground;
        }
        if (_chip != null) _chip.style.display = DisplayStyle.None;
    }

    private void OnEnable() { StartCoroutine(Bind()); }

    private void OnDisable()
    {
        var pm = PlayFabManager.Instance;
        if (pm != null)
        {
            pm.OnLoginSuccess -= HandleLoginSuccess;
            pm.OnLoginError -= HandleLoginError;
            pm.OnCloudDataLoaded -= HandleCloudDataLoaded;
            pm.OnLogout -= HandleLogout;
        }
        StopAllCoroutines();
    }

    private IEnumerator Bind()
    {
        float timeout = 10f;
        while (PlayFabManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        var pm = PlayFabManager.Instance;
        if (pm != null)
        {
            pm.OnLoginSuccess += HandleLoginSuccess;
            pm.OnLoginError += HandleLoginError;
            pm.OnCloudDataLoaded += HandleCloudDataLoaded;
            pm.OnLogout += HandleLogout;
        }

        RefreshChip();
    }

    private void TogglePanel()
    {
        var pm = PlayFabManager.Instance;
        bool loggedIn = pm != null && pm.IsLoggedIn;

        if (!loggedIn)
        {
            SetPanelVisible(false);
            var loginUI = FindLoginUI();
            if (loginUI != null)
                loginUI.ShowLoginPanel();
            else
                Debug.LogWarning("[PlayerProfileWidget] PlayFabLoginUI not found.");
            return;
        }

        SetPanelVisible(!_panelVisible);
    }

    public bool IsPanelVisible => _panelVisible;

    public void SetPanelVisible(bool visible)
    {
        if (visible)
        {
            var leaderboard = FindFirstObjectByType<LeaderboardWidget>();
            if (leaderboard != null && leaderboard.IsPanelVisible)
            {
                leaderboard.SetPanelVisible(false);
            }
        }

        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(AnimatePanel(visible));
    }

    private IEnumerator AnimatePanel(bool visible)
    {
        _panelVisible = visible;
        var mainMenu = FindMainMenuContent();

        if (visible)
        {
            if (mainMenu != null) mainMenu.SetActive(false);
            RefreshPanel();
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.Flex;
                _panel.RemoveFromClassList("slide-in");
            }
            yield return null;
            if (_panel != null) _panel.AddToClassList("slide-in");
        }
        else
        {
            if (_panel != null) _panel.RemoveFromClassList("slide-in");
            yield return new WaitForSeconds(1.5f);
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (mainMenu != null) mainMenu.SetActive(true);
        }
    }

    private GameObject FindMainMenuContent()
    {
        var parent = transform.parent;
        if (parent != null)
        {
            var contentTr = parent.Find("Content");
            if (contentTr != null) return contentTr.gameObject;
        }
        return null;
    }

    private PlayFabLoginUI FindLoginUI()
    {
        var parent = transform.parent;
        if (parent != null) return parent.GetComponentInChildren<PlayFabLoginUI>(true);
        return null;
    }

    private void HandleLoginSuccess(string username) { RefreshChip(); if (_panelVisible) RefreshPanel(); }
    private void HandleLoginError(string error) { RefreshChip(); }
    private void HandleCloudDataLoaded(bool success) { RefreshChip(); if (_panelVisible) RefreshPanel(); }
    private void HandleLogout() { RefreshChip(); if (_panelVisible) RefreshPanel(); }

    private void OnLogoutClicked()
    {
        PlayFabManager.Instance?.Logout();
        RefreshChip();
        RefreshPanel();
    }

    private void RefreshChip()
    {
        var pm = PlayFabManager.Instance;
        if (pm != null && pm.IsLoggedIn)
        {
            if (_chipUsername != null) _chipUsername.text = pm.Username ?? "Player";
            if (_chipStatus != null) { _chipStatus.text = "Online"; _chipStatus.style.color = ButtonColor; }
            if (_chipAvatar != null) _chipAvatar.style.backgroundColor = ButtonColor;
        }
        else
        {
            if (_chipUsername != null) _chipUsername.text = "Not logged in";
            if (_chipStatus != null) { _chipStatus.text = "Click to login"; _chipStatus.style.color = TextMuted; }
            if (_chipAvatar != null) _chipAvatar.style.backgroundColor = TextMuted;
        }
    }

    private void RefreshPanel()
    {
        var pm = PlayFabManager.Instance;
        bool loggedIn = pm != null && pm.IsLoggedIn;

        if (_panelUsername != null)
        {
            _panelUsername.text = loggedIn ? (pm.Username ?? "Player") : "Not logged in";
            _panelUsername.style.color = loggedIn ? Color.white : TextMuted;
        }
        if (_panelId != null) _panelId.text = loggedIn ? (pm.PlayFabId ?? "---") : "---";

        int bestScore = PlayerPrefs.GetInt("BestScore", 0);
        int bestWave = PlayerPrefs.GetInt("BestWave", 0);
        if (_panelBestScore != null) _panelBestScore.text = bestScore.ToString();
        if (_panelBestWave != null) _panelBestWave.text = bestWave.ToString();

        int unlocked = 0;
        int total = 0;
        _achievementList?.Clear();

        var am = AchievementManager.Instance;
        if (am != null && am.achievements != null)
        {
            total = am.achievements.Length;
            foreach (var ach in am.achievements)
            {
                if (ach == null) continue;
                bool isUnlocked = PlayerPrefs.GetInt(ach.UnlockedKey, 0) == 1;
                if (isUnlocked) unlocked++;

                // Container
                var item = new VisualElement();
                item.AddToClassList("ach-item");
                if (isUnlocked) item.AddToClassList("unlocked");

                // Left column: Icon + Info
                var leftCol = new VisualElement();
                leftCol.style.flexDirection = FlexDirection.Row;
                leftCol.style.alignItems = Align.Center;

                // Star icon
                var icon = new VisualElement();
                icon.AddToClassList("ach-item-icon");
                if (isUnlocked) icon.AddToClassList("unlocked");
                leftCol.Add(icon);

                // Text stack
                var textStack = new VisualElement();
                textStack.style.flexDirection = FlexDirection.Column;

                var titleLbl = new Label(ach.title);
                titleLbl.AddToClassList("ach-item-title");
                textStack.Add(titleLbl);

                if (ach.isProgression)
                {
                    int currentProgress = am.GetProgress(ach);
                    var progressLbl = new Label($"PROGRESS: {currentProgress} / {ach.targetValue}");
                    progressLbl.AddToClassList("ach-item-progress-text");
                    textStack.Add(progressLbl);
                }
                leftCol.Add(textStack);
                item.Add(leftCol);

                // Right column: Status stamp
                var statusLbl = new Label(isUnlocked ? "COMPLETED" : "LOCKED");
                statusLbl.AddToClassList("ach-item-status");
                if (isUnlocked) statusLbl.AddToClassList("unlocked");
                item.Add(statusLbl);

                _achievementList?.Add(item);
            }
        }

        if (_panelAchievements != null)
        {
            _panelAchievements.text = $"{unlocked} / {total} unlocked";
            _panelAchievements.style.color = unlocked > 0 ? AccentColor : TextMuted;
        }
    }

    private void Update()
    {
        if (_panelVisible && _panel != null)
        {
            _panel.MarkDirtyRepaint();
        }
    }

    private void OnGeneratePanelBackground(MeshGenerationContext mgc)
    {
        var rect = _panel.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 16f;

        // 1. Draw solid dark blue-gray translucent background shape to match HUD modules
        Color fillCol = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.94f);
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

        // 2. Draw yellow-black diagonal warning stripes at the top edge
        float badgeW = 60f;
        float badgeH = 7f;
        float startX = rect.width - badgeW - 24f;
        float startY = 4f;

        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 6f)
        {
            // Yellow stripe
            painter.strokeColor = new Color(230f / 255f, 180f / 255f, 20f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 4f, startY + badgeH));
            painter.Stroke();

            // Black stripe
            painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset + 3f, startY));
            painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
            painter.Stroke();
        }

        // 3. Draw outer border with breathing glow
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 0.45f);
        Color strokeCol = new Color(230f / 255f, 80f / 255f, 40f / 255f, pulse);
        float lineWidth = 1.5f;

        painter.strokeColor = strokeCol;
        painter.lineWidth = lineWidth;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 4. Draw inner offset double-line border
        float d = 3.5f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.15f);
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

        // 5. Draw 4 3D metallic corner rivets (screws)
        System.Action<Vector2> drawRivet = center =>
        {
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.5f, 0.5f), 3.5f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 110f / 255f, 75f / 255f, 1.0f);
            painter.BeginPath();
            painter.Arc(center, 3.0f, 0f, 360f);
            painter.Fill();

            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.8f, 0.8f), 0.6f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 10f;
        drawRivet(new Vector2(rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rOffset, rect.height - rOffset));
    }

    private void OnDestroy()
    {
        if (_panel != null)
        {
            _panel.generateVisualContent -= OnGeneratePanelBackground;
        }
    }
}
