using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    private Vector3 startPos;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        startPos = transform.localPosition;
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

            timer += Time.deltaTime;

            yield return null;
        }

        transform.localPosition = startPos;
    }
}