using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

/// <summary>
/// Fast travel (B6): while standing inside any SaveRoom, the player presses
/// [T] to open a menu of every unlocked save room (chapters &lt;= current).
/// Choosing a destination teleports the player there (PlayerMovement
/// TeleportPlayer) and sets it as the respawn checkpoint.
///
/// Runtime-built UI like ChapterSummaryWidget/SimpleNotification: lazy
/// singleton, borrows screen-space PanelSettings, loads FastTravel.uss.
/// Story mode only.
/// </summary>
public class FastTravelWidget : MonoBehaviour
{
    private static FastTravelWidget _instance;

    [Header("Controls")]
    [Tooltip("Key that opens the fast travel menu while inside a save room.")]
    public KeyCode toggleKey = KeyCode.T;

    [Header("Content")]
    public string title = "DI CHUYỂN NHANH";
    public string subtitle = "Chọn điểm đến đã mở khóa:";
    public string lockedLabel = "KHÓA";
    public string hint = "[ESC] ĐÓNG";

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _card;
    private VisualElement _list;
    private bool _built;
    private bool _shown;
    private float _nextKeyCheck = 0f;

    public static FastTravelWidget Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("FastTravelWidget");
                _instance = go.AddComponent<FastTravelWidget>();
                Object.DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        Build();
    }

    private void Build()
    {
        if (_built) return;

        var go = new GameObject("FastTravelPanel", typeof(UIDocument));
        go.transform.SetParent(transform, false);
        _doc = go.GetComponent<UIDocument>();
        _doc.sortingOrder = 310;

        var hudDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (hudDoc != null) _doc.panelSettings = hudDoc.panelSettings;

        _root = new VisualElement();
        _root.name = "FastTravelPanel";
        _root.AddToClassList("fast-travel-panel");
        _root.style.display = DisplayStyle.None;

        _card = new VisualElement();
        _card.name = "FastTravelCard";
        _card.AddToClassList("fast-travel-card");

        var t = new Label();
        t.name = "FastTravelTitle";
        t.AddToClassList("fast-travel-title");
        t.text = title;
        _card.Add(t);

        var sub = new Label();
        sub.name = "FastTravelSubtitle";
        sub.AddToClassList("fast-travel-subtitle");
        sub.text = subtitle;
        _card.Add(sub);

        _list = new VisualElement();
        _list.name = "FastTravelList";
        _list.AddToClassList("fast-travel-list");
        _card.Add(_list);

        var h = new Label();
        h.name = "FastTravelHint";
        h.AddToClassList("fast-travel-hint");
        h.text = hint;
        _card.Add(h);

        _root.Add(_card);

        var sheet = Resources.Load<StyleSheet>("FastTravel");
        if (sheet != null)
            _root.styleSheets.Add(sheet);

        _doc.rootVisualElement.Add(_root);
        _built = true;
    }

    private void Update()
    {
        if (GameModeManager.CurrentMode != GameMode.Story) return;
        if (_shown) return;
        if (Time.unscaledTime < _nextKeyCheck) return;
        _nextKeyCheck = Time.unscaledTime + 0.1f;

        if (SaveRoom.CurrentRoom == null) return;
        if (!CanInteract()) return;

        bool pressed = false;
        try { if (Input.GetKeyDown(toggleKey)) pressed = true; } catch { }
        if (!pressed)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && toggleKey == KeyCode.T && kb.tKey.wasPressedThisFrame) pressed = true;
        }
        if (pressed) Open();
    }

    private bool CanInteract()
    {
        if (CutscenePlayer.IsAnyPlaying) return false;
        if (cowsins.PauseMenu.isPaused) return false;
        if (PanelManager.Instance != null && PanelManager.Instance.IsAnyPanelActive()) return false;
        return true;
    }

    private void Open()
    {
        if (!_built) Build();
        if (_shown || _root == null) return;
        if (PanelManager.Instance == null) return;
        if (!PanelManager.Instance.CanOpenPanel("FastTravel")) return;

        PopulateList();

        _shown = true;
        PanelManager.Instance.OpenPanel("FastTravel", _root, _card, Close);
    }

    private void PopulateList()
    {
        _list.Clear();

        var sm = StoryManager.Instance;
        int currentChapter = sm != null ? sm.CurrentChapter : 1;

        var saveRooms = FindObjectsByType<SaveRoom>(FindObjectsSortMode.None);
        System.Array.Sort(saveRooms, (a, b) => a.chapter.CompareTo(b.chapter));

        foreach (var sr in saveRooms)
        {
            if (sr.chapter <= 0) continue;
            bool unlocked = sr.chapter <= currentChapter;

            var row = new Button();
            row.name = $"FastTravelDest_{sr.chapter}";
            row.AddToClassList("fast-travel-dest");

            var label = new Label();
            label.AddToClassList("fast-travel-dest-name");
            label.text = unlocked ? $"CHƯƠNG {sr.chapter}" : $"{lockedLabel} — CHƯƠNG {sr.chapter}";
            row.Add(label);

            var sub = new Label();
            sub.AddToClassList("fast-travel-dest-sub");
            sub.text = unlocked
                ? $"Điểm dừng chân chương {sr.chapter}"
                : "Hoàn thành các chương trước để mở khóa";
            row.Add(sub);

            if (!unlocked)
            {
                row.SetEnabled(false);
            }
            else
            {
                SaveRoom target = sr;
                row.clicked += () => TravelTo(target);
            }

            _list.Add(row);
        }
    }

    private void TravelTo(SaveRoom target)
    {
        Close();

        Vector3 dest = target.EffectiveRespawnPosition;
        SaveRoom.LastCheckpoint = dest;
        SaveRoom.LastCheckpointRotation = target.transform.rotation;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            pm.TeleportPlayer(dest, target.transform.rotation, true, true);
        }
        else
        {
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            player.transform.position = dest;
        }

        SimpleNotification.Show($"Đã di chuyển tới CHƯƠNG {target.chapter}");
    }

    public void Close()
    {
        if (!_shown) return;
        _shown = false;
        if (PanelManager.Instance != null)
            PanelManager.Instance.ClosePanel("FastTravel", _root, _card, null);
        else if (_root != null)
            _root.style.display = DisplayStyle.None;
    }
}
