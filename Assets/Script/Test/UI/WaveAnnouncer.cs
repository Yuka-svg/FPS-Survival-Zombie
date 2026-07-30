using UnityEngine;
using UnityEngine.UIElements;

public class WaveAnnouncer : MonoBehaviour
{
    [Tooltip("Score bonus per cleared wave (mirrors WaveManager.NextWave: wave*500).")]
    public int bonusPerWave = 500;
    public float fadeIn = 0.3f, hold = 2.2f, fadeOut = 0.7f;

    private VisualElement _root;
    private Label _title;
    private Label _sub;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        _root = doc.rootVisualElement.Q("WaveAnnouncer");
        if (_root == null) return;
        _title = _root.Q<Label>("WaveTitle");
        _sub = _root.Q<Label>("WaveSub");
        _root.style.display = DisplayStyle.None;

        var wm = WaveManager.Instance;
        if (wm != null)
        {
            wm.OnWaveStarted += OnWaveStarted;
            wm.OnWaveCompleted += OnWaveCompleted;
            if (wm.currentWave > 0)
            {
                _title.text = "WAVE " + wm.currentWave;
                _sub.text = "SURVIVE";
                Show();
            }
        }
    }

    private void OnDisable()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveCompleted -= OnWaveCompleted;
        }
    }

    private void OnWaveStarted(int wave)
    {
        if (_title != null) _title.text = "WAVE " + wave;
        if (_sub != null) _sub.text = "SURVIVE";
        Show();
    }

    private void OnWaveCompleted(int wave)
    {
        if (_title != null) _title.text = "WAVE " + wave;
        if (_sub != null) _sub.text = "WAVE CLEARED   +" + (wave * bonusPerWave);
        Show();
    }

    private void Show()
    {
        _root.style.display = DisplayStyle.Flex;
        _root.schedule.Execute(() => {
            if (_root != null) _root.EnableInClassList("wave-visible", true);
        });

        float totalVisible = fadeIn + hold;
        _root.schedule.Execute(() => {
            if (_root != null) _root.EnableInClassList("wave-visible", false);
        }).StartingIn(Mathf.RoundToInt(totalVisible * 1000f));

        _root.schedule.Execute(() => {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }).StartingIn(Mathf.RoundToInt((totalVisible + fadeOut) * 1000f));
    }
}
