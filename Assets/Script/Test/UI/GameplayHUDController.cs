using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class GameplayHUDController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _hudChips;
    private Label _waveLabel, _killsLabel, _scoreLabel, _coinsLabel, _critsLabel, _timerLabel, _collectiblesLabel;
    private VisualElement _waveChip, _killsChip, _scoreChip, _coinsChip, _critsChip, _timerChip, _collectiblesChip;

    private int _lastWave = -1, _lastKilled = -1, _lastToKill = -1;
    private int _lastKills = -1, _lastScore = -1, _lastCrits = -1, _lastSec = -1;
    private int _lastCollectibles = -1;
    private int _lastCoins = -1;

    private void Awake()
    {
        // Only freeze timeScale if we're in a gameplay scene (managers exist).
        // In non‑gameplay scenes (e.g. MainMenu), setting timeScale=0 would
        // freeze the entire game permanently.
        if (ScoreManager.Instance != null || WaveManager.Instance != null)
            Time.timeScale = 0f;

        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;

        _hudChips = _doc.rootVisualElement.Q("HUDChips");
        if (_hudChips == null) return;

        _waveChip = _hudChips.Q("WaveChip");
        _killsChip = _hudChips.Q("KillsChip");
        _scoreChip = _hudChips.Q("ScoreChip");
        _coinsChip = _hudChips.Q("CoinsChip");
        _critsChip = _hudChips.Q("HeadshotsChip");
        _timerChip = _hudChips.Q("TimerChip");
        _collectiblesChip = _hudChips.Q("CollectiblesChip");

        _waveLabel = _waveChip?.Q<Label>("WaveLabel");
        _killsLabel = _killsChip?.Q<Label>("KillsLabel");
        _scoreLabel = _scoreChip?.Q<Label>("ScoreLabel");
        _coinsLabel = _coinsChip?.Q<Label>("CoinsLabel");
        _critsLabel = _critsChip?.Q<Label>("HeadshotsLabel");
        _timerLabel = _timerChip?.Q<Label>("TimerLabel");
        _collectiblesLabel = _collectiblesChip?.Q<Label>("CollectiblesLabel");

        if (CowsinsHUDAdapter.Instance != null)
            CowsinsHUDAdapter.Instance.OnCoinsChanged += OnCoinsChanged;

        RegisterChipBackgrounds();
    }

    private void Start()
    {
        StartCoroutine(StartFadeOut());
    }

    private IEnumerator StartFadeOut()
    {
        if (_doc == null) yield break;
        var root = _doc.rootVisualElement;
        var overlay = root?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks while black
            overlay.RemoveFromClassList("fade-out");
        }

        yield return null;

        if (overlay != null)
        {
            overlay.AddToClassList("fade-out"); // Starts 3s fade-out in USS
        }

        // Wait 3.0 seconds in REALTIME because Time.timeScale is 0!
        yield return new WaitForSecondsRealtime(3.0f);

        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Ignore; // Allow clicks to pass through after fade-out completes
        }

        // Unfreeze timescale to resume gameplay — but only if no panel has
        // opened during the fade (e.g. the Story intro appears right after the
        // black overlay; PanelManager already froze time for it). Clobbering
        // timeScale here would let the game run while a panel is on screen.
        if (PanelManager.Instance == null || !PanelManager.Instance.IsAnyPanelActive())
        {
            Time.timeScale = 1f;
        }
    }

    private void OnEnable()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnKillsChanged += OnKillsChanged;
            ScoreManager.Instance.OnScoreChanged += OnScoreChanged;
            ScoreManager.Instance.OnCritsChanged += OnCritsChanged;
        }
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            WaveManager.Instance.OnWaveCompleted += OnWaveCompleted;
        }
        StartCoroutine(TimerRoutine());
    }

    private void OnDisable()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnKillsChanged -= OnKillsChanged;
            ScoreManager.Instance.OnScoreChanged -= OnScoreChanged;
            ScoreManager.Instance.OnCritsChanged -= OnCritsChanged;
        }
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveCompleted -= OnWaveCompleted;
        }
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        if (CowsinsHUDAdapter.Instance != null)
            CowsinsHUDAdapter.Instance.OnCoinsChanged -= OnCoinsChanged;

        UnregisterChipBackgrounds();
    }

    private void ShowChip(VisualElement chip, bool show)
    {
        if (chip == null) return;
        if (show)
            chip.RemoveFromClassList("hud-chip-hidden");
        else
            chip.AddToClassList("hud-chip-hidden");
        chip.MarkDirtyRepaint();
    }

    private void OnWaveStarted(int wave)
    {
        if (_waveChip == null || _waveLabel == null) return;
        ShowChip(_waveChip, true);
        var wm = WaveManager.Instance;
        if (wm == null) return;
        _lastWave = wm.currentWave;
        _lastKilled = wm.zombiesKilledThisWave;
        _lastToKill = wm.zombiesToKill;
        _waveLabel.text = $"Wave {wm.currentWave} \u00B7 {wm.zombiesKilledThisWave}/{wm.zombiesToKill}";
    }

    private void OnWaveCompleted(int wave)
    {
        if (_waveChip == null || _waveLabel == null) return;
        _waveLabel.text = $"Wave {wave} Complete!";
    }

    private void OnKillsChanged()
    {
        if (_killsChip == null || _killsLabel == null) return;
        var sm = ScoreManager.Instance;
        if (sm == null) return;
        ShowChip(_killsChip, sm.kills > 0);
        if (sm.kills != _lastKills)
        {
            _lastKills = sm.kills;
            _killsLabel.text = "Kills : " + sm.kills;
        }
    }

    private void OnScoreChanged()
    {
        if (_scoreChip == null || _scoreLabel == null) return;
        var sm = ScoreManager.Instance;
        if (sm == null) return;
        ShowChip(_scoreChip, sm.score > 0);
        if (sm.score != _lastScore)
        {
            _lastScore = sm.score;
            _scoreLabel.text = "Score : " + sm.score;
        }
    }

    private void OnCritsChanged()
    {
        if (_critsChip == null || _critsLabel == null) return;
        var sm = ScoreManager.Instance;
        if (sm == null) return;
        ShowChip(_critsChip, sm.crits > 0);
        if (sm.crits != _lastCrits)
        {
            _lastCrits = sm.crits;
            _critsLabel.text = "Crits : " + sm.crits;
        }
    }

    private void OnCoinsChanged(int coins)
    {
        if (_coinsLabel == null || _coinsChip == null) return;
        ShowChip(_coinsChip, coins > 0);
        if (coins != _lastCoins)
        {
            _lastCoins = coins;
            _coinsLabel.text = "Coins : " + coins;
        }
    }

    private IEnumerator TimerRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            UpdateTimer();
            UpdateCollectibles();
        }
    }

    private void UpdateTimer()
    {
        var sm = ScoreManager.Instance;
        if (sm == null || _timerChip == null || _timerLabel == null) return;

        int sec = Mathf.FloorToInt(sm.GetSurvivalTime());
        ShowChip(_timerChip, sec > 0);
        if (sec > 0 && sec != _lastSec)
        {
            _lastSec = sec;
            int m = sec / 60, s = sec % 60;
            _timerLabel.text = $"{m:00}:{s:00}";
        }
    }

    private void UpdateCollectibles()
    {
        if (GameModeManager.CurrentMode == GameMode.Endless)
        {
            ShowChip(_collectiblesChip, false);
            return;
        }

        var cm = CollectibleManager.Instance;
        if (cm == null || _collectiblesChip == null || _collectiblesLabel == null) return;

        int c = cm.Count;
        ShowChip(_collectiblesChip, c > 0);
        if (c > 0 && c != _lastCollectibles)
        {
            _lastCollectibles = c;
            _collectiblesLabel.text = $"Journals : {c}/{cm.Total}";
        }
    }

    private void RegisterChipBackgrounds()
    {
        if (_waveChip != null) _waveChip.generateVisualContent += OnGenerateChipBackground;
        if (_killsChip != null) _killsChip.generateVisualContent += OnGenerateChipBackground;
        if (_scoreChip != null) _scoreChip.generateVisualContent += OnGenerateChipBackground;
        if (_coinsChip != null) _coinsChip.generateVisualContent += OnGenerateChipBackground;
        if (_critsChip != null) _critsChip.generateVisualContent += OnGenerateChipBackground;
        if (_timerChip != null) _timerChip.generateVisualContent += OnGenerateChipBackground;
        if (_collectiblesChip != null) _collectiblesChip.generateVisualContent += OnGenerateChipBackground;
    }

    private void UnregisterChipBackgrounds()
    {
        if (_waveChip != null) _waveChip.generateVisualContent -= OnGenerateChipBackground;
        if (_killsChip != null) _killsChip.generateVisualContent -= OnGenerateChipBackground;
        if (_scoreChip != null) _scoreChip.generateVisualContent -= OnGenerateChipBackground;
        if (_coinsChip != null) _coinsChip.generateVisualContent -= OnGenerateChipBackground;
        if (_critsChip != null) _critsChip.generateVisualContent -= OnGenerateChipBackground;
        if (_timerChip != null) _timerChip.generateVisualContent -= OnGenerateChipBackground;
        if (_collectiblesChip != null) _collectiblesChip.generateVisualContent -= OnGenerateChipBackground;
    }

    private void OnGenerateChipBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 8f;

        // 1. Draw solid dark blue-gray translucent background shape (0.85 alpha as requested)
        Color fillCol = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
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

        // 2. Identify themed accent color based on element name
        Color accentCol = Color.white;
        string name = targetElement.name;
        if (name == "WaveChip" || name == "TimerChip")
        {
            accentCol = new Color(78f / 255f, 205f / 255f, 196f / 255f, 1.0f); // Cyan
        }
        else if (name == "KillsChip" || name == "HeadshotsChip")
        {
            accentCol = new Color(229f / 255f, 72f / 255f, 60f / 255f, 1.0f); // Red
        }
        else if (name == "ScoreChip" || name == "CoinsChip")
        {
            accentCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 1.0f); // Gold
        }
        else if (name == "CollectiblesChip")
        {
            accentCol = new Color(179f / 255f, 217f / 255f, 255f / 255f, 1.0f); // Blue-Light
        }

        // 3. Draw left accent vertical line (4px thick) along the left beveled edge
        painter.strokeColor = accentCol;
        painter.lineWidth = 4f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(2f, chamferSize));
        painter.LineTo(new Vector2(2f, rect.height - chamferSize));
        painter.Stroke();

        // 4. Draw outer border (faint white/gray line)
        Color strokeCol = new Color(255f / 255f, 255f / 255f, 255f / 255f, 0.1f);
        painter.strokeColor = strokeCol;
        painter.lineWidth = 1.0f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 5. Draw 4 3D metallic gold mini rivets (screws) in the corners
        System.Action<Vector2> drawRivet = center =>
        {
            // rivet shadow
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.3f, 0.3f), 1.8f, 0f, 360f);
            painter.Fill();

            // rivet body
            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f); // Gold screw head
            painter.BeginPath();
            painter.Arc(center, 1.5f, 0f, 360f);
            painter.Fill();

            // rivet highlight
            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.4f, 0.4f), 0.3f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 6f;
        drawRivet(new Vector2(rOffset + 2f, rOffset)); // offset slightly to the right of the 4px left bar
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rOffset + 2f, rect.height - rOffset));
    }
}
