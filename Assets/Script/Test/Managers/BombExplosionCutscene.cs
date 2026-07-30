using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UIElements;

public class BombExplosionCutscene : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("World position for the temporary cutscene camera. Must have a clear line of sight to explosionPoint — the old default (-271, 37.08, 42.47) was blocked by 4 high-rise buildings at ~179-211 units, making the explosion invisible. New default (-89, 41, 1.4) is 90 units west at height 41 with verified clear LOS.")]
    public Vector3 cameraPoint = new Vector3(-89f, 41f, 1.4f);

    [Header("Explosion")]
    [Tooltip("World position where the bomb explodes (mushroom cloud VFX + SFX origin).")]
    public Vector3 explosionPoint = new Vector3(0.92f, 0.9f, 1.4f);

    [Tooltip("Mushroom cloud VFX prefab (e.g. FX_Nuke_Light_01).")]
    public GameObject nukeVfxPrefab;

    [Tooltip("Uniform scale multiplier applied to the instantiated VFX.")]
    public float vfxScale = 4f;

    [Tooltip("Explosion sound effect.")]
    public AudioClip explosionSfx;

    [Tooltip("Optional mixer group so the SFX respects the master volume slider. Safe to leave null.")]
    public AudioMixerGroup sfxMixerGroup;

    [Range(0f, 1f)]
    public float sfxVolume = 1f;

    [Header("Timing")]
    [Tooltip("Fade to/from black duration at the start/end of the cutscene.")]
    public float fadeDuration = 0.6f;

    [Tooltip("How long (seconds) the camera holds on the explosion.")]
    public float holdDuration = 5f;

    [Tooltip("How long (real seconds) the VFX instance stays before being destroyed after the cutscene ends.")]
    public float vfxLifetime = 20f;

    private bool _played;
    private bool _playing;
    private VisualElement _fadeRoot;
    private GameObject _fadeDocGO;

    /// <summary>True while the bomb explosion cutscene sequence is running.</summary>
    public bool IsPlaying => _playing;

    public void Play(System.Action onComplete = null)
    {
        if (_played) { onComplete?.Invoke(); return; }
        _played = true;
        CutscenePlayer._playingCount++;
        StartCoroutine(PlaySequence(onComplete));
    }

    private IEnumerator PlaySequence(System.Action onComplete)
    {
        _playing = true;
        BuildFadeOverlay();

        float prevTimeScale = Time.timeScale;

        _fadeRoot.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(fadeDuration);

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        Rigidbody playerRb = playerGO != null ? playerGO.GetComponent<Rigidbody>() : null;
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }
        Time.timeScale = 0f;

        var mainCam = Camera.main;
        AudioListener mainListener = mainCam != null ? mainCam.GetComponent<AudioListener>() : null;
        if (mainListener != null) mainListener.enabled = false;

        var camGO = new GameObject("BombExplosion_CutsceneCamera");
        camGO.transform.position = cameraPoint;
        Vector3 lookDir = explosionPoint - cameraPoint;
        camGO.transform.rotation = lookDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDir.normalized, Vector3.up)
            : Quaternion.identity;

        var cam = camGO.AddComponent<Camera>();
        if (mainCam != null)
        {
            cam.fieldOfView = mainCam.fieldOfView;
            cam.nearClipPlane = mainCam.nearClipPlane;
            cam.farClipPlane = mainCam.farClipPlane;
            cam.clearFlags = mainCam.clearFlags;
            cam.backgroundColor = mainCam.backgroundColor;
            cam.cullingMask = mainCam.cullingMask;
            cam.depth = mainCam.depth + 10f;

            var mainPPLayer = mainCam.GetComponent<PostProcessLayer>();
            if (mainPPLayer != null)
            {
                var ppLayer = camGO.AddComponent<PostProcessLayer>();
                ppLayer.volumeLayer = mainPPLayer.volumeLayer;
                ppLayer.volumeTrigger = camGO.transform;
                ppLayer.antialiasingMode = PostProcessLayer.Antialiasing.None;

                var resourcesField = typeof(PostProcessLayer).GetField(
                    "m_Resources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (resourcesField != null)
                    resourcesField.SetValue(ppLayer, resourcesField.GetValue(mainPPLayer));
            }
        }
        else
        {
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
        }

        camGO.AddComponent<AudioListener>();

        GameObject vfxInstance = null;
        if (nukeVfxPrefab != null)
        {
            vfxInstance = Instantiate(nukeVfxPrefab, explosionPoint, Quaternion.identity);
            vfxInstance.transform.localScale *= vfxScale;

            var systems = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }
        }
        else
        {
            Debug.LogWarning("[BombExplosionCutscene] nukeVfxPrefab is not assigned — no VFX will be spawned.");
        }

        if (explosionSfx != null)
        {
            var sfxGO = new GameObject("BombExplosion_SFX");
            sfxGO.transform.position = explosionPoint;
            var src = sfxGO.AddComponent<AudioSource>();
            src.clip = explosionSfx;
            src.outputAudioMixerGroup = sfxMixerGroup;
            src.spatialBlend = 0f;
            src.volume = sfxVolume;
            src.playOnAwake = false;
            src.Play();
            Destroy(sfxGO, explosionSfx.length + 0.5f);
        }

        _fadeRoot.style.opacity = 0f;
        yield return new WaitForSecondsRealtime(fadeDuration);

        yield return new WaitForSecondsRealtime(holdDuration);

        _fadeRoot.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(fadeDuration);

        Destroy(camGO);
        if (mainListener != null) mainListener.enabled = true;
        if (vfxInstance != null) Destroy(vfxInstance, vfxLifetime);

        Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;

        _fadeRoot.style.opacity = 0f;
        yield return new WaitForSecondsRealtime(fadeDuration);

        Destroy(_fadeDocGO);
        _playing = false;
        if (CutscenePlayer._playingCount > 0) CutscenePlayer._playingCount--;
        Debug.Log("[BombExplosionCutscene] Sequence complete.");
        onComplete?.Invoke();
    }

    private void BuildFadeOverlay()
    {
        _fadeDocGO = new GameObject("BombExplosion_FadeOverlay", typeof(UIDocument));
        _fadeDocGO.transform.SetParent(transform, false);
        var doc = _fadeDocGO.GetComponent<UIDocument>();
        doc.sortingOrder = 2000;

        // Copy panelSettings from an existing screen-space UIDocument so the panel actually renders.
        // Must filter out WorldSpacePanelSettings — see UIPanelSettingsUtil for details.
        var ssDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(doc);
        if (ssDoc != null)
        {
            doc.panelSettings = ssDoc.panelSettings;
        }
        if (doc.panelSettings == null)
        {
            doc.panelSettings = UIPanelSettingsUtil.FindScreenSpacePanelSettingsAsset();
        }

        var root = new VisualElement();
        root.name = "FadeOverlay";
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        root.style.backgroundColor = Color.black;
        root.style.opacity = 0f;
        root.pickingMode = PickingMode.Ignore;

        _fadeRoot = root;
        doc.rootVisualElement.Add(root);
        var sheet = Resources.Load<StyleSheet>("BombExplosionCutscene");
        if (sheet != null) doc.rootVisualElement.styleSheets.Add(sheet);
    }
}
