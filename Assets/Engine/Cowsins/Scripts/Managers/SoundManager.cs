using UnityEngine;
using System.Collections;
using System.Collections.Generic;
namespace cowsins
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance;

        [SerializeField] private AudioSource source3D;

        private AudioSource src;
        private readonly Dictionary<float, WaitForSeconds> delayCache = new Dictionary<float, WaitForSeconds>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
            }
            else Destroy(this.gameObject);

            src = GetComponent<AudioSource>();
            src.spatialBlend = 0f;

            if (source3D != null && PoolManager.Instance != null) PoolManager.Instance.RegisterPool(source3D.gameObject, 5);
        }

        private WaitForSeconds GetWaitForSeconds(float seconds)
        {
            if (seconds <= 0f) return null;
            if (!delayCache.TryGetValue(seconds, out var wfs))
            {
                wfs = new WaitForSeconds(seconds);
                delayCache.Add(seconds, wfs);
            }
            return wfs;
        }

        public void PlaySound(AudioClip clip, float delay, float pitchAdded, bool randomPitch)
        {
            StartCoroutine(Play(clip, delay, pitchAdded, randomPitch));
        }
        public void PlaySoundAtPosition(AudioClip clip, Vector3 position, float delay, float pitchAdded, bool randomPitch)
        {
            StartCoroutine(PlayAtPosition(clip, position, delay, pitchAdded, randomPitch));
        }

        private IEnumerator Play(AudioClip clip, float delay, float pitch, bool randomPitch)
        {
            if (clip == null) yield break;
            if (delay > 0) yield return GetWaitForSeconds(delay);
            float pitchAdded = randomPitch ? Random.Range(-pitch, pitch) : pitch;
            src.pitch = 1 + pitchAdded;
            src.PlayOneShot(clip);
            yield return null;
        }

        private IEnumerator PlayAtPosition(AudioClip clip, Vector3 position, float delay, float pitch, bool randomPitch)
        {
            if (clip == null) yield break;
            if (delay > 0) yield return GetWaitForSeconds(delay);

            AudioSource newSource = PoolManager.Instance.GetFromPool(source3D.gameObject, position, Quaternion.identity).GetComponent<AudioSource>();
            newSource.spatialBlend = 1f;
            newSource.volume = 1f;

            float pitchAdded = randomPitch ? Random.Range(-pitch, pitch) : pitch;
            newSource.pitch = 1 + pitchAdded;

            newSource.clip = clip;
            newSource.Play();

            yield return new WaitForSeconds(clip.length / newSource.pitch);
            PoolManager.Instance.ReturnToPool(newSource.gameObject, source3D.gameObject);
        }
    }
}

