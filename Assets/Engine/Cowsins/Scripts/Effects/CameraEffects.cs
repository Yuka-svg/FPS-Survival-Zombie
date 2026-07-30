using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class CameraEffects : MonoBehaviour
    {
        [SerializeField, Header("SHARED REFERENCES")] private Transform playerCamera;
        [SerializeField] private Transform camShakeTarget;

        [SerializeField, Header("TILT")] private float tiltSpeed;
        [SerializeField] private float tiltAmount;

        [SerializeField, Tooltip("Maximum Head Bob"), Header("HEAD BOB")] private float headBobAmplitude = 0.2f;
        [SerializeField, Tooltip("Speed to reach the Maximum Head Bob ( headBobAmplitude)")] private float headBobFrequency = 2f;
        [SerializeField, Range(0,1)] private float headBobCrouchMultiplier;
        [SerializeField, Tooltip("Maximum Breathing Amount"), Header("BREATHING EFFECT")] private float breathingAmplitude = 0.2f;
        [SerializeField, Tooltip("Breathing Speed")] private float breathingFrequency = 2f;
        [SerializeField, Tooltip("Enables Rotation for the Breathing Effect")] private bool applyBreathingRotation;

        [SerializeField, Header("LAND CAMERA SHAKE")] private float landShakeIntensity;
        [SerializeField] private float landShakeDuration;

        // Camera Shake
        private float trauma;
        public float Trauma { get { return trauma; } set { trauma = Mathf.Clamp01(value); } }

        private float power = 16;
        private float movementAmount = 0.8f;
        private float rotationAmount = 17f;

        private float traumaDepthMag = 0.6f;
        private float traumaDecay = 1.3f;

        float timeCounter = 0;

        private Coroutine landingShakeRoutine;

        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        private Rigidbody rb;
        private InputManager inputManager;

        private Vector3 origPos;
        private Quaternion origRot;
        private PlayerMovement playerMovementScript;
        // private float currentCrouchTransition = 0f;

        // Effect offsets — applied as absolute offsets from origPos/origRot each frame.
        // This prevents the accumulation bug where world position/rotation was incremented
        // every frame, causing the camera to drift and then snap back when the player stopped.
        private Vector3 headBobPosOffset = Vector3.zero;
        private Quaternion headBobRotOffset = Quaternion.identity;
        private Vector3 breathingPosOffset = Vector3.zero;
        private Quaternion breathingRotOffset = Quaternion.identity;
        private Quaternion tiltRot = Quaternion.identity;
        private Vector3 landingPosOffset = Vector3.zero;

        public void Initialize(PlayerDependencies playerDependencies)
        {
            player = playerDependencies.PlayerMovementState;
            playerControlProvider = playerDependencies.PlayerControl;
            weaponEvents = playerDependencies.WeaponEvents;
            rb = GetComponent<Rigidbody>();
            this.inputManager = playerDependencies.InputManager;
            playerMovementScript = playerDependencies.GetComponent<PlayerMovement>();

            playerDependencies.PlayerMovementEvents.Events.OnLand.AddListener(LandingShake);
            weaponEvents.Events.OnShootShake.AddListener(ShootShake);
        }

        private void OnEnable()
        {
            if (playerCamera == null)
            {
                CowsinsUtilities.LogErrorFormat("No <b><color=cyan>PlayerCamera</color></b> reference found in CameraEffects. " +
                    "Please assign this reference accordingly to fix this error.");
                return;
            }

            if (camShakeTarget == null)
            {
                CowsinsUtilities.LogErrorFormat("No <b><color=cyan>CamShakeTarget</color></b> reference found in CameraEffects. " +
                    "Please assign this reference accordingly to fix this error.");
                return;
            }

            origPos = playerCamera.localPosition;
            origRot = playerCamera.localRotation;
        }
        private void Update()
        {
            if (!playerControlProvider.IsControllable || playerCamera == null || camShakeTarget == null) return;

            UpdateTilt();

            UpdateHeadBob();
            UpdateBreathing();

            // Apply combined position and rotation as absolute offsets from the original
            // transform values. This prevents frame-over-frame accumulation that caused
            // the camera to drift and then snap back when the player stopped moving.
            playerCamera.localPosition = origPos + headBobPosOffset + breathingPosOffset + landingPosOffset;
            playerCamera.localRotation = origRot * tiltRot * headBobRotOffset * breathingRotOffset;

            HandleCamShake();
        }

        private void UpdateTilt()
        {
            // Use actual horizontal velocity to scale tilt intensity, so quick taps
            // (which produce near-zero velocity) don't cause a sudden tilt snap.
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            float velMagnitude = horizontalVel.magnitude;
            float walkSpeed = player.WalkSpeed;
            float tiltScale = walkSpeed > 0f ? Mathf.Clamp01(velMagnitude / walkSpeed) : 0f;

            if (tiltScale < 0.01f)
            {
                // Lerp tilt back to identity when (nearly) stopped so the camera doesn't hold a stale tilt
                tiltRot = Quaternion.Lerp(tiltRot, Quaternion.identity, Time.deltaTime * tiltSpeed);
                return;
            }

            Quaternion rot = CalculateTilt();
            // Scale the tilt by how fast the player is actually moving. This smooths
            // the transition when tapping movement keys (velocity ramps up/down gradually).
            rot = Quaternion.Slerp(Quaternion.identity, rot, tiltScale);
            tiltRot = Quaternion.Lerp(tiltRot, rot, Time.deltaTime * tiltSpeed);
        }

        private Quaternion CalculateTilt()
        {
            float x = inputManager.X;
            float y = inputManager.Y;

            Vector3 vector = new Vector3(y, 0, -x).normalized * tiltAmount;

            return Quaternion.Euler(vector);
        }

        private void UpdateHeadBob()
        {
            if (player.IsIdle || inputManager.Jumping)
            {
                // Smoothly return offsets to zero instead of snapping localPosition to origPos
                headBobPosOffset = Vector3.Lerp(headBobPosOffset, Vector3.zero, Time.deltaTime * 2f);
                headBobRotOffset = Quaternion.Lerp(headBobRotOffset, Quaternion.identity, Time.deltaTime * 2f);
                return;
            }

            float angle = Time.timeSinceLevelLoad * headBobFrequency;
            float amplitude = player.IsCrouching ? headBobAmplitude * headBobCrouchMultiplier : headBobAmplitude;
            float distanceY = amplitude * Mathf.Sin(angle) / 400f;
            float distanceX = amplitude * Mathf.Cos(angle) / 100f;

            // Set absolute offset from original — no accumulation
            headBobPosOffset = new Vector3(0f, distanceY, 0f);
            headBobRotOffset = Quaternion.Euler(distanceX, 0f, 0f);
        }

        private void UpdateBreathing()
        {
            float angle = Time.timeSinceLevelLoad * breathingFrequency;
            float distance = breathingAmplitude * Mathf.Sin(angle) / 400f;
            float distanceRot = breathingAmplitude * Mathf.Cos(angle) / 100f;

            // Set absolute offset from original — no accumulation
            breathingPosOffset = new Vector3(0f, distance, 0f);
            breathingRotOffset = applyBreathingRotation ? Quaternion.Euler(distanceRot, 0f, 0f) : Quaternion.identity;
        }

        #region CameraShake
        private float GetFloat(float seed) { return (Mathf.PerlinNoise(seed, timeCounter) - 0.5f) * 2f; }

        private Vector3 GetVec3() { return new Vector3(GetFloat(1), GetFloat(10), GetFloat(100) * traumaDepthMag); }

        private void HandleCamShake()
        {
            if (Trauma > 0)
            {
                timeCounter += Time.deltaTime * Mathf.Pow(Trauma, 0.3f) * power;

                Vector3 newPos = GetVec3() * movementAmount * Trauma;
                camShakeTarget.localPosition = newPos;

                camShakeTarget.localRotation = Quaternion.Euler(newPos * rotationAmount);

                Trauma -= Time.deltaTime * traumaDecay * (Trauma + 0.3f);
            }
            else
            {
                //lerp back towards default position and rotation once shake is done
                Vector3 newPos = Vector3.Lerp(camShakeTarget.localPosition, Vector3.zero, Time.deltaTime);
                camShakeTarget.localPosition = newPos;
                camShakeTarget.localRotation = Quaternion.Euler(newPos * rotationAmount);
            }
        }

        public void Shake(float amount, float _power, float _movementAmount, float _rotationAmount)
        {
            Trauma = amount;
            power = _power;
            movementAmount = _movementAmount;
            rotationAmount = _rotationAmount;
        }

        public void ShootShake(float amount)
        {
            Trauma += amount;
            power = 20;
            movementAmount = .8f;
            rotationAmount = 17f;
        }

        public void ExplosionShake(float distance)
        {
            Trauma += 10f / distance;
            power = 30;
            movementAmount = 1f;
            rotationAmount = 30f;
        }

        /// <summary>
        /// Triggers a vertical shake to simulate landing impact.
        /// </summary>
        public void LandingShake()
        {
            if (landingShakeRoutine != null) StopCoroutine(landingShakeRoutine);
            landingShakeRoutine = StartCoroutine(LandingShakeRoutine(landShakeIntensity, landShakeDuration));
        }

        private IEnumerator LandingShakeRoutine(float intensity, float duration)
        {
            if (playerCamera == null) yield return null;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = elapsed / duration;

                // Quick "down then up" bounce curve
                float curve = Mathf.Sin(normalized * Mathf.PI);
                // Exponential decay
                float decay = 1f - (normalized * normalized);
                float displacement = curve * decay * -intensity;

                // Store as offset — applied in Update() alongside head bob and breathing
                landingPosOffset = Vector3.up * displacement;

                yield return null;
            }

            // Ensure reset
            landingPosOffset = Vector3.zero;
        }

        #endregion
    }
}
