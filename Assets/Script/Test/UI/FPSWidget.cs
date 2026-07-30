using UnityEngine;
using UnityEngine.UIElements;

public class FPSWidget : MonoBehaviour
{
    [Tooltip("Seconds between text refreshes.")] public float refreshRate = 0.5f;
    [Tooltip("Number of frames averaged for a stable reading.")] public int window = 50;

    private static readonly Color Good = new(0.31f, 0.878f, 0.541f, 0.45f);
    private static readonly Color Ok = new(1f, 0.83f, 0.30f, 0.45f);
    private static readonly Color Bad = new(0.85f, 0.35f, 0.15f, 0.45f);

    private Label _label;
    private float[] _deltas;
    private int _index;
    private float _timer;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        _label = doc.rootVisualElement.Q<Label>("FPSLabel");
        if (_label == null) { enabled = false; return; }
        _deltas = new float[Mathf.Max(8, window)];
        _timer = refreshRate;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        _deltas[_index] = dt;
        _index = (_index + 1) % _deltas.Length;

        _timer -= dt;
        if (_timer > 0f) return;
        _timer = refreshRate;

        float total = 0f;
        for (int i = 0; i < _deltas.Length; i++) total += _deltas[i];
        float fps = total > 0f ? _deltas.Length / total : 0f;
        int rounded = Mathf.RoundToInt(fps);
        Color c = fps < 15f ? Bad : (fps < 45f ? Ok : Good);
        if (_label != null) { _label.style.color = c; _label.text = rounded + " FPS"; }
    }
}
