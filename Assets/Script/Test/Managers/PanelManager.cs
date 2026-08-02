using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance;

    public const float PanelTransitionDuration = 1.5f;
    public const float BlackOverlayDuration = 3.0f;

    private readonly Dictionary<string, System.Action> _activePanels = new Dictionary<string, System.Action>();
    private readonly HashSet<string> _transitioningPanels = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private Coroutine _lockCoroutine;
    private static readonly Dictionary<string, bool> _hudActiveState = new Dictionary<string, bool>();
    private readonly Dictionary<string, bool> _desiredActiveStates = new Dictionary<string, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // DisableDomainReload: clear statics so a stale destroyed reference
        // doesn't make the scene's PanelManager destroy itself in Awake.
        Instance = null;
        _hudActiveState.Clear();
    }

    private float _blackOverlayClearTime = 0f;
    private bool _hudCurrentlyVisibleState = true;
    private bool _firstFrameSet = false;

    public bool forceFreezeTimeScale = false;

    private Dictionary<string, List<Coroutine>> _panelCoroutines = new Dictionary<string, List<Coroutine>>();

    private void StartTrackedCoroutine(string name, IEnumerator routine)
    {
        if (_panelCoroutines.TryGetValue(name, out var list) && list != null)
        {
            foreach (var co in list)
            {
                if (co != null) StopCoroutine(co);
            }
            list.Clear();
        }
        else
        {
            list = new List<Coroutine>();
            _panelCoroutines[name] = list;
        }

        var newCo = StartCoroutine(routine);
        list.Add(newCo);
    }

    public void ClosePanelImmediate(string name, VisualElement panel = null, VisualElement card = null)
    {
        if (_panelCoroutines.TryGetValue(name, out var list) && list != null)
        {
            foreach (var co in list)
            {
                if (co != null) StopCoroutine(co);
            }
            list.Clear();
        }

        _transitioningPanels.Remove(name);
        _desiredActiveStates[name] = false;
        _activePanels.Remove(name);

        if (panel != null)
        {
            panel.RemoveFromClassList("visible");
            panel.style.display = DisplayStyle.None;
        }
        if (card != null)
        {
            card.RemoveFromClassList("visible");
        }

        UpdateGameplayState();
    }

    public void OpenPanel(string name, VisualElement panel, VisualElement card, System.Action closeCallback = null)
    {
        _desiredActiveStates[name] = true;
        if (panel != null)
        {
            panel.style.display = DisplayStyle.Flex;
            panel.AddToClassList("visible");
        }
        if (card != null)
        {
            card.AddToClassList("visible");
            card.MarkDirtyRepaint();
        }

        RegisterPanelActive(name, true, closeCallback);
        StartTrackedCoroutine(name, RegisterTransition(name, PanelTransitionDuration));
    }

    public void ClosePanel(string name, VisualElement panel, VisualElement card, System.Action onTransitionComplete = null)
    {
        _desiredActiveStates[name] = false;
        UpdateGameplayState(); // Trigger HUD to slide back in immediately in parallel with panel fade/scale out

        if (panel == null)
        {
            RegisterPanelActive(name, false);
            onTransitionComplete?.Invoke();
            return;
        }
        StartTrackedCoroutine(name, ClosePanelCoroutine(name, panel, card, onTransitionComplete));
    }

    private IEnumerator ClosePanelCoroutine(string name, VisualElement panel, VisualElement card, System.Action onTransitionComplete)
    {
        if (panel != null) panel.RemoveFromClassList("visible");
        if (card != null) card.RemoveFromClassList("visible");

        StartTrackedCoroutine(name, RegisterTransition(name, PanelTransitionDuration));

        yield return new WaitForSecondsRealtime(PanelTransitionDuration);

        // Ensure state wasn't changed to open again during the transition wait
        bool isDesiredActive = _desiredActiveStates.ContainsKey(name) && _desiredActiveStates[name];
        if (!isDesiredActive)
        {
            if (panel != null) panel.style.display = DisplayStyle.None;
            RegisterPanelActive(name, false);
            onTransitionComplete?.Invoke();
        }
    }

    public void RegisterPanelActive(string name, bool active, System.Action closeCallback = null)
    {
        if (active)
        {
            _activePanels[name] = closeCallback;
        }
        else
        {
            _activePanels.Remove(name);
            _desiredActiveStates[name] = false;
        }

        UpdateGameplayState();
    }

    private void UpdateGameplayState()
    {
        if (_lockCoroutine != null)
        {
            StopCoroutine(_lockCoroutine);
            _lockCoroutine = null;
        }

        bool anyActive = _activePanels.Count > 0;
        
        // Check desired states for HUD visibility
        bool anyDesiredActive = false;
        foreach (var val in _desiredActiveStates.Values)
        {
            if (val) { anyDesiredActive = true; break; }
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        var playerControl = player != null ? player.GetComponentInChildren<PlayerControl>() : null;
        var canvasGo = GameObject.Find("GameUICanvas");

        // 1. Manage Timescale and Control based on actual active states (after transitions finish)
        if (anyActive)
        {
            Time.timeScale = 0f;
            cowsins.PauseMenu.isPaused = true;

            if (cowsins.UIController.Instance != null)
                cowsins.UIController.Instance.UnlockMouse();
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }

            if (playerControl != null)
                playerControl.LoseControl();
        }
        else
        {
            cowsins.PauseMenu.isPaused = false;
            if (!forceFreezeTimeScale)
            {
                Time.timeScale = 1f;
            }

            if (playerControl != null)
                playerControl.GrantControl();

            if (gameObject.activeInHierarchy)
            {
                _lockCoroutine = StartCoroutine(ForceLockMouseCoroutine());
            }
        }

        // 2. Manage HUD Visibility based on desired states and black overlay
        if (canvasGo != null)
        {
            bool targetVisible = !anyDesiredActive;
            var uiDoc = canvasGo.GetComponent<UIDocument>();
            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                var root = uiDoc.rootVisualElement;
                var overlay = root.Q("BlackOverlay");
                if (overlay != null)
                {
                    bool isBlack = !overlay.ClassListContains("fade-out");
                    if (isBlack)
                    {
                        targetVisible = false;
                    }
                    else if (_blackOverlayClearTime != 0f)
                    {
                        float elapsed = Time.realtimeSinceStartup - _blackOverlayClearTime;
                        if (elapsed < 3.0f)
                        {
                            targetVisible = false;
                        }
                    }
                }
            }

            if (!_firstFrameSet || _hudCurrentlyVisibleState != targetVisible)
            {
                _hudCurrentlyVisibleState = targetVisible;
                _firstFrameSet = true;
                SetHUDVisible(canvasGo.transform, targetVisible);
            }
        }
    }

    private void Update()
    {
        var canvasGo = GameObject.Find("GameUICanvas");
        if (canvasGo == null) return;

        var uiDoc = canvasGo.GetComponent<UIDocument>();
        if (uiDoc == null || uiDoc.rootVisualElement == null) return;

        var root = uiDoc.rootVisualElement;
        var overlay = root.Q("BlackOverlay");
        if (overlay == null) return;

        bool isBlack = !overlay.ClassListContains("fade-out");
        bool targetVisible;

        if (isBlack)
        {
            _blackOverlayClearTime = 0f;
            targetVisible = false;
        }
        else
        {
            if (_blackOverlayClearTime == 0f)
            {
                _blackOverlayClearTime = Time.realtimeSinceStartup;
            }

            float elapsed = Time.realtimeSinceStartup - _blackOverlayClearTime;
            if (elapsed >= 3.0f)
            {
                bool anyDesiredActive = false;
                foreach (var val in _desiredActiveStates.Values)
                {
                    if (val) { anyDesiredActive = true; break; }
                }
                targetVisible = !anyDesiredActive;
            }
            else
            {
                targetVisible = false;
            }
        }

        if (!_firstFrameSet || _hudCurrentlyVisibleState != targetVisible)
        {
            _hudCurrentlyVisibleState = targetVisible;
            _firstFrameSet = true;
            SetHUDVisible(canvasGo.transform, targetVisible);
        }
    }

    private System.Collections.IEnumerator ForceLockMouseCoroutine()
    {
        for (int i = 0; i < 10; i++)
        {
            cowsins.PauseMenu.isPaused = false;
            if (cowsins.UIController.Instance != null)
                cowsins.UIController.Instance.LockMouse();
            else
            {
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }

#if UNITY_EDITOR
            PauseManager.EditorReallowCursorLock();
            System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                UnityEditor.EditorWindow.FocusWindowIfItsOpen(gameViewType);
            }
#endif
            yield return null;
        }
        _lockCoroutine = null;
    }

    public static void SetHUDVisible(Transform canvasRoot, bool visible)
    {
        if (canvasRoot != null)
        {
            string[] overlayNames = { "PausePanel", "GameOverPanel", "JournalUI", "SkillTreeWidget", "QuestTrackerWidget", "StatsPanel" };

            if (!visible)
            {
                if (_hudActiveState.Count == 0)
                {
                    for (int i = 0; i < canvasRoot.childCount; i++)
                    {
                        var child = canvasRoot.GetChild(i);
                        bool isOverlay = false;
                        foreach (var n in overlayNames)
                        {
                            if (child.name == n) { isOverlay = true; break; }
                        }
                        if (!isOverlay)
                        {
                            _hudActiveState[child.name] = child.gameObject.activeSelf;
                            child.gameObject.SetActive(false);
                        }
                    }
                }
            }
            else
            {
                if (_hudActiveState.Count > 0)
                {
                    for (int i = 0; i < canvasRoot.childCount; i++)
                    {
                        var child = canvasRoot.GetChild(i);
                        bool wasActive;
                        if (_hudActiveState.TryGetValue(child.name, out wasActive))
                            child.gameObject.SetActive(wasActive);
                    }
                    _hudActiveState.Clear();
                }
            }

            // Apply transition animation classes to UITK HUD elements
            var uiDoc = canvasRoot.GetComponent<UIDocument>();
            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                var root = uiDoc.rootVisualElement;
                string[] hudElementNames = {
                    "HealthCluster", "StaminaCluster", "AmmoCluster",
                    "CompassViewport", "ThreatWidget", "HUDChips",
                    "RightHUDColumn", "Crosshair", "FPSLabel"
                };

                foreach (var elName in hudElementNames)
                {
                    var element = root.Q(elName);
                    if (element != null)
                    {
                        if (!visible)
                        {
                            element.AddToClassList("hud-hidden");
                        }
                        else
                        {
                            element.RemoveFromClassList("hud-hidden");
                        }
                    }
                }
            }
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var crosshair = player.GetComponentInChildren<cowsins.Crosshair>(true);
            if (crosshair != null) crosshair.gameObject.SetActive(visible);
        }
    }

    public void RegisterPanelTransitioning(string name, bool transitioning)
    {
        if (transitioning)
        {
            _transitioningPanels.Add(name);
        }
        else
        {
            _transitioningPanels.Remove(name);
        }
    }

    private IEnumerator RegisterTransition(string name, float duration)
    {
        RegisterPanelTransitioning(name, true);
        yield return new WaitForSecondsRealtime(duration);
        RegisterPanelTransitioning(name, false);
    }

    public bool IsAnyPanelActive()
    {
        return _activePanels.Count > 0;
    }

    public bool IsAnyPanelTransitioning()
    {
        return _transitioningPanels.Count > 0;
    }

    public bool IsPanelActive(string name)
    {
        return _activePanels.ContainsKey(name);
    }

    public bool CloseActivePanel()
    {
        // Copy keys to avoid InvalidOperationException when the callback
        // removes entries from _activePanels (e.g. skillTree.Close).
        var keys = new List<string>(_activePanels.Keys);
        foreach (var key in keys)
        {
            if (_activePanels.TryGetValue(key, out var callback))
            {
                if (key != "Pause" && key != "GameOver" && callback != null)
                {
                    Debug.Log($"[PanelManager] Generic ESC -> Closing active panel: {key}");
                    callback.Invoke();
                    return true;
                }
            }
        }
        return false;
    }

    public void ForceLockMouse()
    {
        if (gameObject.activeInHierarchy)
        {
            if (_lockCoroutine != null) StopCoroutine(_lockCoroutine);
            _lockCoroutine = StartCoroutine(ForceLockMouseCoroutine());
        }
    }

    public bool CanOpenPanel(string name)
    {
        // GameOver blocks everything except GameOver itself
        if (_activePanels.ContainsKey("GameOver"))
        {
            return name == "GameOver";
        }

        // If any panel is transitioning, block
        if (IsAnyPanelTransitioning())
        {
            return false;
        }

        // If another panel is active, block
        foreach (var p in _activePanels.Keys)
        {
            if (p != name)
            {
                return false;
            }
        }

        return true;
    }
}
