using UnityEngine;

/// <summary>
/// Drives a cinematic, hands-off menu camera: a slow orbit around a focus point
/// with a gentle vertical bob and look sway for a "living, handheld" feel.
/// Uses unscaled time so it keeps moving regardless of Time.timeScale.
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class MenuCameraOrbit : MonoBehaviour
{
    [Header("Focus")]
    public Vector3 focusPoint = new Vector3(0f, 18f, 0f);
    public float radius = 130f;
    public float height = 55f;

    [Header("Orbit")]
    public float orbitSpeed = 3.5f;   // degrees / second
    public float startAngle = 30f;

    [Header("Life")]
    public float bobAmplitude = 2.5f;
    public float bobSpeed = 0.35f;     // cycles / second
    [Range(0f, 6f)] public float lookSwayDeg = 1.1f;
    public float swaySpeed = 0.28f;

    private float _angle;

    private void OnEnable()
    {
        _angle = startAngle;
        Apply();
    }

    private void LateUpdate()
    {
        _angle += orbitSpeed * Time.unscaledDeltaTime;
        Apply();
    }

    private void Apply()
    {
        float rad = _angle * Mathf.Deg2Rad;
        float t = Time.unscaledTime;
        float bob = Mathf.Sin(t * bobSpeed * Mathf.PI * 2f) * bobAmplitude;

        Vector3 pos = focusPoint + new Vector3(
            Mathf.Sin(rad) * radius,
            height + bob,
            Mathf.Cos(rad) * radius);
        transform.position = pos;

        Vector3 dir = (focusPoint - pos).normalized;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);

        Vector3 sway = new Vector3(
            Mathf.Sin(t * swaySpeed * 1.3f) * lookSwayDeg,
            Mathf.Sin(t * swaySpeed) * lookSwayDeg,
            0f);
        transform.rotation = look * Quaternion.Euler(sway);
    }
}
