using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen black overlay shared by every step of the ending sequence.
/// Owned (created) by EndingSequenceManager; steps never fade themselves —
/// they build behind the blackout, then EndingSequenceManager fades the
/// blackout away and back. Sorting order 3000 sits above every other
/// UIDocument in the ending chain (bomb fade 2000, credits 1800, etc.), so
/// the gameplay never flashes between slides.
/// </summary>
public class EndingBlackout : MonoBehaviour
{
    [Tooltip("Fade duration used by FadeToBlack()/FadeFromBlack() when the caller passes 0.")]
    public float defaultFade = 0.6f;

    private UIDocument _doc;
    private VisualElement _root;
    private Coroutine _fadeRoutine;
    private GameObject _docGO;

    /// <summary>True while the overlay is fully opaque (screen is black).</summary>
    public bool IsBlack { get; private set; }

    /// <summary>True while a fade is in progress (no other fade should start).</summary>
    public bool IsFading => _fadeRoutine != null;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    private void Build()
    {
        _docGO = new GameObject("EndingBlackout_Doc", typeof(UIDocument));
        _docGO.transform.SetParent(transform, false);
        _doc = _docGO.GetComponent<UIDocument>();
        _doc.sortingOrder = 3000;

        var ssDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (ssDoc != null) _doc.panelSettings = ssDoc.panelSettings;
        if (_doc.panelSettings == null)
            _doc.panelSettings = UIPanelSettingsUtil.FindScreenSpacePanelSettingsAsset();

        _root = new VisualElement();
        _root.name = "EndingBlackout";
        _root.style.position = Position.Absolute;
        _root.style.left = 0;
        _root.style.right = 0;
        _root.style.top = 0;
        _root.style.bottom = 0;
        _root.style.backgroundColor = Color.black;
        _root.style.opacity = 0f;
        _root.pickingMode = PickingMode.Ignore;
        _doc.rootVisualElement.Add(_root);
        IsBlack = false;
    }

    /// <summary>Fade from transparent to fully black, then invokes onComplete.</summary>
    public void FadeToBlack(float duration, Action onComplete = null)
    {
        StartFade(1f, duration > 0f ? duration : defaultFade, onComplete);
    }

    /// <summary>Fade from fully black to transparent, then invokes onComplete.</summary>
    public void FadeFromBlack(float duration, Action onComplete = null)
    {
        StartFade(0f, duration > 0f ? duration : defaultFade, onComplete);
    }

    /// <summary>Instantly snap to black without any animation.</summary>
    public void SnapBlack()
    {
        if (_root == null) return;
        if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        _root.style.opacity = 1f;
        IsBlack = true;
    }

    private void StartFade(float target, float duration, Action onComplete)
    {
        if (_root == null) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(target, duration, onComplete));
    }

    private IEnumerator FadeRoutine(float target, float duration, Action onComplete)
    {
        float start = _root.style.opacity.value;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            t = t * t * (3f - 2f * t); // smoothstep
            _root.style.opacity = Mathf.Lerp(start, target, t);
            yield return null;
        }
        _root.style.opacity = target;
        IsBlack = target >= 0.99f;
        _fadeRoutine = null;
        onComplete?.Invoke();
    }
}
