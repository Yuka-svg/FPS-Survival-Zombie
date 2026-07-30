using System.Collections;
using UnityEngine;

/// <summary>
/// Orchestrates the full story ending sequence when a target quest completes
/// (e.g. Quest 12 — escaping the town):
///
///   Quest completes (journal reward popup may open, handled by JournalUI as usual)
///     -> wait until the journal popup (if any) is closed by the player
///     -> BombExplosionCutscene.Play()
///     -> EpilogueSlide.Play()       (cure announcement)
///     -> FollowerEndingSlide.Play() (3 variants based on follower count)
///     -> CreditsSequence.Play()     (loads the main menu scene when done)
///
/// This is a pure additive listener: it does not modify QuestTrigger,
/// StoryManager, WaveQuestInteractable, or JournalUI. It only polls
/// JournalUI.Instance.IsOpen (an existing public property) and calls Play()
/// on the three step components, which likewise do not self-trigger.
/// </summary>
public class EndingSequenceManager : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Quest that must complete to start the ending sequence (e.g. Quest_12_EscapeTown). " +
             "Leave null to fire on ANY quest completion (not recommended).")]
    public QuestData targetQuest;

    [Header("Steps (in order)")]
    public BombExplosionCutscene bombCutscene;
    public EpilogueSlide epilogue;
    public FollowerEndingSlide followerEnding;
    public CreditsSequence credits;

    private bool _fired;

    private void OnEnable() => Subscribe();

    private void Start() => Subscribe(); // Fallback in case OnEnable ran before StoryManager.Awake.

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
    }

    private void Subscribe()
    {
        if (StoryManager.Instance == null) return;
        StoryManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        StoryManager.Instance.OnQuestCompleted += HandleQuestCompleted;
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        if (_fired) return;
        if (targetQuest != null && quest != targetQuest) return;

        _fired = true;
        Debug.Log($"[EndingSequenceManager] Starting ending sequence for quest '{quest?.title}'.");
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // If completing this quest granted a journal reward, JournalUI.Show()
        // has already been called synchronously by StoryManager.GrantRewards()
        // by the time OnQuestCompleted fires. Wait for the player to close it
        // before cutting to the bomb cutscene, so the two don't overlap.
        if (JournalUI.Instance != null)
        {
            // Give the journal a frame to actually open (Show() runs earlier in
            // the same call stack, but yield once to be safe against ordering).
            yield return null;
            while (JournalUI.Instance.IsOpen)
                yield return null;
        }

        if (bombCutscene != null)
        {
            bool done = false;
            bombCutscene.Play(() => done = true);
            while (!done) yield return null;
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] bombCutscene not assigned — skipping.");
        }

        if (epilogue != null)
        {
            bool done = false;
            epilogue.Play(() => done = true);
            while (!done) yield return null;
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] epilogue not assigned — skipping.");
        }

        if (followerEnding != null)
        {
            bool done = false;
            followerEnding.Play(() => done = true);
            while (!done) yield return null;
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] followerEnding not assigned — skipping.");
        }

        if (credits != null)
        {
            credits.Play(); // Terminal step — loads the main menu scene itself.
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] credits not assigned — ending sequence has nothing left to do.");
        }
    }
}
