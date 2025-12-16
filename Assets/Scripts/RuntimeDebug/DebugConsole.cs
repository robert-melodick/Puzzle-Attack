using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace PuzzleAttack.RuntimeDebug
{
    /// <summary>
    /// Console UI and command parsing system.
    /// Handles input, command history, autocomplete, and output display.
    /// </summary>
    public class DebugConsole : MonoBehaviour
    {
        [Header("UI Settings")]
        public int maxLogEntries = 100;
        public int consoleHeight = 300;
        public int inputHeight = 30;
        public int fontSize = 14;

        private bool _isVisible = false;
        private string _currentInput = "";
        private List<LogEntry> _logEntries = new List<LogEntry>();
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;
        private Vector2 _scrollPosition;
        private string _autocompletePreview = "";

        private GUIStyle _consoleStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _logStyle;
        private bool _stylesInitialized = false;

        private struct LogEntry
        {
            public string message;
            public LogType type;
            public Color color;

            public LogEntry(string msg, LogType t)
            {
                message = msg;
                type = t;
                color = GetColorForLogType(t);
            }

            private static Color GetColorForLogType(LogType type)
            {
                switch (type)
                {
                    case LogType.Error: return new Color(1f, 0.3f, 0.3f);
                    case LogType.Warning: return new Color(1f, 0.8f, 0f);
                    default: return Color.white;
                }
            }
        }

        void Start()
        {
            // Welcome message
            AddLog("Debug Console initialized. Type /help for commands.", LogType.Log);
        }

        void Update()
        {
            if (!_isVisible) return;

            // Handle command history navigation
            if (Event.current != null && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    NavigateHistory(-1);
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    NavigateHistory(1);
                }
                else if (Event.current.keyCode == KeyCode.Tab)
                {
                    TryAutocomplete();
                    Event.current.Use();
                }
            }
        }

        void OnGUI()
        {
            if (!_isVisible) return;

            InitializeStyles();

            // Console background
            GUI.Box(new Rect(0, 0, Screen.width, consoleHeight), "", _consoleStyle);

            // Log area
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, consoleHeight - inputHeight - 30));
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(consoleHeight - inputHeight - 40));

            foreach (var entry in _logEntries)
            {
                GUIStyle coloredStyle = new GUIStyle(_logStyle);
                coloredStyle.normal.textColor = entry.color;
                GUILayout.Label(entry.message, coloredStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // Input area
            float inputY = consoleHeight - inputHeight - 10;
            
            GUI.SetNextControlName("ConsoleInput");
            _currentInput = GUI.TextField(
                new Rect(10, inputY, Screen.width - 20, inputHeight),
                _currentInput,
                _inputStyle
            );

            // Auto-focus input field
            GUI.FocusControl("ConsoleInput");

            // Show autocomplete preview
            if (!string.IsNullOrEmpty(_autocompletePreview))
            {
                GUI.Label(
                    new Rect(15, inputY + 5, Screen.width - 30, inputHeight),
                    _autocompletePreview,
                    new GUIStyle(_inputStyle) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f) } }
                );
            }

            // Handle input submission
            if (Event.current != null && Event.current.isKey)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    if (!string.IsNullOrWhiteSpace(_currentInput))
                    {
                        ExecuteCommand(_currentInput);
                        _currentInput = "";
                        _autocompletePreview = "";
                        Event.current.Use();
                    }
                }
            }

            // Update autocomplete preview as user types
            UpdateAutocompletePreview();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _consoleStyle = new GUIStyle(GUI.skin.box);
            _consoleStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.9f));

            _inputStyle = new GUIStyle(GUI.skin.textField);
            _inputStyle.fontSize = fontSize;
            _inputStyle.normal.textColor = Color.white;
            _inputStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.9f));
            _inputStyle.padding = new RectOffset(5, 5, 5, 5);

            _logStyle = new GUIStyle(GUI.skin.label);
            _logStyle.fontSize = fontSize;
            _logStyle.richText = true;
            _logStyle.wordWrap = true;

            _stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (visible)
            {
                _currentInput = "";
                _autocompletePreview = "";
                _historyIndex = -1;
            }
        }

        public void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            // Add to log
            AddLog($"> {command}", LogType.Log);

            // Add to history
            _commandHistory.Insert(0, command);
            if (_commandHistory.Count > 50)
            {
                _commandHistory.RemoveAt(_commandHistory.Count - 1);
            }
            _historyIndex = -1;

            // Parse command
            command = command.Trim();
            
            // Remove leading slash if present
            if (command.StartsWith("/"))
            {
                command = command.Substring(1);
            }

            // Split into command and arguments
            var parts = ParseCommandLine(command);
            if (parts.Length == 0) return;

            string commandName = parts[0];
            string[] args = new string[parts.Length - 1];
            System.Array.Copy(parts, 1, args, 0, args.Length);

            // Execute via registry
            if (DebugCommandRegistry.TryExecuteCommand(commandName, args, out string result))
            {
                if (!string.IsNullOrEmpty(result))
                {
                    AddLog(result, LogType.Log);
                }
            }
            else
            {
                AddLog(result, LogType.Error);
            }

            // Auto-scroll to bottom
            _scrollPosition = new Vector2(0, float.MaxValue);
        }

        /// <summary>
        /// Parse command line, respecting quoted strings
        /// </summary>
        private string[] ParseCommandLine(string commandLine)
        {
            List<string> parts = new List<string>();
            bool inQuotes = false;
            string current = "";

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        parts.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (!string.IsNullOrEmpty(current))
            {
                parts.Add(current);
            }

            return parts.ToArray();
        }

        public void AddLog(string message, LogType logType = LogType.Log)
        {
            _logEntries.Add(new LogEntry(message, logType));

            // Limit log size
            if (_logEntries.Count > maxLogEntries)
            {
                _logEntries.RemoveAt(0);
            }

            // Auto-scroll to bottom
            _scrollPosition = new Vector2(0, float.MaxValue);
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0) return;

            _historyIndex += direction;
            _historyIndex = Mathf.Clamp(_historyIndex, -1, _commandHistory.Count - 1);

            if (_historyIndex >= 0)
            {
                _currentInput = _commandHistory[_historyIndex];
            }
            else
            {
                _currentInput = "";
            }

            _autocompletePreview = "";
        }

        private void UpdateAutocompletePreview()
        {
            if (string.IsNullOrEmpty(_currentInput))
            {
                _autocompletePreview = "";
                return;
            }

            string input = _currentInput.TrimStart('/');
            var matches = DebugCommandRegistry.GetCommandsStartingWith(input);

            if (matches.Count == 1 && matches[0] != input)
            {
                _autocompletePreview = "/" + matches[0];
            }
            else
            {
                _autocompletePreview = "";
            }
        }

        private void TryAutocomplete()
        {
            if (string.IsNullOrEmpty(_currentInput)) return;

            string input = _currentInput.TrimStart('/');
            var matches = DebugCommandRegistry.GetCommandsStartingWith(input);

            if (matches.Count == 1)
            {
                _currentInput = "/" + matches[0] + " ";
                _autocompletePreview = "";
            }
            else if (matches.Count > 1)
            {
                // Show all matches
                AddLog($"Matches: {string.Join(", ", matches.Select(m => "/" + m))}", LogType.Log);
            }
        }
    }
}

#endif