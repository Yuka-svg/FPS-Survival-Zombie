using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scenes")]
    public string gameSceneName = "Story mode";
    public string endlessSceneName = "Endless";

    private UIDocument _doc;
    private Label _bestLabel;
    private Label _bestEndlessLabel;

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void Start()
    {
        Time.timeScale = 1f;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        // Start playing the main menu music.
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayMenuMusic();

        if (_doc != null)
        {
            var root = _doc.rootVisualElement;

            // Set all TemplateContainers to ignore picking so clicks can pass through to inner panels
            foreach (var tc in root.Query<TemplateContainer>().ToList())
            {
                tc.pickingMode = PickingMode.Ignore;
            }

            var playBtn = root.Q("MainMenuModule_Play");
            var endlessBtn = root.Q("MainMenuModule_Endless");
            var quitBtn = root.Q("TacticalQuitButton");
            _bestLabel = root.Q<Label>("BestText");
            _bestEndlessLabel = root.Q<Label>("BestEndlessText");

            if (playBtn != null)
                playBtn.RegisterCallback<ClickEvent>(_ => PlayGame());
            if (endlessBtn != null)
                endlessBtn.RegisterCallback<ClickEvent>(_ => PlayEndless());
            if (quitBtn != null)
                quitBtn.RegisterCallback<ClickEvent>(_ => QuitGame());
        }

        RefreshBestScore();
        StartCoroutine(BindToPlayFab());
    }

    private IEnumerator BindToPlayFab()
    {
        float timeout = 10f;
        while (PlayFabManager.Instance == null && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        var pm = PlayFabManager.Instance;
        if (pm != null)
        {
            pm.OnLoginSuccess += HandleLoginSuccess;
            pm.OnCloudDataLoaded += HandleCloudDataLoaded;
            if (pm.IsLoggedIn) RefreshBestScore();
        }
    }

    private void OnDisable()
    {
        var pm = PlayFabManager.Instance;
        if (pm != null)
        {
            pm.OnLoginSuccess -= HandleLoginSuccess;
            pm.OnCloudDataLoaded -= HandleCloudDataLoaded;
        }
    }

    private void HandleLoginSuccess(string username)
    {
        RefreshBestScore();
    }

    private void HandleCloudDataLoaded(bool success)
    {
        if (success) RefreshBestScore();
    }

    public void RefreshBestScore()
    {
        int bestScore = PlayerPrefs.GetInt("BestScore", 0);
        int bestWave = PlayerPrefs.GetInt("BestWave", 0);
        if (_bestLabel != null)
        {
            _bestLabel.text = bestScore > 0
                ? ("Best  " + bestScore + "    Wave " + bestWave)
                : "No record yet";
        }

        int bestEndlessScore = PlayerPrefs.GetInt("BestEndlessScore", 0);
        int bestEndlessWave = PlayerPrefs.GetInt("BestEndlessWave", 0);
        if (_bestEndlessLabel != null)
        {
            _bestEndlessLabel.text = bestEndlessScore > 0 || bestEndlessWave > 0
                ? ("Best  " + bestEndlessScore + "    Wave " + bestEndlessWave)
                : "No record yet";
        }
    }

    public void PlayGame()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.SetMode(GameMode.Story);
        StartCoroutine(LaunchOperationCoroutine(gameSceneName));
    }

    public void PlayEndless()
    {
        if (GameModeManager.Instance != null)
            GameModeManager.Instance.SetMode(GameMode.Endless);
        StartCoroutine(LaunchOperationCoroutine(endlessSceneName));
    }

    private IEnumerator LaunchOperationCoroutine(string targetSceneName)
    {
        Time.timeScale = 1f;

        // 1. Find all required components
        var loginUI = FindFirstObjectByType<PlayFabLoginUI>();
        var profileWidget = FindFirstObjectByType<PlayerProfileWidget>();
        var leaderboardWidget = FindFirstObjectByType<LeaderboardWidget>();
        var cameraOrbit = FindFirstObjectByType<MenuCameraOrbit>();

        // 2. Close any open widgets/panels (Profile, Rankings)
        if (profileWidget != null && profileWidget.IsPanelVisible)
        {
            profileWidget.SetPanelVisible(false);
        }
        if (leaderboardWidget != null && leaderboardWidget.IsPanelVisible)
        {
            leaderboardWidget.SetPanelVisible(false);
        }

        // 3. Slide out all left menu modules, quit button, and logout button sequentially (bottom first)
        if (loginUI != null)
        {
            yield return StartCoroutine(loginUI.SlideOutAllMenuElementsCoroutine());
            yield return new WaitForSeconds(1.0f); // Allow remaining time of transition to finish (1.5s total)
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        // 5. Get the black overlay element
        VisualElement overlay = null;
        if (_doc != null)
        {
            overlay = _doc.rootVisualElement.Q("BlackOverlay");
        }
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        // Hide and lock cursor immediately when fade-to-black starts
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
#if UNITY_EDITOR
        PauseManager.EditorReallowCursorLock();
#endif

        // 6. Transition: Shrink all camera parameters to 0 over 3 seconds (while USS transition handles fade-to-black)
        float startRadius = cameraOrbit != null ? cameraOrbit.radius : 130f;
        float startHeight = cameraOrbit != null ? cameraOrbit.height : 55f;
        float startBob = cameraOrbit != null ? cameraOrbit.bobAmplitude : 2.5f;
        float startSway = cameraOrbit != null ? cameraOrbit.lookSwayDeg : 1.1f;
        
        float duration = 3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            
            // Standard Ease-In-Out for camera parameters
            float easeT = t * t * (3f - 2f * t);

            // Ease-Out Cubic for horizontal radius: 1 - (1 - t)^3
            float invT = 1f - t;
            float easeT_radius = 1f - (invT * invT * invT);

            // Ease-In Cubic for vertical height: t^3
            float easeT_height = t * t * t;

            // Lerp camera orbit parameters to 0
            if (cameraOrbit != null)
            {
                cameraOrbit.radius = Mathf.Lerp(startRadius, 0f, easeT_radius);
                cameraOrbit.height = Mathf.Lerp(startHeight, 0f, easeT_height);
                cameraOrbit.bobAmplitude = Mathf.Lerp(startBob, 0f, easeT);
                cameraOrbit.lookSwayDeg = Mathf.Lerp(startSway, 0f, easeT);
            }

            yield return null;
        }

        // Ensure final values
        if (cameraOrbit != null)
        {
            cameraOrbit.radius = 0f;
            cameraOrbit.height = 0f;
            cameraOrbit.bobAmplitude = 0f;
            cameraOrbit.lookSwayDeg = 0f;
        }

        // 7. Load scene
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.ResetProgress();

        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayChapterMusic(1);

        SceneManager.LoadScene(targetSceneName);
    }

    public void QuitGame()
    {
        StartCoroutine(TransitionAndQuit());
    }

    private System.Collections.IEnumerator TransitionAndQuit()
    {
        // 1. Find all required components (Crucial fix: resolved compilation error due to undefined variables)
        var loginUI = FindFirstObjectByType<PlayFabLoginUI>();
        var profileWidget = FindFirstObjectByType<PlayerProfileWidget>();
        var leaderboardWidget = FindFirstObjectByType<LeaderboardWidget>();

        // 2. Close any open widgets/panels (Profile, Rankings)
        if (profileWidget != null && profileWidget.IsPanelVisible)
        {
            profileWidget.SetPanelVisible(false);
        }
        if (leaderboardWidget != null && leaderboardWidget.IsPanelVisible)
        {
            leaderboardWidget.SetPanelVisible(false);
        }

        // 3. Slide out all left menu modules, quit button, and logout button sequentially (bottom first)
        if (loginUI != null)
        {
            yield return StartCoroutine(loginUI.SlideOutAllMenuElementsCoroutine());
            yield return new WaitForSeconds(1.0f); // Allow remaining time of transition to finish (1.5s total)
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        // 5. Get the black overlay element, make it visible and transparent initially
        // 5. Get the black overlay element
        VisualElement overlay = null;
        if (_doc != null)
        {
            overlay = _doc.rootVisualElement.Q("BlackOverlay");
        }
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        // Hide and lock cursor immediately when fade-to-black starts
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
#if UNITY_EDITOR
        PauseManager.EditorReallowCursorLock();
#endif

        yield return new WaitForSecondsRealtime(3.0f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
