using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public delegate void ScoreEvent();

    public event ScoreEvent OnScoreChanged;
    public event ScoreEvent OnKillsChanged;
    public event ScoreEvent OnCritsChanged;

    [Header("Score")]
    public int score;

    [Header("Stats")]
    public int kills;
    public int crits;

    private float survivalTimer;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        survivalTimer += Time.deltaTime;
    }

    public void AddKill(int amount = 100)
    {
        kills++;
        score += amount;
        OnKillsChanged?.Invoke();
        OnScoreChanged?.Invoke();
        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterKill(amount);
    }

    public void AddCrit(int amount = 50)
    {
        crits++;
        score += amount;
        OnCritsChanged?.Invoke();
        OnScoreChanged?.Invoke();
        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterCrit(amount);
    }

    public void AddWaveBonus(int amount)
    {
        score += amount;
        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.AddScore(amount);
    }

    public int GetSurvivalBonus()
    {
        return Mathf.RoundToInt(survivalTimer);
    }

    public int GetFinalScore()
    {
        return score + GetSurvivalBonus();
    }

    public float GetSurvivalTime()
    {
        return survivalTimer;
    }
}