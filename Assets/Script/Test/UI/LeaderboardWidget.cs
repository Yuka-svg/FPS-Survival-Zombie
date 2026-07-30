using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class LeaderboardWidget : MonoBehaviour
{
    [Header("Panel Size (px @1920x1080)")]
    public float rowHeight = 56f;
    public int maxResults = 10;
    public float chipWidth = 200f;
    public float chipHeight = 48f;

    private static readonly Color AccentColor = new Color(0.85f, 0.78f, 0.45f, 1f);
    private static readonly Color RowDefault = new Color(0.10f, 0.12f, 0.15f, 1f);
    private static readonly Color RowHighlight = new Color(0.85f, 0.78f, 0.45f, 0.15f);

    private VisualElement _chip;
    private VisualElement _panel;
    private VisualElement _scrim;
    private VisualElement _listContainer;
    private Label _statusText;

    private bool _panelVisible;
    private bool _loading;
    private List<PlayFabManager.LeaderboardEntry> _entries;
    private UIDocument _doc;
    private bool _initialized;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) { enabled = false; return; }

        _initialized = true;

        var root = _doc.rootVisualElement;
        _chip = root.Q("MainMenuModule_Rankings");
        _scrim = root.Q("LeaderboardScrim");
        _panel = root.Q("LeaderboardPanel");
        _listContainer = _panel?.Q("ListContent");
        _statusText = _panel?.Q<Label>("StatusText");

        if (_chip != null)
        {
            _chip.RegisterCallback<ClickEvent>(_ => TogglePanel());
        }
        if (_scrim != null) _scrim.RegisterCallback<ClickEvent>(_ => SetPanelVisible(false));

        var closeBtn = root.Q("LeaderboardCloseButton");
        if (closeBtn != null) closeBtn.RegisterCallback<ClickEvent>(_ => SetPanelVisible(false));

        var refreshBtn = root.Q("RefreshButton");
        if (refreshBtn != null) refreshBtn.RegisterCallback<ClickEvent>(_ => RefreshLeaderboard());

        if (_panel != null)
        {
            _panel.style.display = DisplayStyle.None;
            _panel.generateVisualContent += OnGeneratePanelBackground;
        }
        if (_scrim != null) _scrim.style.display = DisplayStyle.None;
        if (_chip != null) _chip.style.display = DisplayStyle.None;
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        StartCoroutine(Bind());
    }

    private void OnDisable()
    {
        var pm = PlayFabManager.Instance;
        if (pm != null)
        {
            pm.OnLoginSuccess -= HandleLoginSuccess;
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
            pm.OnLogout += HandleLogout;
            UpdateChipVisibility();
        }
    }

    private void HandleLoginSuccess(string username)
    {
        UpdateChipVisibility();
        if (_panelVisible) RefreshLeaderboard();
    }

    private void HandleLogout()
    {
        UpdateChipVisibility();
        if (_panelVisible) SetPanelVisible(false);
    }

    private void UpdateChipVisibility()
    {
    }

    private void TogglePanel() => SetPanelVisible(!_panelVisible);

    public bool IsPanelVisible => _panelVisible;

    public void SetPanelVisible(bool visible)
    {
        if (visible)
        {
            var profile = FindFirstObjectByType<PlayerProfileWidget>();
            if (profile != null && profile.IsPanelVisible)
            {
                profile.SetPanelVisible(false);
            }
        }

        StopAllCoroutines();
        StartCoroutine(AnimatePanel(visible));

        if (visible)
        {
            RefreshLeaderboard();
        }
    }

    private IEnumerator AnimatePanel(bool show)
    {
        var mainMenu = FindMainMenuContent();
        _panelVisible = show;

        if (show)
        {
            if (mainMenu != null) mainMenu.SetActive(false);
            if (_scrim != null)
            {
                _scrim.style.display = DisplayStyle.Flex;
                _scrim.style.opacity = 0f;
            }
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.Flex;
                _panel.RemoveFromClassList("slide-in");
            }
            
            yield return null;
            
            if (_scrim != null) _scrim.style.opacity = 0.8f;
            if (_panel != null) _panel.AddToClassList("slide-in");
        }
        else
        {
            if (_scrim != null) _scrim.style.opacity = 0f;
            if (_panel != null) _panel.RemoveFromClassList("slide-in");
            
            yield return new WaitForSeconds(1.5f);
            
            if (_scrim != null) _scrim.style.display = DisplayStyle.None;
            if (_panel != null) _panel.style.display = DisplayStyle.None;
            if (mainMenu != null) mainMenu.SetActive(true);
        }
    }

    public void RefreshLeaderboard()
    {
        var pm = PlayFabManager.Instance;
        if (pm == null || !pm.IsLoggedIn)
        {
            if (_statusText != null) _statusText.text = "Please log in to view the leaderboard.";
            ClearRows();
            return;
        }

        if (_loading) return;
        _loading = true;
        if (_statusText != null) _statusText.text = "Loading...";
        ClearRows();

        pm.GetLeaderboard(maxResults, entries =>
        {
            _loading = false;
            _entries = entries;

            if (entries == null)
            {
                if (_statusText != null) _statusText.text = "Failed to load leaderboard. Please try again.";
                ClearRows();
                return;
            }

            if (entries.Count == 0)
            {
                if (_statusText != null) _statusText.text = "No leaderboard data yet.";
                ClearRows();
                return;
            }

            if (_statusText != null) _statusText.text = $"Top {entries.Count} players";
            BuildRows(entries);
        });
    }

    private void BuildRows(List<PlayFabManager.LeaderboardEntry> entries)
    {
        ClearRows();
        if (_listContainer == null) return;

        string myPlayFabId = PlayFabManager.Instance != null ? PlayFabManager.Instance.PlayFabId : null;

        foreach (var entry in entries)
        {
            bool isMe = entry.playFabId == myPlayFabId;

            var row = new VisualElement();
            row.name = $"Row_{entry.rank}";
            row.AddToClassList("leaderboard-row");
            if (isMe) row.AddToClassList("is-me");
            if (entry.rank == 1) row.AddToClassList("rank-1");
            else if (entry.rank == 2) row.AddToClassList("rank-2");
            else if (entry.rank == 3) row.AddToClassList("rank-3");
            
            _listContainer.Add(row);

            string rankStr = entry.rank <= 3 ? $"#{entry.rank}" : entry.rank.ToString();
            var rankLbl = new Label(rankStr);
            rankLbl.AddToClassList("row-rank");
            row.Add(rankLbl);

            var nameLbl = new Label(entry.displayName);
            nameLbl.AddToClassList("row-name");
            row.Add(nameLbl);

            var scoreLbl = new Label(entry.score.ToString());
            scoreLbl.AddToClassList("row-score");
            row.Add(scoreLbl);
        }
    }

    private void ClearRows() { _listContainer?.Clear(); }

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

        // 2. Draw tactical thin neon orange border
        Color borderCol = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.22f);
        painter.strokeColor = borderCol;
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

        // 3. Draw mini accent ticks in corners for industrial HUD aesthetic
        painter.strokeColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.6f);
        painter.lineWidth = 2f;
        // Top-left chamfer tick
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, chamferSize + 4f));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.LineTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(chamferSize + 4f, 0));
        painter.Stroke();
        // Bottom-right chamfer tick
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width, rect.height - chamferSize - 4f));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(rect.width - chamferSize - 4f, rect.height));
        painter.Stroke();
    }

    private void OnDestroy()
    {
        // Shared UIDocument is managed by the MainMenu scene GameObject, do not destroy it.
    }
}
