using System;
using UnityEngine;

namespace PlayerSystem.Test
{
    public class TeamModeManager : MonoBehaviour
    {
        private static TeamModeManager s_instance;

        public static TeamModeManager Instance => s_instance;

        public event Action OnSwitchRequested;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
                OnSwitchRequested?.Invoke();
        }
    }
}
