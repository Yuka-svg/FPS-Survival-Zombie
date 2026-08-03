using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance;

    public delegate void WaveEvent(int wave);
    public delegate void WaveProgressEvent(int zombiesKilled, int zombiesToKill);
    public event WaveEvent OnWaveStarted;
    public event WaveEvent OnWaveCompleted;
    public event WaveProgressEvent OnWaveProgressChanged;

    [Header("Wave")]
    public int currentWave = 1;

    public int baseZombieCount = 10;

    [Header("Endless Mode")]
    [Tooltip("Extra zombies added per wave beyond wave 1 for endless mode. Overrides the default +5 when higher.")]
    public int endlessZombiesPerWave = 8;

    [Header("Boss Wave")]
    [Tooltip("Every Nth wave is a boss wave (Boomer/Tank guaranteed spawn). 0 = disabled.")]
    public int bossWaveInterval = 5;

    public int zombiesToKill;

    public int zombiesKilledThisWave;

    private void Awake()
    {
        Instance = this;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        // DisableDomainReload: clear the singleton so a stale destroyed
        // reference doesn't leak across play sessions.
        Instance = null;
    }

    private void Start()
    {
        if (FindFirstObjectByType<WeaponSelectionWidget>() == null)
            StartWave();
        else
            currentWave = 0;
    }

    public void StartWave()
    {
        if (currentWave == 0) currentWave = 1;
        zombiesKilledThisWave = 0;

        int perWaveStep = GameModeManager.CurrentMode == GameMode.Endless
            ? endlessZombiesPerWave
            : 5;

        zombiesToKill =
            baseZombieCount +
            ((currentWave - 1) * perWaveStep);

        if (bossWaveInterval > 0 && currentWave % bossWaveInterval == 0)
            zombiesToKill += baseZombieCount / 2;

        OnWaveStarted?.Invoke(currentWave);

        Debug.Log(
            "Wave " +
            currentWave +
            " Started. Need Kill: " +
            zombiesToKill
        );
    }

    public bool IsBossWave()
    {
        return bossWaveInterval > 0 && currentWave % bossWaveInterval == 0;
    }

    public void RegisterZombieKill()
    {
        zombiesKilledThisWave++;
        OnWaveProgressChanged?.Invoke(zombiesKilledThisWave, zombiesToKill);

        if (
            zombiesKilledThisWave >=
            zombiesToKill
        )
        {
            NextWave();
        }
    }

    private void NextWave()
    {
        OnWaveCompleted?.Invoke(currentWave);
        currentWave++;
        ScoreManager.Instance?.
        AddWaveBonus(
            currentWave * 500
        );

        StartWave();

        Debug.Log(
            "Next Wave: " +
            currentWave
        );
    }
}