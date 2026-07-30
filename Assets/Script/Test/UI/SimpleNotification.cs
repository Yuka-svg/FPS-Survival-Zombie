using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class SimpleNotification : MonoBehaviour
{
    private static SimpleNotification _instance;

    [Header("Timing")]
    public float fadeIn = 0.3f;
    public float hold = 2.5f;
    public float fadeOut = 0.8f;

    [Header("Visuals")]
    public Color textColor = new Color(0.9f, 0.85f, 0.7f, 1f);
    public Color scrimColor = new Color(0f, 0f, 0f, 0.6f);
    public float fontSize = 28f;

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _scrim;
    private Label _text;
    private Coroutine _routine;

    public static SimpleNotification Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SimpleNotification");
                _instance = go.AddComponent<SimpleNotification>();
                Object.DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        var go = new GameObject("NotificationPanel", typeof(UIDocument));
        go.transform.SetParent(transform, false);
        _doc = go.GetComponent<UIDocument>();
        _doc.sortingOrder = 500;

        var hudDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (hudDoc != null) _doc.panelSettings = hudDoc.panelSettings;

        _root = new VisualElement();
        _root.name = "NotificationRoot";
        _root.AddToClassList("notification-root");

        _scrim = new VisualElement();
        _scrim.name = "NotificationScrim";
        _scrim.AddToClassList("notification-scrim");
        _scrim.style.backgroundColor = scrimColor;
        _root.Add(_scrim);

        _text = new Label();
        _text.name = "NotificationLabel";
        _text.AddToClassList("notification-label");
        _text.style.color = textColor;
        _text.style.fontSize = fontSize;
        _root.Add(_text);

        var sheet = Resources.Load<StyleSheet>("SimpleNotification");
        if (sheet != null)
            _root.styleSheets.Add(sheet);

        _doc.rootVisualElement.Add(_root);
    }

    public static void Show(string message)
    {
        var inst = Instance;
        if (inst == null) return;
        inst.ShowInternal(message);
    }

    private void ShowInternal(string message)
    {
        if (_text != null) _text.text = message;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        _root.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(fadeIn + hold);
        _root.style.opacity = 0f;
        yield return new WaitForSecondsRealtime(fadeOut);
        _routine = null;
    }
}
