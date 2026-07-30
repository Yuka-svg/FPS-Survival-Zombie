using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone player flashlight toggled by the FlashlightToggle input action
/// (bound to F by default in PlayerActions.inputactions).
///
/// This is independent of the Cowsins weapon-attachment Flashlight system —
/// the player always has this personal flashlight, regardless of which weapon
/// or attachments are equipped. It works alongside the Cowsins system without
/// conflicting with it.
///
/// The flashlight is a Spot Light parented to the player's main camera so it
/// always points where the player looks. If no light is assigned in the
/// Inspector, one is created at runtime as a child of the main camera.
/// </summary>
public class PlayerFlashlight : MonoBehaviour
{
    [Header("Flashlight Light")]
    [Tooltip("The Spot Light used as the flashlight. If null, one is created as a child of the main camera at runtime.")]
    public Light flashlightLight;

    [Tooltip("If true, the flashlight starts on. If false, starts off.")]
    public bool defaultOn = false;

    [Header("Auto-Created Light Settings")]
    [Tooltip("Spot angle (degrees) used when auto-creating the light.")]
    [Range(10f, 130f)]
    public float spotAngle = 70f;

    [Tooltip("Range used when auto-creating the light.")]
    public float range = 25f;

    [Tooltip("Intensity used when auto-creating the light.")]
    public float intensity = 3f;

    [Tooltip("Color used when auto-creating the light.")]
    public Color lightColor = new Color(1f, 0.95f, 0.8f, 1f);

    [Header("Audio (optional)")]
    [Tooltip("SFX played when the flashlight turns on. Optional.")]
    public AudioClip toggleOnSFX;

    [Tooltip("SFX played when the flashlight turns off. Optional.")]
    public AudioClip toggleOffSFX;

    [Tooltip("Volume of the toggle SFX.")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.7f;

    /// <summary>True while the flashlight is on.</summary>
    public bool IsOn { get; private set; }

    private Light _light;
    private bool _createdRuntime;

    private void Awake()
    {
        // Use the assigned light or create one.
        if (flashlightLight != null)
        {
            _light = flashlightLight;
        }
        else
        {
            _light = CreateFlashlightLight();
            _createdRuntime = true;
        }

        if (_light != null)
        {
            _light.type = LightType.Spot;
            _light.enabled = defaultOn;
            IsOn = defaultOn;
        }
    }

    private Light CreateFlashlightLight()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            // Fallback: find any camera tagged MainCamera.
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in cams)
            {
                if (c.CompareTag("MainCamera")) { cam = c; break; }
            }
        }
        if (cam == null)
        {
            Debug.LogWarning("[PlayerFlashlight] No main camera found; flashlight will not be created.");
            return null;
        }

        var go = new GameObject("PlayerFlashlight_Light");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = spotAngle;
        light.range = range;
        light.intensity = intensity;
        light.color = lightColor;
        light.shadows = LightShadows.None; // Point/Spot shadows are expensive; keep off.
        light.renderMode = LightRenderMode.ForcePixel;

        return light;
    }

    private void Update()
    {
        // Read the FlashlightToggle action from the generated PlayerActions.
        var actions = cowsins.InputManager.inputActions;
        if (actions == null) return;

        var action = actions.GameControls.FlashlightToggle;
        if (action != null && action.WasPressedThisFrame())
        {
            Toggle();
        }
    }

    /// <summary>Toggle the flashlight on/off.</summary>
    public void Toggle()
    {
        if (_light == null) return;
        IsOn = !IsOn;
        _light.enabled = IsOn;
        PlayToggleSFX();
    }

    /// <summary>Force the flashlight on or off.</summary>
    public void SetOn(bool on)
    {
        if (_light == null) return;
        IsOn = on;
        _light.enabled = on;
        PlayToggleSFX();
    }

    private void PlayToggleSFX()
    {
        var clip = IsOn ? toggleOnSFX : toggleOffSFX;
        if (clip != null && cowsins.SoundManager.Instance != null)
        {
            cowsins.SoundManager.Instance.PlaySound(clip, 0, 0, true);
        }
        else if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, sfxVolume);
        }
    }

    private void OnDestroy()
    {
        // Clean up the runtime-created light (don't destroy an assigned one).
        if (_createdRuntime && _light != null)
        {
            Destroy(_light.gameObject);
        }
    }
}
