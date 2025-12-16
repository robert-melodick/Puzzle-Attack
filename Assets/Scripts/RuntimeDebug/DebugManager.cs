using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace PuzzleAttack.RuntimeDebug
{
    /// <summary>
    /// Central coordinator for all debug systems.
    /// Handles hotkey input and manages console + Unity UI panel visibility.
    /// </summary>
    public class DebugManager : MonoBehaviour
    {
        private static DebugManager _instance;
        public static DebugManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("DebugManager");
                    _instance = go.AddComponent<DebugManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Components")]
        public DebugConsole console;
        public UnityUIDebugPanel uiPanel;
        public GridDebugAPI gridDebugAPI;

        [Header("Settings")]
        public KeyCode consoleToggleKey = KeyCode.BackQuote; // Tilde (~)
        public KeyCode uiPanelToggleKey = KeyCode.F1;

        private bool _consoleVisible = false;
        private bool _uiPanelVisible = false;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize components
            if (console == null)
                console = gameObject.AddComponent<DebugConsole>();

            if (uiPanel == null)
                uiPanel = gameObject.AddComponent<UnityUIDebugPanel>();

            if (gridDebugAPI == null)
                gridDebugAPI = gameObject.AddComponent<GridDebugAPI>();

            // Initialize command registry
            DebugCommandRegistry.Initialize();

            UnityEngine.Debug.Log("[DebugManager] Initialized - Press ~ for console, F1 for UI panel");
        }

        void Update()
        {
            // Toggle console
            if (Input.GetKeyDown(consoleToggleKey))
            {
                _consoleVisible = !_consoleVisible;
                console.SetVisible(_consoleVisible);
            }

            // Toggle UI panel
            if (Input.GetKeyDown(uiPanelToggleKey))
            {
                _uiPanelVisible = !_uiPanelVisible;
                uiPanel.SetVisible(_uiPanelVisible);
            }
        }

        /// <summary>
        /// Execute a console command from anywhere (useful for UI buttons)
        /// </summary>
        public void ExecuteCommand(string command)
        {
            if (console != null)
            {
                console.ExecuteCommand(command);
            }
        }

        /// <summary>
        /// Add a log message to the console
        /// </summary>
        public void Log(string message, LogType logType = LogType.Log)
        {
            if (console != null)
            {
                console.AddLog(message, logType);
            }
        }
    }
}

#endif