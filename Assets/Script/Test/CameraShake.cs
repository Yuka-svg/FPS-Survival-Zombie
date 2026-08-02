using UnityEngine;
using System.Collections;
using cowsins;

public class CameraShake : MonoBehaviour
{
    private Vector3 startPos;
    private Coroutine shakeRoutine;
    private PlayerStats playerStats;

    private void Awake()
    {
        startPos = transform.localPosition;
    }

    private void Start()
    {
        playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
            playerStats.AddOnDieListener(CancelShake);
    }

    private void OnDestroy()
    {
        if (playerStats != null)
            playerStats.RemoveOnDieListener(CancelShake);
    }

    public void Shake()
    {
        Shake(0.15f, 0.1f);
    }

    public void Shake(float duration, float magnitude)
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine =
            StartCoroutine(
                DoShake(duration, magnitude)
            );
    }

    private void CancelShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        transform.localPosition = startPos;
    }

    private IEnumerator DoShake(
        float duration,
        float magnitude
    )
    {
        float timer = 0;

        while (timer < duration)
        {
            transform.localPosition =
                startPos +
                Random.insideUnitSphere *
                magnitude;

            timer += Time.unscaledDeltaTime;

            yield return null;
        }

        transform.localPosition = startPos;
    }
}
