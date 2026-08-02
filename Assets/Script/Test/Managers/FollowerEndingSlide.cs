using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

/// <summary>
/// Ending slide variant selected by the number of companions following the
/// player. Shares the EpilogueSlide UXML/USS look but tints the title and
/// divider with a per-variant accent color (gray / blue / gold). Like
/// EpilogueSlide it does not fade itself or touch Time.timeScale — the
/// EndingSequenceManager owns the blackout and the frozen time.
/// </summary>
public class FollowerEndingSlide : MonoBehaviour
{
    [Serializable]
    public class EndingVariant
    {
        public string title = "KẾT THÚC";
        [TextArea(3, 5)]
        public string body = "";
        [Tooltip("Accent color for title + divider (0 gray, 1 blue, 2 gold).")]
        public Color accentColor = Color.white;
    }

    [Header("Variant Texts (0/1/2 followers)")]
    public EndingVariant[] variants = new EndingVariant[3]
    {
        new EndingVariant
        {
            title = "KẾT THÚC — MỘT MÌNH",
            body = "Người chơi rời khỏi thành phố một mình, không mang theo ai. " +
                   "Có vẻ người chơi không thích đi theo nhóm.",
            accentColor = new Color(0.60f, 0.63f, 0.67f, 1f) // gray
        },
        new EndingVariant
        {
            title = "KẾT THÚC — MỘT NGƯỜI BẠN",
            body = "Người chơi rời khỏi thành phố cùng một người bạn đồng hành. " +
                   "Có vẻ người chơi là người kỹ tính.",
            accentColor = new Color(0.30f, 0.64f, 1.00f, 1f) // blue
        },
        new EndingVariant
        {
            title = "KẾT THÚC — ĐỒNG ĐỘI",
            body = "Người chơi rời khỏi thành phố cùng cả hai người bạn đồng hành. " +
                   "Có vẻ người chơi thích làm việc nhóm.",
            accentColor = new Color(1.00f, 0.82f, 0.40f, 1f) // gold
        }
    };

    [Header("Timing")]
    public float titleFadeIn = 0.9f;
    public float dividerExpand = 0.6f;
    [Tooltip("Seconds per character while typewriting the body text.")]
    public float typewriterDelay = 0.022f;
    [Tooltip("Hold time after the body has finished typing.")]
    public float holdAfterType = 5f;

    [Header("Visuals")]
    public Color backgroundColor = new Color(0.047f, 0.102f, 0.18f, 1f);
    public Color textColor = new Color(0.8f, 0.84f, 0.88f, 1f);

    [Header("Audio")]
    [Tooltip("Tick sound while typewriting. If empty, resolved from GameOverManager/JournalUI hover SFX.")]
    public AudioClip typeSFX;

    private bool _played;
    private VisualElement _root;
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
        int followerCount = CountFollowingCompanions();
        int index = Mathf.Clamp(followerCount, 0, variants.Length - 1);

        Build(index);
        if (_root == null) { onComplete?.Invoke(); yield break; }

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

        float elapsed = 0f;
        while (elapsed < titleFadeIn)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / titleFadeIn);
            t = t * t * (3f - 2f * t);
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

        string full = variants[index] != null ? (variants[index].body ?? "") : "";
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

        yield return new WaitForSecondsRealtime(holdAfterType);
        _routine = null;
        onComplete?.Invoke();
    }

    private void Build(int index)
    {
        _docGO = new GameObject("FollowerEndingSlide_Doc", typeof(UIDocument));
        _docGO.transform.SetParent(transform, false);
        var doc = _docGO.GetComponent<UIDocument>();
        doc.sortingOrder = 1650;

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
        if (bg != null) bg.style.backgroundColor = backgroundColor;

        Color accent = (variants != null && index >= 0 && index < variants.Length && variants[index] != null)
            ? variants[index].accentColor
            : Color.white;

        _title = _root.Q<Label>("Title");
        if (_title != null)
        {
            if (variants != null && index >= 0 && index < variants.Length && variants[index] != null)
                _title.text = variants[index].title;
            _title.style.color = accent;
            _title.style.opacity = 0f;
        }

        _divider = _root.Q("Divider");
        if (_divider != null)
        {
            _divider.style.backgroundColor = accent;
            _divider.style.width = 0f;
        }

        var illustration = _root.Q("Illustration");
        if (illustration != null) illustration.style.display = DisplayStyle.None;

        _text = _root.Q<Label>("BodyText");
        if (_text != null)
        {
            _text.text = "";
            _text.style.color = textColor;
        }
    }

    private int CountFollowingCompanions()
    {
        var all = FindObjectsByType<CompanionAI>(FindObjectsSortMode.None);
        return all.Count(c => c != null && (c.CurrentState == CompanionAI.State.Following
                                         || c.CurrentState == CompanionAI.State.Downed));
    }
}
