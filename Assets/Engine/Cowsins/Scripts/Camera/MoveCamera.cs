namespace cowsins
{
    using UnityEngine;

    /// <summary>
    /// Keep camera in place with premium AAA-grade step-climbing Y-offset compensation.
    /// Decouples visual camera transitions from sudden vertical physics teleports.
    /// </summary>
    public class MoveCamera : MonoBehaviour
    {
        [Tooltip("Reference to our Camera Head Transform, that defines the placement of our Camera"), SerializeField] private Transform cameraHead;

        [Tooltip("Smoothing time for vertical camera movement on stairs/rough terrain. Lower = faster, higher = smoother."), SerializeField, Min(0.01f)]
        private float verticalSmoothTime = 0.05f;

        private PlayerMovement playerMovement;
        private float verticalVelocity = 0f;

        private void Awake()
        {
            if (cameraHead != null)
            {
                playerMovement = cameraHead.GetComponentInParent<PlayerMovement>();
            }
        }

        private void Start()
        {
            if (playerMovement == null && cameraHead != null)
            {
                playerMovement = cameraHead.GetComponentInParent<PlayerMovement>();
            }
        }

        private void LateUpdate()
        {
            if (cameraHead == null) return;

            if (playerMovement == null)
            {
                playerMovement = cameraHead.GetComponentInParent<PlayerMovement>();
            }

            Vector3 targetPos = cameraHead.position;
            float newY;

            // Instantly snap the camera Y if the player is airborne, climbing, sliding, crouching, dashing, or wallrunning
            if (playerMovement == null || 
                !playerMovement.Grounded || 
                playerMovement.IsClimbing || 
                playerMovement.IsSliding || 
                playerMovement.IsCrouching || 
                playerMovement.IsDashing || 
                playerMovement.IsWallRunning)
            {
                newY = targetPos.y;
                verticalVelocity = 0f;
            }
            else
            {
                // Smoothly damp the vertical position towards the player's head height
                newY = Mathf.SmoothDamp(transform.position.y, targetPos.y, ref verticalVelocity, verticalSmoothTime);
            }

            // Keep horizontal tracking instant to maintain mouse-look responsiveness
            transform.position = new Vector3(targetPos.x, newY, targetPos.z);
        }
    }
}
