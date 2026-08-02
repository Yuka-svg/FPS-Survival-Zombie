using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

/// <summary>
/// Ending slide (cure announcement). No longer fades itself or toggles
/// Time.timeScale — EndingSequenceManager owns the blackout overlay and keeps
/// time frozen across the whole ending chain. This component only builds its
/// panel, animates the title/divider in, typewrites the body, holds, then
/// signals completion. Fades in/out happen through EndingBlackout.
/// </summary>
public class EpilogueSlide : MonoBehaviour
{
    [Header("Content")]
    [TextArea(3, 8)]
    public string bodyText =
        "Dịch bệnh đã được kiểm soát, đã tìm ra phương thuốc, nhưng danh tính người đem " +
        "phương thuốc về cho các nhà khoa học vẫn là một ẩn số.";

    public string titleText = "KẾT THÚC";

    [Tooltip("Optional illustration shown above the text. Leave empty for now — assign later.")]
    public Sprite illustration;

    [Header("Timing")]
    public float titleFadeIn = 0.9f;
    public float dividerExpand = 0.6f;
    [Tooltip("Seconds per character while typewriting the body text.")]
    public float typewriterDelay = 0.022f;
    [Tooltip("Hold time after the body has finished typing.")]
    public float holdAfterType = 5f;

    [Header("Visuals")]
    public Color backgroundColor = new Color(0.047f, 0.102f, 0.18f, 1f); // navy
    public Color textColor = new Color(0.8f, 0.84f, 0.88f, 1f);
    public Color accentColor = new Color(0.83f, 0.69f, 0.22f, 1f); // gold

    [Header("Audio")]
    [Tooltip("Tick sound while typewriting. If empty, resolved from GameOverManager/JournalUI hover SFX.")]
    public AudioClip typeSFX;

    private bool _played;
    private VisualElement _root;
    private VisualElement _illustrationEl;
    private Label _title;
    private Label _text;
    private VisualElement _divider;
    private GameObject _docGO;
    private Coroutine _routine;

    /// <summary>True while this slide's play routine is running.</summary>
    public bool IsPlaying => _routine != null;

    /// <summary>True once Play has ever been called (slides are single-use).</summary>
    public bool HasPlayed => _played;

    public void Play(Action onComplete = null)
    {
        if (_played) { onComplete?.Invoke(); return; }
        _played = true;
        _routine = StartCoroutine(PlayRoutine(onComplete));
    }

