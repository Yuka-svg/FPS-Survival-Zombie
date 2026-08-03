using UnityEngine;

public enum GameMode
{
    Story,
    Endless
}

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance;
    private static GameMode _persistedMode = GameMode.Story;

    [SerializeField] private GameMode currentMode = GameMode.Story;

    public static GameMode CurrentMode => StoryManager.Instance != null ? GameMode.Story : (Instance != null ? Instance.currentMode : _persistedMode);

    private void Awake()
    {
        Instance = this;
        _persistedMode = currentMode;
    }

    public void SetMode(GameMode mode)
    {
        currentMode = mode;
        _persistedMode = mode;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        // DisableDomainReload: clear the singleton and the persisted mode so
        // a stale destroyed reference / leftover mode doesn't leak across
        // play sessions (e.g. Story kept in memory after an Endless run).
        Instance = null;
        _persistedMode = GameMode.Story;
    }
}
