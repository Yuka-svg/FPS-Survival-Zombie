using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class StatsPanelUI : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.O;

    [Header("Animation")]
    public float fadeDuration = 0.18f;

    private UIDocument _doc;
    private VisualElement _root;
    private readonly List<Label> _labels = new List<Label>();
    private readonly List<Label> _values = new List<Label>();
    private readonly List<string> _lastValues = new List<string>();
    private bool _visible;
    public bool IsOpen => _visible;
    private float _fade;

    private static readonly string[] StatLabels =
    {
        "Play Time", "Distance Travelled", "Total Kills",
        "  Zombies", "  Boomers", "  Tanks",
        "Total Damage Dealt", "Health Lost", "Health Healed",
        "Crits", "Shots Fired", "Shots Hit", "Accuracy",
        "Reloads", "Journals Collected", "Deaths",
        "Coins", "Wave Reached", "Score", "Best Score"
    };

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null)
        {
            var canvasGo = GameObject.Find("GameUICanvas");
            if (canvasGo != null) _doc = canvasGo.GetComponent<UIDocument>();
        }
        if (_doc == null)
        {
            var pm = FindAnyObjectByType<PauseManager>();
            if (pm != null && pm.uiDocument != null) _doc = pm.uiDocument;
        }
        if (_doc == null) return;

        _root = _doc.rootVisualElement.Q("StatsPanel");
        if (_root == null) return;

        var grid = _root.Q("StatsGrid");
        if (grid == null) return;

        for (int i = 0; i < StatLabels.Length; i++)
        {
            var row = new VisualElement();
            row.name = "Row" + i;
            row.AddToClassList("stats-row");

            var label = new Label();
            label.name = "Label";
            label.text = StatLabels[i];
            label.AddToClassList("stats-label");
            if (StatLabels[i].StartsWith("  "))
                label.AddToClassList("sub");
            row.Add(label);
            _labels.Add(label);

            var value = new Label();
            value.name = "Value";
            value.text = "--";
            value.AddToClassList("stats-value");
            row.Add(value);
            _values.Add(value);

            grid.Add(row);
            _lastValues.Add(null);
        }

        _visible = false;
        _fade = 0f;
        _root.style.display = DisplayStyle.None;

        StartCoroutine(PollStats());
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
            bool pauseActive = PauseManager.Instance != null && PauseManager.Instance.IsOpenOrTransitioning;
            bool journalActive = JournalUI.Instance != null && JournalUI.Instance.IsOpenOrTransitioning;
            
            bool skillTreeActive = false;
            var skillTree = FindAnyObjectByType<SkillTreeWidget>();
            if (skillTree != null) skillTreeActive = skillTree.IsOpenOrTransitioning;

            if (_visible)
            {
                Toggle();
            }
            else if (!gameOver && !pauseActive && !journalActive && !skillTreeActive)
            {
                Toggle();
            }
        }
        float target = _visible ? 1f : 0f;
        if (!Mathf.Approximately(_fade, target))
            _fade = Mathf.MoveTowards(_fade, target, 1f / Mathf.Max(0.01f, fadeDuration) * Time.unscaledDeltaTime);
    }

    private System.Collections.IEnumerator PollStats()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (true)
        {
            if (_visible && _root != null)
                RefreshValues();
            yield return wait;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    public void Toggle()
    {
        SetVisible(!_visible);
    }

    private Coroutine _closeCoroutine;

    public void SetVisible(bool visible, bool instant = false)
    {
        _visible = visible;
        if (_root == null) return;

        if (visible)
        {
            if (PanelManager.Instance != null)
            {
                PanelManager.Instance.OpenPanel("Stats", _root, null, Toggle);
            }
            else
            {
                _root.style.display = DisplayStyle.Flex;
                _root.AddToClassList("visible");
            }
        }
        else
        {
            if (instant)
            {
                _root.RemoveFromClassList("visible");
                _root.style.display = DisplayStyle.None;
                if (PanelManager.Instance != null)
                {
                    PanelManager.Instance.RegisterPanelActive("Stats", false);
                }
            }
            else
            {
                if (PanelManager.Instance != null)
                {
                    PanelManager.Instance.ClosePanel("Stats", _root, null);
                }
                else
                {
                    _root.RemoveFromClassList("visible");
                    _root.style.display = DisplayStyle.None;
                }
            }
        }

        if (instant)
            _fade = visible ? 1f : 0f;
    }

    private void RefreshValues()
    {
        var t = PlayerStatsTracker.Instance;
        if (t == null) return;

        SetVal(0, PlayerStatsTracker.FormatTime(t.GetPlayTime()));
        SetVal(1, PlayerStatsTracker.FormatDistance(t.GetDistanceMoved()));
        SetVal(2, t.TotalKills.ToString());
        SetVal(3, t.zombieKills.ToString());
        SetVal(4, t.boomerKills.ToString());
        SetVal(5, t.tankKills.ToString());
        SetVal(6, PlayerStatsTracker.FormatDamage(t.totalDamageDealt));
        SetVal(7, PlayerStatsTracker.FormatHealth(t.GetHealthLost()));
        SetVal(8, PlayerStatsTracker.FormatHealth(t.GetHealthHealed()));
        SetVal(9, t.GetCrits().ToString());
        SetVal(10, t.GetShotsFired().ToString());
        SetVal(11, t.GetShotsHit().ToString());
        SetVal(12, t.GetAccuracy().ToString("F1") + "%");
        SetVal(13, t.GetReloadCount().ToString());
        int jCol = t.GetJournalsCollected();
        int jTot = t.GetJournalsTotal();
        SetVal(14, jTot > 0 ? $"{jCol} / {jTot}" : jCol.ToString());
        SetVal(15, t.GetDeathCount().ToString());
        SetVal(16, t.GetCoins().ToString());
        SetVal(17, t.GetWaveReached().ToString());
        SetVal(18, t.GetScore().ToString());
        SetVal(19, t.GetBestScore().ToString());
    }

    private void SetVal(int index, string value)
    {
        if (index >= _values.Count) return;
        if (_lastValues[index] == value) return;
        _lastValues[index] = value;
        _values[index].text = value;
    }
}