    /// <summary>Destroys the panel. Safe to call once the blackout is opaque.</summary>
    public void Dispose()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        if (_docGO != null)
        {
            Destroy(_docGO);
            _docGO = null;
        }
        _root = null;
        _title = null;
        _text = null;
        _divider = null;
    }

    private IEnumerator PlayRoutine(Action onComplete)
    {
        Build();
        if (_root == null) { onComplete?.Invoke(); yield break; }

        // Resolve typewriter SFX (asset/shader-safe per play, like CutscenePlayer).
        if (typeSFX == null)
        {
            var gom = FindAnyObjectByType<GameOverManager>();
            if (gom != null) typeSFX = gom.hoverSFX;
        }
        if (typeSFX == null)
        {
            var jui = FindAnyObjectByType<JournalUI>();
            if (jui != null) typeSFX = jui.hoverSFX;
        }

        // 1) Title rises in with a glow (unscaled coroutine — time is frozen).
        float elapsed = 0f;
        while (elapsed < titleFadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / titleFadeIn);
            t = t * t * (3f - 2f * t); // smoothstep
            if (_title != null)
            {
                _title.style.opacity = t;
                _title.style.translate = new Translate(0, Mathf.Lerp(26f, 0f, t));
            }
            yield return null;
        }
        if (_title != null)
        {
            _title.style.opacity = 1f;
            _title.style.translate = new Translate(0, 0);
        }

        // 2) Divider slides out from the center.
        elapsed = 0f;
        float targetWidth = 420f;
        while (elapsed < dividerExpand)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dividerExpand);
            t = t * t * (3f - 2f * t);
            if (_divider != null) _divider.style.width = Mathf.Lerp(0f, targetWidth, t);
            yield return null;
        }
        if (_divider != null) _divider.style.width = targetWidth;

        // 3) Typewrite the body text.
        string full = bodyText ?? "";
        string current = "";
        int sfxInterval = 2;
        for (int i = 0; i < full.Length; i++)
        {
            current += full[i];
            if (_text != null) _text.text = current;
            if (i % sfxInterval == 0 && typeSFX != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySound(typeSFX, 0f, 0f, false);
            yield return new WaitForSecondsRealtime(typewriterDelay);
        }

        // 4) Hold, then report done — the manager fades to black and disposes.
        yield return new WaitForSecondsRealtime(holdAfterType);
        _routine = null;
        onComplete?.Invoke();
    }

    private void Build()
    {
        _docGO = new GameObject("EpilogueSlide_Doc", typeof(UIDocument));
        _docGO.transform.SetParent(transform, false);
        var doc = _docGO.GetComponent<UIDocument>();
        doc.sortingOrder = 1500;

        var ssDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(doc);
        if (ssDoc != null) doc.panelSettings = ssDoc.panelSettings;
        if (doc.panelSettings == null)
            doc.panelSettings = UIPanelSettingsUtil.FindScreenSpacePanelSettingsAsset();

        var asset = Resources.Load<VisualTreeAsset>("EpilogueSlide");
        if (asset == null) return;
        asset.CloneTree(doc.rootVisualElement);

        _root = doc.rootVisualElement.Q("EpilogueRoot");
        if (_root == null) return;
        _root.pickingMode = PickingMode.Ignore;

        var bg = _root.Q("Background");
        if (bg != null)
        {
            bg.style.backgroundColor = backgroundColor;
            var grad = MakeVerticalGradientTexture(backgroundColor, new Color(0.016f, 0.031f, 0.063f, 1f));
            if (grad != null) bg.style.backgroundImage = Background.FromTexture2D(grad);
        }

        var vignette = _root.Q("Vignette");
        if (vignette != null)
        {
            var vig = MakeRadialVignetteTexture();
            if (vig != null) vignette.style.backgroundImage = Background.FromTexture2D(vig);
        }

        _title = _root.Q<Label>("Title");
        if (_title != null)
        {
            if (!string.IsNullOrEmpty(titleText)) _title.text = titleText;
            _title.style.color = accentColor;
            _title.style.opacity = 0f;
        }

        _divider = _root.Q("Divider");
        if (_divider != null)
        {
            _divider.style.backgroundColor = accentColor;
            _divider.style.width = 0f;
        }

        _illustrationEl = _root.Q("Illustration");
        if (_illustrationEl != null)
        {
            _illustrationEl.SetBackgroundImageSafe(illustration);
            if (illustration != null)
            {
                _illustrationEl.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
        }

        _text = _root.Q<Label>("BodyText");
        if (_text != null)
        {
            _text.text = "";
            _text.style.color = textColor;
        }
    }

    /// <summary>
    /// Vertical color ramp used as the slide background (USS gradients are
    /// rejected by the style engine in this Unity version, so textures are
    /// generated at runtime instead).
    /// </summary>
    private static Texture2D MakeVerticalGradientTexture(Color top, Color bottom)
    {
        const int h = 128;
        var tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            float t = y / (h - 1f);
            var c = Color.Lerp(top, bottom, t);
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Radial darkening toward the screen edges — cinema vignette.
    /// </summary>
    private static Texture2D MakeRadialVignetteTexture()
    {
        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f;
                float dy = (y + 0.5f) / s - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 center .. ~1.4 corner
                float a = Mathf.SmoothStep(0.55f, 1.15f, d);
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(a) * 0.72f));
            }
        }
        tex.Apply();
        return tex;
    }
}
