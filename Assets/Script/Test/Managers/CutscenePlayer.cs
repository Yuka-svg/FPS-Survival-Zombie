using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

public class CutscenePlayer : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("Large title text (e.g. 'CHƯƠNG 1' or 'XÁC NGƯỜI LÍNH').")]
    public string title = "";
    [Tooltip("Body / subtitle text.")]
    [TextArea(3, 8)]
    public string body = "";

    [Header("Timing")]
    public float fadeIn = 0.6f;
    public float hold = 3f;
    public float fadeOut = 0.8f;

    [Header("Visuals")]
    public Color scrim = new Color(0f, 0f, 0f, 0.85f);
    public Color titleColor = new Color(0.85f, 0.78f, 0.45f, 1f);
    public Color bodyColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    [Header("Audio")]
    [Tooltip("Sound clip played during text typing. If left empty, will dynamically try to fetch UI Hover SFX.")]
    public AudioClip typeSFX;

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _scrim;
    private Label _title;
    private Label _body;
    private Coroutine _routine;

    internal static int _playingCount;

    public bool IsPlaying => _routine != null;

    /// <summary>
    /// True if ANY CutscenePlayer or BombExplosionCutscene in the scene is
    /// currently playing. Uses a static counter updated by OnEnable/OnDisable
    /// instead of FindObjectsByType every access.
    /// </summary>
    public static bool IsAnyPlaying => _playingCount > 0;

    /// <summary>
    /// Returns true if the given PanelSettings is configured for world-space
    /// rendering (as opposed to screen-space overlay). Detected by name since
    /// PanelSettings doesn't expose a public "worldSpace" flag. The project's
    /// world-space panel is named "WorldSpacePanelSettings" (see
    /// CompanionRescueUI / CompanionHealthBar).
    /// </summary>
    private static bool IsWorldSpacePanelSettings(PanelSettings ps)
    {
        if (ps == null) return false;
        // World-space panels in this project are named with "WorldSpace".
        // This covers WorldSpacePanelSettings and any future variants.
        return ps.name.IndexOf("WorldSpace", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Build()
    {
        if (_root != null) return;

        var go = new GameObject(name + "_Panel", typeof(UIDocument));
        go.transform.SetParent(transform, false);
        _doc = go.GetComponent<UIDocument>();

        // Borrow panel settings from an existing screen-space UIDocument.
        // IMPORTANT: must skip WorldSpacePanelSettings — otherwise the cutscene
        // would render in world space at this GameObject's world position
        // (e.g. at the quest trigger's position "outside the map") instead of
        // on the camera screen. See UIPanelSettingsUtil for details.
        var hudDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (hudDoc != null)
        {
            _doc.panelSettings = hudDoc.panelSettings;
            _doc.sortingOrder = 100;
        }

        _root = new VisualElement();
        _root.name = "CutscenePanel";
        _root.AddToClassList("cutscene-panel");
        _root.pickingMode = PickingMode.Ignore;

        _scrim = new VisualElement();
        _scrim.name = "CutsceneScrim";
        _scrim.AddToClassList("cutscene-scrim");
        _root.Add(_scrim);

        _title = new Label();
        _title.name = "CutsceneTitle";
        _title.AddToClassList("cutscene-title");
        _root.Add(_title);

        _body = new Label();
        _body.name = "CutsceneBody";
        _body.AddToClassList("cutscene-body");
        _root.Add(_body);

        var sheet = Resources.Load<StyleSheet>("CutscenePanel");
        if (sheet != null)
            _root.styleSheets.Add(sheet);

        _doc.rootVisualElement.Add(_root);
    }

    private void OnDestroy()
    {
        // If the cutscene was playing when this object gets destroyed
        // (e.g. scene unload), restore timeScale so the game doesn't freeze.
        if (_routine != null && Time.timeScale == 0f)
        {
            bool pauseOpen = false;
            bool gameOver = false;
            try
            {
                pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
                gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
            }
            catch (System.NullReferenceException) { }
            if (!pauseOpen && !gameOver)
                Time.timeScale = 1f;
        }
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
            if (_playingCount > 0) _playingCount--;
        }
    }

    public void Play(System.Action onComplete = null)
    {
        Build();
        if (_routine != null) StopCoroutine(_routine);
        _playingCount++;
        _routine = StartCoroutine(PlayRoutine(onComplete));
    }

    private IEnumerator PlayRoutine(System.Action onComplete)
    {
        _title.text = "";
        _body.text = "";

        // Re-resolve typeSFX each play so asset/shader changes apply.
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

        _scrim.style.backgroundColor = scrim;
        _title.style.color = titleColor;
        _body.style.color = bodyColor;

        // 2. Setup display and lock mouse interaction to scrim
        _root.style.display = DisplayStyle.Flex;
        _root.style.opacity = 0f; // Start fully transparent
        _scrim.pickingMode = PickingMode.Position;

        Rigidbody playerRb = null;
        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            playerRb = playerGo.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }
        }

        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        yield return null; // Wait one frame for Yoga layout engine to register opacity: 0

        // 3. Fade in scrim class overlay (starts 1.5s USS transition to opacity 1)
        _root.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(1.5f); // Wait for scrim fade-in to complete

        // 4. Typewrite the title text (first)
        string fullTitle = string.IsNullOrEmpty(title) ? "" : title.ToUpper();
        string currentTitle = "";
        float charDelay = 0.015f; // fast, satisfying typewriter speed
        int sfxInterval = 2;

        for (int i = 0; i < fullTitle.Length; i++)
        {
            currentTitle += fullTitle[i];
            _title.text = currentTitle;

            if (i % sfxInterval == 0 && typeSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(typeSFX, 0f, 0f, false);
            }
            yield return new WaitForSecondsRealtime(charDelay);
        }

        // Slight pause between title and body typing
        yield return new WaitForSecondsRealtime(0.2f);

        // 5. Typewrite the body/subtitle text (second)
        string currentBody = "";
        for (int i = 0; i < body.Length; i++)
        {
            currentBody += body[i];
            _body.text = currentBody;

            if (i % sfxInterval == 0 && typeSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(typeSFX, 0f, 0f, false);
            }
            yield return new WaitForSecondsRealtime(charDelay);
        }

        // 6. Hold for 1.5s after typing is completely done
        yield return new WaitForSecondsRealtime(1.5f);

        // 7. Fade out entire panel (starts 1.5s USS transition to opacity 0)
        _root.style.opacity = 0f;
        yield return new WaitForSecondsRealtime(1.5f); // Wait for fade-out to complete

        _scrim.pickingMode = PickingMode.Ignore;
        _root.style.display = DisplayStyle.None;

        bool pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
        if (!pauseOpen && !gameOver)
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        _routine = null;
        if (_playingCount > 0) _playingCount--;
        onComplete?.Invoke();
    }
}
