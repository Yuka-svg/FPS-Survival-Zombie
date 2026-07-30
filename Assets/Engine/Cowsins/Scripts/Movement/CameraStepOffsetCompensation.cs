/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// Smoothly offsets the camera position to compensate for sudden vertical position changes 
    /// caused by step climbing or snapping to the ground.
    /// </summary>
    public class CameraStepOffsetCompensation : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The PlayerMovement component to listen to for step climb events.")]
        [SerializeField] private PlayerMovement playerMovement;

        [Tooltip("The transform to apply the offset to. If null, applies to this transform.")]
        [SerializeField] private Transform targetTransform;

        [Header("Settings")]
        [Tooltip("How fast the offset decays back to zero. Higher values mean faster decay.")]
        [SerializeField] private float smoothSpeed = 10f;

        [Tooltip("Enable vertical offset compensation.")]
        [SerializeField] private bool enableCompensation = true;

        private float currentOffset = 0f;
        private Vector3 originalLocalPos;

        private void Awake()
        {
            if (targetTransform == null)
            {
                targetTransform = transform;
            }
            originalLocalPos = targetTransform.localPosition;

            if (playerMovement == null)
            {
                playerMovement = GetComponentInParent<PlayerMovement>();
            }
        }

        private void OnEnable()
        {
            if (playerMovement != null)
            {
                playerMovement.OnStepClimb.AddListener(OnStepClimb);
            }
            else
            {
                // Try finding it if not assigned yet
                playerMovement = GetComponentInParent<PlayerMovement>();
                if (playerMovement != null)
                {
                    playerMovement.OnStepClimb.AddListener(OnStepClimb);
                }
            }
        }

        private void OnDisable()
        {
            if (playerMovement != null)
            {
                playerMovement.OnStepClimb.RemoveListener(OnStepClimb);
            }
        }

        private void LateUpdate()
        {
            if (targetTransform == null) return;

            // Decays the offset towards 0 using frame-rate independent exponential decay
            if (Mathf.Abs(currentOffset) > 0.0001f)
            {
                currentOffset = currentOffset * Mathf.Exp(-smoothSpeed * Time.deltaTime);
            }
            else
            {
                currentOffset = 0f;
            }

            // Apply the offset to the local position's Y axis
            targetTransform.localPosition = new Vector3(
                originalLocalPos.x,
                originalLocalPos.y + currentOffset,
                originalLocalPos.z
            );
        }

        private void OnStepClimb(float heightDelta)
        {
            if (!enableCompensation) return;

            // When the player steps up (positive heightDelta), the body is instantly teleported up.
            // To compensate and keep the camera at the same world height, we offset the camera downwards.
            // When the player steps down/snaps (negative heightDelta), the body is instantly teleported down.
            // We offset the camera upwards to compensate.
            currentOffset -= heightDelta;
            currentOffset = Mathf.Clamp(currentOffset, -1.2f, 1.2f);
        }
    }
}
