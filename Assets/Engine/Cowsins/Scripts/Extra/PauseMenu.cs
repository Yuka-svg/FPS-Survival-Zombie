using System;
using UnityEngine;

namespace cowsins
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private PlayerDependencies playerDependencies;
        private IPlayerControlProvider playerControlProvider;
        private IPlayerStatsProvider playerStatsProvider;
        private InputManager inputManager;

        public static PauseMenu Instance { get; private set; }
        public static bool isPaused { get; set; }

        public event Action OnPause;
        public event Action OnUnPause;

        private void Start()
        {
            playerControlProvider = playerDependencies.PlayerControl;
            playerStatsProvider = playerDependencies.PlayerStats;
            inputManager = playerDependencies.InputManager;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            isPaused = false;

            inputManager.OnTogglePause += TogglePause;
        }

        private void OnDisable()
        {
            if (inputManager != null)
                inputManager.OnTogglePause -= TogglePause;
        }

        public void TogglePause()
        {
            isPaused = !isPaused;

            if (isPaused)
            {
                playerControlProvider.LoseControl();
                OnPause?.Invoke();
            }
            else
                UnPause();
        }

        public void UnPause()
        {
            isPaused = false;
            playerControlProvider.CheckIfCanGrantControl();
            OnUnPause?.Invoke();
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
