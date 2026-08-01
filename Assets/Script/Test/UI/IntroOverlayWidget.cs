using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shows the story intro panel right after the opening black-overlay fade.
/// Only in Story mode (never Endless). Pauses the game via PanelManager while
/// the intro is visible; the player presses "BẮT ĐẦU" to resume gameplay.
/// </summary>
public class IntroOverlayWidget : MonoBehaviour
{
    public static IntroOverlayWidget Instance;

    [Header("Content")]
    [Tooltip("Title of the intro panel.")]
    public string introTitle = "NHIỆM VỤ KHỞI ĐẦU";

    [Tooltip("Story intro body text.")]
    [TextArea(8, 16)]
    public string introBody =
        "Một đợt bùng phát virus bí ẩn đã biến cả thị trấn thành vùng cấm đầy thây ma.\n\n" +
        "Bạn là người sống sót cuối cùng còn tỉnh táo trong bệnh viện. Hãy trang bị vũ khí, " +
        "tìm kiếm đồ tiếp tế và chiến đấu thoát khỏi thị trấn.\n\n" +
        "Trên đường đi, gặp gỡ các nhân vật, hoàn thành nhiệm vụ và khám phá sự thật " +
        "đằng sau đại dịch.";

    [Tooltip("Hint text under the start button.")]
    public string introHint = "NHẤN [H] BẤT CỨ LÚC NÀO ĐỂ XEM HƯỚNG DẪN CHƠI";

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _card;
    private bool _initialized;
    private bool _shown;
    private static bool _introShownThisSession;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void OnEnable()
    {
        if (!_initialized) Initialize();
    }

    private void OnDisable()
    {
        _initialized = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Initialize()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;
        _root = _doc.rootVisualElement.Q("IntroPanel");
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;

        _card = _root.Q("IntroCard");

        var title = _root.Q<Label>("IntroTitle");
        if (title != null) title.text = introTitle;
        var body = _root.Q<Label>("IntroBody");
        if (body != null) body.text = introBody;
        var hint = _root.Q<Label>("IntroHint");
        if (hint != null) hint.text = introHint;

        var startBtn = _root.Q<Button>("IntroStartButton");
        if (startBtn != null) startBtn.clicked += Close;

        _initialized = true;
    }

    private void Start()
    {
        if (!_initialized) Initialize();
        if (_introShownThisSession) return;
        if (GameModeManager.CurrentMode != GameMode.Story) return;
        _introShownThisSession = true;
        StartCoroutine(ShowAfterBlackFade());
    }

    private IEnumerator ShowAfterBlackFade()
    {
        // Open the intro panel JUST BEFORE the black overlay fade-out finishes
        // (BlackOverlayDuration = 3.0s). This is critical: GameplayHUDController's
        // StartFadeOut checks IsAnyPanelActive() when its 3.0s wait ends and only
        // then resumes timeScale to 1. If we open a fraction later (e.g. the old
        // 3.4s), the character already starts moving for 0.4s before the intro
        // panel appears. Opening at 2.85s keeps time frozen the whole time, so
        // gameplay truly starts only when the player presses BẮT ĐẦU.
        yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration - 0.15f);
        yield return null;
        Open();
    }

    private void Open()
    {
        if (_shown || !_initialized) return;
        if (PanelManager.Instance == null) return;
        if (!PanelManager.Instance.CanOpenPanel("Intro")) return;

        _shown = true;
        PanelManager.Instance.OpenPanel("Intro", _root, _card, Close);
    }

    public void Close()
    {
        if (!_shown) return;
        _shown = false;
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("Intro", _root, _card, null);
        }
        else
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }
    }
}
