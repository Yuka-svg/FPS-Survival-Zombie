using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// World-space UI shown above the companion while it is Downed.
///
/// Displays:
///   - A "Giữ E để cứu" prompt when the companion is downed.
///   - A progress bar (0..100%) that fills as the player holds E to rescue.
///   - Hides automatically when the companion is revived (rescued or auto-revive).
///
/// Reuses the same WorldSpacePanelSettings as CompanionHealthBar and builds
/// its visual tree programmatically (no external UXML required). The visual
/// tree is attached in LateUpdate (not Awake) because UIDocument.rootVisualElement
/// is null until the panel settings have been applied — this mirrors the
/// CompanionHealthBar pattern.
/// </summary>
[RequireComponent(typeof(CompanionAI))]
public class CompanionRescueUI : MonoBehaviour
{
    public float heightOffset = 2.6f;       // Slightly above the health bar.
    public Vector2 panelSize = new Vector2(180f, 40f);
    public float worldScale = 0.0075f;
    public Color promptColor = new Color(0.95f, 0.92f, 0.8f, 1f);
    public Color barBgColor = new Color(0f, 0f, 0f, 0.6f);
    public Color barFillColor = new Color(0.3f, 0.85f, 1f, 1f); // Cyan
    public float promptFontSize = 14f;

    private CompanionAI _companion;
    private GameObject _panelGO;
    private UIDocument _doc;
    private VisualElement _root;
    private Label _promptLabel;
    private VisualElement _barBg;
    private VisualElement _barFill;
    private Transform _cam;
    private bool _uiAttached;    // True once the visual tree has been attached to rootVisualElement.
    private bool _visible;       // Desired visibility (opacity-based, not SetActive).
    private float _currentProgress; // 0..1

    private void Awake()
    {
        _companion = GetComponent<CompanionAI>();
        BuildPanel();
    }

    /// <summary>
    /// Creates the panel GameObject + UIDocument and loads panel settings.
    /// Does NOT touch rootVisualElement — that is deferred to LateUpdate
    /// because rootVisualElement is null until the panel settings are applied.
    /// </summary>
    private void BuildPanel()
    {
        _panelGO = new GameObject("CompanionRescuePanel");
        _panelGO.transform.SetParent(transform, false);
        _panelGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        _panelGO.transform.localRotation = Quaternion.identity;
        _panelGO.transform.localScale = Vector3.one;
        // Keep the panel GO active so the UIDocument initializes its panel
        // (rootVisualElement is null while the GO is inactive). Visibility is
        // controlled via opacity on the root element instead of SetActive.
        _panelGO.SetActive(true);

        _doc = _panelGO.AddComponent<UIDocument>();
        _doc.sortingOrder = 151; // Just above CompanionHealthBar (150).
        _doc.worldSpaceSize = new Vector2(panelSize.x * worldScale, panelSize.y * worldScale);

        var settings = Resources.Load<PanelSettings>("WorldSpacePanelSettings");
        if (settings != null)
            _doc.panelSettings = settings;
        else
            Debug.LogWarning("[CompanionRescueUI] WorldSpacePanelSettings not found in Resources!");

        // Remove any accidental collider created by UIDocument.
        var col = _panelGO.GetComponent<Collider>();
        if (col != null) Destroy(col);
    }

