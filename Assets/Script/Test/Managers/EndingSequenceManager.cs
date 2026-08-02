using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the full story ending sequence when a target quest completes
/// (e.g. Quest 12 — escaping the town):
///
///   Quest completes (journal reward popup may open, handled by JournalUI as usual)
///     -> wait until the journal popup (if any) is closed by the player
///     -> EndingBlackout fades the screen to black (owned here, order 3000)
///     -> BombExplosionCutscene.Play()   (explosion; ends on a black screen)
///     -> EpilogueSlide.Play()           (cure announcement, revealed from black)
///     -> FollowerEndingSlide.Play()     (3 variants based on follower count)
///     -> CreditsSequence.Play()         (scrolls; ESC/click skips)
///     -> fade to black -> load the main menu scene
///
/// The whole chain runs under Time.timeScale == 0 and every fade goes through
/// the shared EndingBlackout, so the gameplay never flashes between steps.
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

    [Header("Transitions")]
    [Tooltip("Fade-to-black duration used between steps.")]
    public float toBlackDuration = 0.6f;
    [Tooltip("Fade-from-black duration used to reveal each step.")]
    public float fromBlackDuration = 0.8f;

    private bool _fired;
    private EndingBlackout _blackout;

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
        // Shared full-screen blackout, transparent by default (built in Awake).
        _blackout = gameObject.AddComponent<EndingBlackout>();

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

        // 1) Cut to black, then run the bomb cutscene. Its own short fade is
        //    invisible on the blackout; fade the blackout away in sync with the
        //    bomb's fade-open so the explosion is actually visible.
        yield return FadeToBlack();
        Debug.Log("[EndingSequenceManager] black. starting bomb.");
        if (bombCutscene != null)
        {
            bool done = false;
            bombCutscene.Play(() => done = true);
            yield return FadeFromBlack();
            while (!done) yield return null;
            Debug.Log("[EndingSequenceManager] bomb done.");
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] bombCutscene not assigned — skipping.");
        }

        // Time stays frozen from here until the main menu loads.
        Time.timeScale = 0f;

        // Ensure the blackout (order 3000) is fully covering the bomb's own
        // fade overlay (order 2000), then remove the bomb overlay so the next
        // step's lower-order panel is not hidden behind it.
        _blackout.SnapBlack();
        if (bombCutscene != null) bombCutscene.ReleaseFadeOverlay();
        Debug.Log("[EndingSequenceManager] blackout snapped, bomb overlay released.");

        // 2) Epilogue: revealed from black, typed out, then black again.
        if (epilogue != null)
        {
            bool done = false;
            epilogue.Play(() => { done = true; Debug.Log("[EndingSequenceManager] epilogue done callback."); });
            yield return FadeFromBlack();
            while (!done) yield return null;
            Debug.Log("[EndingSequenceManager] fading to black after epilogue.");
            yield return FadeToBlack();
            epilogue.Dispose();
            Debug.Log("[EndingSequenceManager] epilogue disposed.");
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] epilogue not assigned — skipping.");
        }

        // 3) Follower ending slide (variant by companion count).
        if (followerEnding != null)
        {
            bool done = false;
            followerEnding.Play(() => done = true);
            yield return FadeFromBlack();
            while (!done) yield return null;
            yield return FadeToBlack();
            followerEnding.Dispose();
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] followerEnding not assigned — skipping.");
        }

        // 4) Credits: revealed from black, scrolls (ESC/click skips), then
        //    fade to black and load the main menu — never showing gameplay.
        if (credits != null)
        {
            bool done = false;
            credits.Play(() => done = true);
            yield return FadeFromBlack();
            while (!done) yield return null;
            yield return FadeToBlack();
            credits.ExitToMainMenu();
        }
        else
        {
            Debug.LogWarning("[EndingSequenceManager] credits not assigned — loading main menu directly.");
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
        }
    }

    private IEnumerator FadeToBlack()
    {
        if (_blackout == null) yield break;
        bool done = false;
        _blackout.FadeToBlack(toBlackDuration, () => done = true);
        while (!done) yield return null;
    }

    private IEnumerator FadeFromBlack()
    {
        if (_blackout == null) yield break;
        bool done = false;
        _blackout.FadeFromBlack(fromBlackDuration, () => done = true);
        while (!done) yield return null;
    }
}
