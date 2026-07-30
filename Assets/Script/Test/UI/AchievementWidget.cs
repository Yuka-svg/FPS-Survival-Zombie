using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class AchievementWidget : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.J;

    [Header("Popup Timing")]
    public float popupFadeIn = 0.3f;
    public float popupHold = 4f;
    public float popupFadeOut = 0.6f;

    [Header("List Panel Layout")]
    public float rowHeight = 70f;
    public float rowSpacing = 8f;

    private UIDocument _doc;
    private VisualElement _popupRoot;
    private VisualElement _listContent;
    private Label _popupTitle;
    private Label _popupDesc;
    private VisualElement _popupIcon;
    private VisualElement _scrim;
    private VisualElement _listPanel;
    private Label _listHint;
    private bool _listOpen;
    private readonly Queue<AchievementData> _popupQueue = new Queue<AchievementData>();
    private bool _popupPlaying;

    private static readonly Color Accent = new Color(0.85f, 0.78f, 0.45f, 1f);
    private static readonly Color CardTop = new Color(0.122f, 0.149f, 0.18f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.66f, 0.72f, 1f);

    private void Awake()
    {
        var go = new GameObject("Achievement_Doc", typeof(UIDocument));
        go.transform.SetParent(transform, false);
        _doc = go.GetComponent<UIDocument>();
        _doc.sortingOrder = 100;

        var asset = Resources.Load<VisualTreeAsset>("AchievementWidget");
        if (asset == null) { enabled = false; return; }
        asset.CloneTree(_doc.rootVisualElement);

        _popupRoot = _doc.rootVisualElement.Q("AchievementPopup");
        _popupTitle = _popupRoot?.Q<Label>("Title");
        _popupDesc = _popupRoot?.Q<Label>("Desc");
        _popupIcon = _popupRoot?.Q("Icon");

        _scrim = _doc.rootVisualElement.Q("AchievementScrim");
        _listPanel = _doc.rootVisualElement.Q("AchievementListPanel");
        _listContent = _listPanel?.Q("ScrollArea");
        _listHint = _listPanel?.Q<Label>("ListHint");

        if (_scrim != null) _scrim.RegisterCallback<ClickEvent>(_ => ToggleList());
        if (_listHint != null) _listHint.text = $"Press {toggleKey} to close";
    }

    private void OnEnable()
    {
        var mgr = AchievementManager.Instance;
        if (mgr != null)
            mgr.OnAchievementUnlocked += HandleUnlocked;
    }

    private void OnDisable()
    {
        var mgr = AchievementManager.Instance;
        if (mgr != null)
            mgr.OnAchievementUnlocked -= HandleUnlocked;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleList();
    }

    private void ToggleList()
    {
        _listOpen = !_listOpen;
        if (_listOpen)
        {
            _listPanel.style.display = DisplayStyle.Flex;
            _scrim.style.display = DisplayStyle.Flex;
            RebuildRows();
            _listPanel.style.opacity = 1f;
            _scrim.style.opacity = 0.8f;
        }
        else
        {
            _listPanel.style.opacity = 0f;
            _scrim.style.opacity = 0f;
            _listPanel.schedule.Execute(() => {
                _listPanel.style.display = DisplayStyle.None;
                _scrim.style.display = DisplayStyle.None;
            }).StartingIn(200);
        }
    }

    private void RebuildRows()
    {
        _listContent.Clear();
        var mgr = AchievementManager.Instance;
        if (mgr == null || mgr.achievements == null) return;

        foreach (var ach in mgr.achievements)
        {
            if (ach == null) continue;
            bool unlocked = mgr.IsUnlocked(ach);
            int progress = mgr.GetProgress(ach);
            int target = ach.targetValue;

            var row = new VisualElement();
            row.name = $"Row_{ach.id}";
            row.AddToClassList("ach-row");
            row.style.minHeight = rowHeight;
            row.style.marginBottom = rowSpacing;
            row.style.backgroundColor = unlocked ? new Color(Accent.r, Accent.g, Accent.b, 0.12f) : CardTop;
            _listContent.Add(row);

            var iconEl = new VisualElement();
            iconEl.name = "Icon";
            iconEl.AddToClassList("ach-icon");
            iconEl.style.backgroundColor = unlocked ? Accent : new Color(Muted.r, Muted.g, Muted.b, 0.3f);
            if (ach.icon != null)
                iconEl.style.backgroundImage = new StyleBackground(ach.icon);
            row.Add(iconEl);

            var textCol = new VisualElement();
            textCol.AddToClassList("ach-text-col");
            row.Add(textCol);

            var title = new Label(ach.title);
            title.AddToClassList("ach-title");
            title.style.color = unlocked ? Color.white : Muted;
            textCol.Add(title);

            string descText = unlocked
                ? ach.description
                : ach.isProgression
                    ? $"{ach.description}  ({progress}/{target})"
                    : ach.description;
            var desc = new Label(descText);
            desc.AddToClassList("ach-desc");
            textCol.Add(desc);

            var badge = new Label(unlocked ? "✓" : "[ ]");
            badge.AddToClassList("ach-badge");
            badge.style.color = unlocked ? Accent : Muted;
            row.Add(badge);
        }
    }

    private void HandleUnlocked(AchievementData ach)
    {
        _popupQueue.Enqueue(ach);
        if (!_popupPlaying)
            StartCoroutine(PlayPopupQueue());
    }

    private IEnumerator PlayPopupQueue()
    {
        _popupPlaying = true;
        while (_popupQueue.Count > 0)
        {
            var ach = _popupQueue.Dequeue();
            if (_popupTitle != null) _popupTitle.text = ach.title;
            if (_popupDesc != null) _popupDesc.text = ach.description;
            if (ach.icon != null && _popupIcon != null)
                _popupIcon.style.backgroundImage = new StyleBackground(ach.icon);

            _popupRoot.style.opacity = 1f;
            yield return new WaitForSecondsRealtime(popupFadeIn + popupHold);
            _popupRoot.style.opacity = 0f;
            yield return new WaitForSecondsRealtime(popupFadeOut);
        }
        _popupPlaying = false;
    }

    private void OnDestroy()
    {
        if (_doc != null && _doc.gameObject != null)
            Destroy(_doc.gameObject);
    }
}
