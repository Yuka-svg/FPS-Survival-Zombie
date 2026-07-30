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

    public static GameMode CurrentMode => Instance != null ? Instance.currentMode : _persistedMode;

    private void Awake()
    {
        currentMode = _persistedMode;
        Instance = this;
    }

    public void SetMode(GameMode mode)
    {
        currentMode = mode;
        _persistedMode = mode;
    }
}
