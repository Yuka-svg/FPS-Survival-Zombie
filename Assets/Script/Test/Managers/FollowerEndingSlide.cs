using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class FollowerEndingSlide : MonoBehaviour
{
    [Serializable]
    public class EndingVariant
    {
        public string title = "KẾT THÚC";
        [TextArea(3, 5)]
        public string body = "";
    }

    [Header("Variant Texts (0/1/2 followers)")]
    public EndingVariant[] variants = new EndingVariant[3]
    {
        new EndingVariant
        {
            title = "KẾT THÚC — MỘT MÌNH",
            body = "Người chơi rời khỏi thành phố một mình, không mang theo ai. " +
                   "Có vẻ người chơi không thích đi theo nhóm."
        },
        new EndingVariant
        {
            title = "KẾT THÚC — MỘT NGƯỜI BẠN",
            body = "Người chơi rời khỏi thành phố cùng một người bạn đồng hành. " +
                   "Có vẻ người chơi là người kỹ tính."
        },
        new EndingVariant
        {
            title = "KẾT THÚC — ĐỒNG ĐỘI",
            body = "Người chơi rời khỏi thành phố cùng cả hai người bạn đồng hành. " +
                   "Có vẻ người chơi thích làm việc nhóm."
        }
    };

    [Header("Timing")]
    public float fadeIn = 1f;
    public float hold = 6f;
    public float fadeOut = 1f;

    [Header("Visuals")]
    public Color backgroundColor = new Color(0.031f, 0.071f, 0.125f, 1f);
    public Color titleColor = new Color(0.78f, 0.82f, 0.86f, 1f);

    private bool _played;
    private VisualElement _root;
    private GameObject _docGO;

    public void Play(Action onComplete = null)
    {
        if (_played) { onComplete?.Invoke(); return; }
        _played = true;
        StartCoroutine(PlayRoutine(onComplete));
    }

    private IEnumerator PlayRoutine(Action onComplete)
    {
        int followerCount = CountFollowingCompanions();
        int index = Mathf.Clamp(followerCount, 0, 2);
        Debug.Log($"[FollowerEndingSlide] Following companions: {followerCount} → showing variant {index}");

        Build(index);

        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (_root != null)
        {
            _root.style.opacity = 1f;
            yield return new WaitForSecondsRealtime(fadeIn + hold);
            _root.style.opacity = 0f;
            yield return new WaitForSecondsRealtime(fadeOut);
        }

        Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
        if (_docGO != null) Destroy(_docGO);

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
        _root.style.opacity = 0f;

        var bg = _root.Q("Background");
        if (bg != null) bg.style.backgroundColor = backgroundColor;

        var titleEl = _root.Q<Label>("Title");
        if (titleEl != null && variants != null && index >= 0 && index < variants.Length)
        {
            titleEl.text = variants[index].title;
            titleEl.style.color = titleColor;
        }

        var illustration = _root.Q("Illustration");
        if (illustration != null) illustration.style.display = DisplayStyle.None;

        var bodyEl = _root.Q<Label>("BodyText");
        if (bodyEl != null && variants != null && index >= 0 && index < variants.Length)
        {
            bodyEl.text = variants[index].body;
            bodyEl.style.color = titleColor;
        }
    }

    private int CountFollowingCompanions()
    {
        var all = FindObjectsByType<CompanionAI>(FindObjectsSortMode.None);
        return all.Count(c => c != null && (c.CurrentState == CompanionAI.State.Following
                                         || c.CurrentState == CompanionAI.State.Downed));
    }
}
