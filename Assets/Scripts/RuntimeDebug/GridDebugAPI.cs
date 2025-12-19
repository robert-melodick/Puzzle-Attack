using UnityEngine;
using System.Collections.Generic;
using PuzzleAttack.Grid;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace PuzzleAttack.RuntimeDebug
{
    /// <summary>
    /// Clean API for debug commands to manipulate the grid.
    /// All methods are decorated with [DebugCommand] for automatic console registration.
    /// Both console and ImGui call these same methods - single source of truth.
    /// </summary>
    public class GridDebugAPI : MonoBehaviour
    {
        private GridManager _gridManager;
        private TileSpawner _tileSpawner;
        private GridRiser _gridRiser;
        private MatchDetector _matchDetector;
        private MatchProcessor _matchProcessor;

        // Tile type name to sprite index mapping
        private Dictionary<string, int> _tileTypeMap = new Dictionary<string, int>()
        {
            { "red", 0 },
            { "blue", 1 },
            { "green", 2 },
            { "yellow", 3 },
            { "purple", 4 },
            { "orange", 5 }
        };

        void Awake()
        {
            // Find grid components - we'll do this lazily on first command if not found
        }

        void Start()
        {
            FindGridComponents();
        }

        private void FindGridComponents()
        {
            if (_gridManager != null) return;

            _gridManager = FindFirstObjectByType<GridManager>();
            if (_gridManager != null)
            {
                _tileSpawner = _gridManager.tileSpawner;
                _gridRiser = _gridManager.gridRiser;
                _matchDetector = _gridManager.matchDetector;
                _matchProcessor = _gridManager.matchProcessor;
            }
        }

        private bool ValidateGridComponents()
        {
            FindGridComponents();
            
            if (_gridManager == null)
            {
                DebugManager.Instance.Log("Grid system not found! Make sure you're in a gameplay scene.", LogType.Error);
                return false;
            }
            return true;
        }

        #region Spawn Commands

        [DebugCommand("spawn", "Spawn a tile at the specified position", "/spawn <type> <x> <y>")]
        public void SpawnTile(string tileType, int x, int y)
        {
            if (!ValidateGridComponents()) return;

            // Validate tile type
            if (!_tileTypeMap.TryGetValue(tileType.ToLower(), out int typeIndex))
            {
                DebugManager.Instance.Log($"Unknown tile type: {tileType}. Valid types: {string.Join(", ", _tileTypeMap.Keys)}", LogType.Error);
                return;
            }

            // Validate coordinates
            if (x < 0 || x >= _gridManager.gridWidth || y < 0 || y >= _gridManager.gridHeight)
            {
                DebugManager.Instance.Log($"Invalid coordinates ({x}, {y}). Grid size: {_gridManager.gridWidth}x{_gridManager.gridHeight}", LogType.Error);
                return;
            }

            var grid = _gridManager.GetGrid();

            // Clear existing tile if present
            var existingTile = grid[x, y];
            if (existingTile != null)
            {
                Destroy(existingTile);
                grid[x, y] = null;
            }

            // Spawn new tile
            _tileSpawner.SpawnTile(x, y, _gridRiser.CurrentGridOffset);
            
            // Override the random type with our specified type
            var newTile = grid[x, y];
            if (newTile != null)
            {
                SpriteRenderer sr = newTile.GetComponent<SpriteRenderer>();
                sr.sprite = _tileSpawner.tileSprites[typeIndex];
                
                Tile tileScript = newTile.GetComponent<Tile>();
                tileScript.Initialize(x, y, typeIndex, _gridManager);
            }

            DebugManager.Instance.Log($"Spawned {tileType} tile at ({x}, {y})");
        }

        [DebugCommand("fill", "Fill an area with random tiles", "/fill <x1> <y1> <x2> <y2>")]
        public void SpawnRandomArea(int x1, int y1, int x2, int y2)
        {
            if (!ValidateGridComponents()) return;

            int minX = Mathf.Clamp(Mathf.Min(x1, x2), 0, _gridManager.gridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.Max(x1, x2), 0, _gridManager.gridWidth - 1);
            int minY = Mathf.Clamp(Mathf.Min(y1, y2), 0, _gridManager.gridHeight - 1);
            int maxY = Mathf.Clamp(Mathf.Max(y1, y2), 0, _gridManager.gridHeight - 1);

            var grid = _gridManager.GetGrid();
            int count = 0;
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var existingTile = grid[x, y];
                    if (existingTile != null)
                    {
                        Destroy(existingTile);
                        grid[x, y] = null;
                    }
                    
                    _tileSpawner.SpawnTile(x, y, _gridRiser.CurrentGridOffset);
                    count++;
                }
            }

            DebugManager.Instance.Log($"Spawned {count} random tiles in area ({minX},{minY}) to ({maxX},{maxY})");
        }

        #endregion

        #region Clear Commands

        [DebugCommand("clear", "Clear a single tile or area", "/clear <x> <y> [x2] [y2]")]
        public void ClearTile(int x, int y)
        {
            if (!ValidateGridComponents()) return;

            if (x < 0 || x >= _gridManager.gridWidth || y < 0 || y >= _gridManager.gridHeight)
            {
                DebugManager.Instance.Log($"Invalid coordinates ({x}, {y})", LogType.Error);
                return;
            }

            var grid = _gridManager.GetGrid();
            var tile = grid[x, y];
            if (tile != null)
            {
                grid[x, y] = null;
                Destroy(tile);
                DebugManager.Instance.Log($"Cleared tile at ({x}, {y})");
            }
            else
            {
                DebugManager.Instance.Log($"No tile at ({x}, {y})");
            }
        }

        [DebugCommand("cleararea", "Clear a rectangular area", "/cleararea <x1> <y1> <x2> <y2>")]
        public void ClearArea(int x1, int y1, int x2, int y2)
        {
            if (!ValidateGridComponents()) return;

            int minX = Mathf.Clamp(Mathf.Min(x1, x2), 0, _gridManager.gridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.Max(x1, x2), 0, _gridManager.gridWidth - 1);
            int minY = Mathf.Clamp(Mathf.Min(y1, y2), 0, _gridManager.gridHeight - 1);
            int maxY = Mathf.Clamp(Mathf.Max(y1, y2), 0, _gridManager.gridHeight - 1);

            var grid = _gridManager.GetGrid();
            int count = 0;
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var tile = grid[x, y];
                    if (tile != null)
                    {
                        grid[x, y] = null;
                        Destroy(tile);
                        count++;
                    }
                }
            }

            DebugManager.Instance.Log($"Cleared {count} tiles in area ({minX},{minY}) to ({maxX},{maxY})");
        }

        [DebugCommand("matches", "Clear all current matches on the grid")]
        public void ClearMatches()
        {
            if (!ValidateGridComponents()) return;

            var matches = _matchDetector.GetAllMatches();
            if (matches.Count > 0)
            {
                StartCoroutine(_matchProcessor.ProcessMatches(matches));
                DebugManager.Instance.Log($"Clearing {matches.Count} matched tiles");
            }
            else
            {
                DebugManager.Instance.Log("No matches found");
            }
        }

        [DebugCommand("clearall", "Destroy all tiles on the grid")]
        public void ClearAll()
        {
            if (!ValidateGridComponents()) return;

            int count = 0;
            var grid = _gridManager.GetGrid();
            
            for (int x = 0; x < _gridManager.gridWidth; x++)
            {
                for (int y = 0; y < _gridManager.gridHeight; y++)
                {
                    if (grid[x, y] != null)
                    {
                        Destroy(grid[x, y]);
                        grid[x, y] = null;
                        count++;
                    }
                }
            }

            DebugManager.Instance.Log($"Cleared all {count} tiles from grid");
        }

        #endregion

        #region Grid Control Commands

        [DebugCommand("speed", "Set the base grid rise speed", "/speed <speed>")]
        public void SetBaseRiseSpeed(float speed)
        {
            if (!ValidateGridComponents()) return;

            _gridRiser.baseRiseSpeed = Mathf.Clamp(speed, 0f, 10f);
            DebugManager.Instance.Log($"Base rise speed set to {_gridRiser.baseRiseSpeed:F2}");
        }

        [DebugCommand("level", "Set the speed level (1-99)", "/level <level>")]
        public void SetSpeedLevel(int level)
        {
            if (!ValidateGridComponents()) return;

            _gridRiser.speedLevel = Mathf.Clamp(level, 1, _gridRiser.maxSpeedLevel);
            DebugManager.Instance.Log($"Speed level set to {_gridRiser.speedLevel}");
        }

        [DebugCommand("multiplier", "Set the fast rise multiplier", "/multiplier <multiplier>")]
        public void SetFastRiseMultiplier(float multiplier)
        {
            if (!ValidateGridComponents()) return;

            _gridRiser.fastRiseMultiplier = Mathf.Clamp(multiplier, 1f, 10f);
            DebugManager.Instance.Log($"Fast rise multiplier set to {_gridRiser.fastRiseMultiplier:F1}x");
        }

        [DebugCommand("offset", "Get current grid offset", "/offset")]
        public void GetGridOffset()
        {
            if (!ValidateGridComponents()) return;

            DebugManager.Instance.Log($"Current grid offset: {_gridRiser.CurrentGridOffset:F2}");
        }

        [DebugCommand("drop", "Force all tiles to drop/fall")]
        public void ForceDrop()
        {
            if (!ValidateGridComponents()) return;

            StartCoroutine(_gridManager.DropTiles());
            DebugManager.Instance.Log("Forcing tile drop");
        }

        [DebugCommand("breathing", "Toggle breathing room system", "/breathing")]
        public void ToggleBreathingRoom()
        {
            if (!ValidateGridComponents()) return;

            _gridRiser.enableBreathingRoom = !_gridRiser.enableBreathingRoom;
            DebugManager.Instance.Log($"Breathing room: {(_gridRiser.enableBreathingRoom ? "ENABLED" : "DISABLED")}");
        }

        [DebugCommand("breathingadd", "Add breathing room time", "/breathingadd <seconds>")]
        public void AddBreathingRoom(float seconds)
        {
            if (!ValidateGridComponents()) return;

            int fakeTiles = Mathf.RoundToInt(seconds / _gridRiser.breathingRoomPerTile);
            _gridRiser.AddBreathingRoom(fakeTiles);
            DebugManager.Instance.Log($"Added {seconds:F1}s breathing room");
        }

        #endregion

        #region Utility Commands

        [DebugCommand("help", "List all available commands")]
        private void Help()
        {
            // List all commands
            var commandList = new System.Text.StringBuilder();
            commandList.AppendLine("<color=yellow>Available Commands:</color>");
            commandList.AppendLine("");
            
            foreach (var cmd in DebugCommandRegistry.Commands.Values)
            {
                commandList.AppendLine($"  <color=cyan>/{cmd.Name}</color> - {cmd.Description}");
            }
            
            commandList.AppendLine("");
            commandList.AppendLine("Type <color=cyan>/helpcommand <name></color> for detailed usage of a specific command");
            DebugManager.Instance.Log(commandList.ToString());
        }

        [DebugCommand("helpcommand", "Get detailed help for a specific command", "/helpcommand <command_name>")]
        public void HelpCommand(string commandName)
        {
            // Show help for specific command
            if (DebugCommandRegistry.Commands.TryGetValue(commandName, out var cmd))
            {
                DebugManager.Instance.Log(cmd.GetHelpText());
            }
            else
            {
                DebugManager.Instance.Log($"Unknown command: {commandName}", LogType.Error);
            }
        }

        [DebugCommand("commands", "Alias for help - list all available commands")]
        public void Commands()
        {
            Help();
        }

        [DebugCommand("?", "Alias for help - list all available commands")]
        public void QuestionMark()
        {
            Help();
        }

        [DebugCommand("info", "Display current grid state information")]
        public void GridInfo()
        {
            if (!ValidateGridComponents()) return;

            var info = new System.Text.StringBuilder();
            info.AppendLine("<color=yellow>Grid Information:</color>");
            info.AppendLine($"  Size: {_gridManager.gridWidth}x{_gridManager.gridHeight}");
            info.AppendLine($"  Tile Size: {_gridManager.tileSize}");
            info.AppendLine($"  Current Offset: {_gridRiser.CurrentGridOffset:F2}");
            info.AppendLine($"  Speed Level: {_gridRiser.speedLevel} / {_gridRiser.maxSpeedLevel}");
            info.AppendLine($"  Base Rise Speed: {_gridRiser.baseRiseSpeed:F3}");
            info.AppendLine($"  Fast Rise Multiplier: {_gridRiser.fastRiseMultiplier:F1}x");
            info.AppendLine($"  Breathing Room: {(_gridRiser.enableBreathingRoom ? "Enabled" : "Disabled")}");
            info.AppendLine($"  Is Swapping: {_gridManager.IsSwapping}");
            info.AppendLine($"  Processing Matches: {_matchProcessor.IsProcessingMatches}");

            // Count tiles
            int tileCount = 0;
            var grid = _gridManager.GetGrid();
            for (int x = 0; x < _gridManager.gridWidth; x++)
            {
                for (int y = 0; y < _gridManager.gridHeight; y++)
                {
                    if (grid[x, y] != null) tileCount++;
                }
            }
            info.AppendLine($"  Active Tiles: {tileCount}");

            DebugManager.Instance.Log(info.ToString());
        }

        #endregion

        #region Public Getters for ImGui

        public GridManager GetGridManager() => _gridManager;
        public string[] GetTileTypeNames() => new List<string>(_tileTypeMap.Keys).ToArray();
        public int GetTileTypeIndex(string name) => _tileTypeMap.TryGetValue(name.ToLower(), out int idx) ? idx : 0;
        
        public Sprite GetTileSprite(int index)
        {
            FindGridComponents();
            if (_tileSpawner != null && index >= 0 && index < _tileSpawner.tileSprites.Length)
            {
                return _tileSpawner.tileSprites[index];
            }
            return null;
        }
        
        public Sprite GetTileSprite(string name)
        {
            int index = GetTileTypeIndex(name);
            return GetTileSprite(index);
        }

        #endregion
    }
}

#endif