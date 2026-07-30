using cowsins;
using UnityEngine;

public class FootstepsBehaviour
{
    private MovementContext movementContext;
    private Rigidbody rb => movementContext.Rigidbody;
    private IPlayerMovementStateProvider playerMovement;
    private IPlayerMovementEventsProvider playerEvents;
    private PlayerOrientation orientation => playerMovement?.Orientation;

    private float stepTimer;
    private PlayerMovementSettings playerSettings;
    private AudioSource audioSource;

    public FootstepsBehaviour(MovementContext context)
    {
        playerMovement = context.Dependencies.PlayerMovementState;
        playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerSettings = context.Settings;
        this.audioSource = context.Transform.GetComponent<AudioSource>();

        movementContext = context;
        playerEvents.Events.OnWallRunStart.AddListener(ResetFootsteps);

        // Cache layer indices for better performance
        CacheLayerIndices();
    }

    private void CacheLayerIndices()
    {
        foreach (var entry in playerSettings.footstepSounds.surfaceSounds)
        {
            if (entry.cachedLayerIndex == -1)
                entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);
        }
    }

    public bool CanExecute()
    {
        if (!playerMovement.Grounded && !playerMovement.IsWallRunning || playerMovement.IsIdle)
        {
            stepTimer = 1 - playerSettings.footstepSpeed;
            return false;
        }
        return true;
    }

    public void FootSteps()
    {
        if (!CanExecute()) return;

        stepTimer -= Time.deltaTime * playerMovement.CurrentSpeed / 15;

        if (stepTimer <= 0)
        {
            stepTimer = 1 - playerSettings.footstepSpeed;
            audioSource.pitch = UnityEngine.Random.Range(.7f, 1.3f);

            Vector3 footstepCheckDirection = !playerMovement.IsWallRunning ? Vector3.down :
                (movementContext.WallLeft ? -orientation.Right : orientation.Right) * 2;

            if (Physics.Raycast(movementContext.Transform.position, footstepCheckDirection, out RaycastHit hit, 2.5f, movementContext.WhatIsGround))
            {
                PlayFootstepSound(hit.transform.gameObject.layer);
            }
        }
    }

    private void PlayFootstepSound(int layer)
    {
        AudioClip[] sounds = playerSettings.footstepSounds.GetSoundsForLayer(layer);

        if (sounds.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, sounds.Length);
            audioSource.PlayOneShot(sounds[randomIndex], playerSettings.footstepVolume);
        }
    }

    public void ResetFootsteps()
    {
        stepTimer = 0;
    }
}