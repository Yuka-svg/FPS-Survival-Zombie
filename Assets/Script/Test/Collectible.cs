using System.Collections.Generic;
using UnityEngine;

public class Collectible : MonoBehaviour
{
    public static readonly List<Collectible> ActiveCollectibles = new List<Collectible>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveCollectibles.Clear();
    }

    private void OnEnable()
    {
        if (!ActiveCollectibles.Contains(this))
            ActiveCollectibles.Add(this);
    }

    private void OnDisable()
    {
        ActiveCollectibles.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveCollectibles.Remove(this);
    }

    public JournalData journal;

    bool picked = false;

    /// <summary>True after this collectible has been picked up by the player.</summary>
    public bool IsPicked => picked;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        Collect();
    }

    public void Collect()
    {
        if (picked) return;

        picked = true;

        CollectibleManager.Instance.Collect(journal);

        gameObject.SetActive(false);
    }
}