    /// <summary>
    /// Builds the visual tree and attaches it to the UIDocument's rootVisualElement.
    /// Called from LateUpdate once rootVisualElement becomes available.
    /// </summary>
    private void AttachVisualTree()
    {
        if (_doc == null || _doc.rootVisualElement == null) return;

        _root = new VisualElement();
        _root.name = "RescueRoot";
        _root.style.width = panelSize.x;
        _root.style.height = panelSize.y;
        _root.style.backgroundColor = new Color(0f, 0f, 0f, 0f); // Transparent container.
        _root.style.flexDirection = FlexDirection.Column;
        _root.style.alignItems = Align.Center;
        _root.style.justifyContent = Justify.FlexStart;

        _promptLabel = new Label();
        _promptLabel.name = "RescuePrompt";
        _promptLabel.text = "Giữ [E] để cứu";
        _promptLabel.style.color = promptColor;
        _promptLabel.style.fontSize = promptFontSize;
        _promptLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _promptLabel.style.marginBottom = 4f;
        _promptLabel.style.whiteSpace = WhiteSpace.Normal;
        _promptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _root.Add(_promptLabel);

        _barBg = new VisualElement();
        _barBg.name = "RescueBarBg";
        _barBg.style.width = panelSize.x;
        _barBg.style.height = 8f;
        _barBg.style.backgroundColor = barBgColor;
        _barBg.style.borderTopLeftRadius = 4f;
        _barBg.style.borderTopRightRadius = 4f;
        _barBg.style.borderBottomLeftRadius = 4f;
        _barBg.style.borderBottomRightRadius = 4f;
        _barBg.style.overflow = Overflow.Hidden;
        _root.Add(_barBg);

        _barFill = new VisualElement();
        _barFill.name = "RescueBarFill";
        _barFill.style.width = 0f; // Fills with progress.
        _barFill.style.height = 8f;
        _barFill.style.backgroundColor = barFillColor;
        _barFill.style.borderTopLeftRadius = 4f;
        _barFill.style.borderTopRightRadius = 4f;
        _barFill.style.borderBottomLeftRadius = 4f;
        _barFill.style.borderBottomRightRadius = 4f;
        _barBg.Add(_barFill);

        _doc.rootVisualElement.Add(_root);
        _uiAttached = true;
        // Apply current visibility + progress state to the freshly built tree.
        _root.style.opacity = _visible ? 1f : 0f;
        UpdateBar();
    }

    private void OnEnable()
    {
        if (_companion != null)
        {
            _companion.OnRescueProgressChanged += HandleProgress;
            _companion.OnStateChanged += HandleStateChanged;
        }
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        _currentProgress = 0f;
        _visible = false;
        SetVisible(false);
    }

    private void OnDisable()
    {
        if (_companion != null)
        {
            _companion.OnRescueProgressChanged -= HandleProgress;
            _companion.OnStateChanged -= HandleStateChanged;
        }
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (_panelGO != null)
        {
            if (_doc != null && _doc.rootVisualElement != null)
                _doc.rootVisualElement.Clear();
            Destroy(_panelGO);
        }
    }

    private void HandleStateChanged(CompanionAI.State newState)
    {
        // Show the panel only while Downed; hide otherwise.
        SetVisible(newState == CompanionAI.State.Downed);
        if (newState != CompanionAI.State.Downed)
        {
            _currentProgress = 0f;
            UpdateBar();
        }
    }

    private void HandleProgress(float normalized)
    {
        _currentProgress = Mathf.Clamp01(normalized);
        UpdateBar();
    }

    private void UpdateBar()
    {
        if (_barFill != null)
            _barFill.style.width = panelSize.x * _currentProgress;
        // Dim the prompt once rescue is underway (keep just the bar prominent).
        if (_promptLabel != null)
            _promptLabel.style.opacity = _currentProgress > 0f ? 0.4f : 1f;
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        // Apply opacity immediately if the root is ready; otherwise LateUpdate
        // will apply it once the visual tree is attached.
        if (_root != null)
            _root.style.opacity = visible ? 1f : 0f;
    }

    private void LateUpdate()
    {
        // Deferred visual tree attachment — rootVisualElement is null until
        // the panel settings have been applied (may take a frame after Awake).
        if (!_uiAttached)
            AttachVisualTree();

        // No billboarding: world-space UIDocument renders on the +Z face, so
        // keeping rotation = identity (set in BuildPanel) makes the text face
        // the camera the same way CompanionHealthBar does. LookRotation here
        // would flip the text backwards.
    }
}
