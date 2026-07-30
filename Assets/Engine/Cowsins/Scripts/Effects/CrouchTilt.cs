/// <summary>
/// This script belongs to cowsinsT as a part of the cowsins' FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class CrouchTilt : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies playerDependencies;

        [Tooltip("Rotation desired when crouching"), SerializeField] private Vector3 tiltRot, tiltPosOffset;
        [Tooltip("Tilting / Rotation velocity"), SerializeField] private float tiltSpeed;

        private IWeaponBehaviourProvider wp; // IWeaponBehaviourProvider is implemented in WeaponController.cs

        private Quaternion origRot;
        private Vector3 origPos;
        private Coroutine tiltCoroutine;

        void Start()
        {
            wp = playerDependencies.WeaponBehaviour;

            origRot = transform.localRotation;
            origPos = transform.localPosition;

            playerDependencies.PlayerMovementEvents.Events.OnCrouchStart.AddListener(StartCrouch);
            playerDependencies.PlayerMovementEvents.Events.OnCrouchStop.AddListener(StopCrouch);

            playerDependencies.WeaponEvents.Events.OnAimStart.AddListener(HandleAimStart);
            playerDependencies.WeaponEvents.Events.OnAimStop.AddListener(HandleAimStop);
        }

        private void OnDestroy()
        {
            if (playerDependencies != null)
            {
                if (playerDependencies.PlayerMovementEvents != null && playerDependencies.PlayerMovementEvents.Events != null)
                {
                    playerDependencies.PlayerMovementEvents.Events.OnCrouchStart.RemoveListener(StartCrouch);
                    playerDependencies.PlayerMovementEvents.Events.OnCrouchStop.RemoveListener(StopCrouch);
                }
                if (playerDependencies.WeaponEvents != null && playerDependencies.WeaponEvents.Events != null)
                {
                    playerDependencies.WeaponEvents.Events.OnAimStart.RemoveListener(HandleAimStart);
                    playerDependencies.WeaponEvents.Events.OnAimStop.RemoveListener(HandleAimStop);
                }
            }
        }

        private void StartCrouch()
        {
            if (!wp.IsAiming) StartTilting(tiltRot, origPos + tiltPosOffset);
        }

        private void StopCrouch()
        {
            StartTilting(origRot.eulerAngles, origPos);
        }

        private void HandleAimStart(float aimDuration)
        {
            if (playerDependencies.PlayerMovementState.IsCrouching)
            {
                StartTilting(origRot.eulerAngles, origPos);
            }
        }

        private void HandleAimStop()
        {
            if (playerDependencies.PlayerMovementState.IsCrouching)
            {
                StartTilting(tiltRot, origPos + tiltPosOffset);
            }
        }

        private void StartTilting(Vector3 targetRotation, Vector3 targetPosition)
        {
            if (tiltCoroutine != null) StopCoroutine(tiltCoroutine);
            tiltCoroutine = StartCoroutine(TiltRoutine(targetRotation, targetPosition));
        }

        private IEnumerator TiltRoutine(Vector3 targetRotation, Vector3 targetPosition)
        {
            Quaternion targetRotQuat = Quaternion.Euler(targetRotation);
            while (Quaternion.Angle(transform.localRotation, targetRotQuat) > 0.1f ||
                   Vector3.Distance(transform.localPosition, targetPosition) > 0.01f)
            {
                float lerpFactor = 1f - Mathf.Exp(-tiltSpeed * Time.deltaTime);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotQuat, lerpFactor);
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, lerpFactor);
                yield return null;  // Wait for the next frame before continuing
            }
            transform.localRotation = targetRotQuat;
            transform.localPosition = targetPosition;
        }
    }
}
