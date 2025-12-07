using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class CursorController : MonoBehaviour, ICursorCommands
    {
        [Header("Cursor Settings")] public int cursorWidth = 2;
        public int cursorHeight = 1;
        public Color cursorColor = new(1f, 1f, 1f, 0.5f);
        public GameObject cursorPrefab;

        [Header("Block Slip Indicator")] public Color blockSlipColor = new(0f, 1f, 0.5f, 0.7f); // Green/cyan glow

        [SerializeField] private GridManager gridManager;
        [SerializeField] private GridRiser gridRiser;
        private GameObject _blockSlipIndicator;

        private Vector2Int _cursorPosition;
        private GameObject _cursorVisual;
        private bool _blockSlipActive;
        private float _currentGridOffset;
        private int _gridHeight;
        private int _gridWidth;

        private float _tileSize;
        private TileSpawner _tileSpawner;
        private bool _usingPrefabCursor;

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public Vector2Int CursorPosition => _cursorPosition;

        // Movement commands
        public void MoveLeft()
        {
            MoveCursor(-1, 0);
        }

        public void MoveRight()
        {
            MoveCursor(1, 0);
        }

        public void MoveUp()
        {
            MoveCursor(0, 1);
        }

        public void MoveDown()
        {
            MoveCursor(0, -1);
        }

        public void Swap()
        {
            if (gridManager != null) gridManager.RequestSwapAtCursor();
        }

        public void FastRiseGrid()
        {
            if (gridManager != null) gridRiser.RequestFastRise();
        }

        public void Initialize(GridManager manager, float tileSize, int gridWidth, int gridHeight,
            TileSpawner spawner = null, GridRiser riser = null)
        {
            gridManager = manager;
            this._tileSize = tileSize;
            this._gridWidth = gridWidth;
            this._gridHeight = gridHeight;
            _tileSpawner = spawner;
            if (riser != null) gridRiser = riser;

            _cursorPosition = new Vector2Int(0, gridHeight / 2);
            GridX = _cursorPosition.x;
            GridY = _cursorPosition.y;

            CreateCursor();
        }


        private void CreateCursor()
        {
            if (cursorPrefab != null)
            {
                _cursorVisual = Instantiate(cursorPrefab, Vector3.zero, Quaternion.identity, transform);
                _usingPrefabCursor = true;
            }
            else
            {
                _cursorVisual = new GameObject("Cursor");
                _cursorVisual.transform.SetParent(transform);

                var sr = _cursorVisual.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCursorSprite();
                sr.color = cursorColor;
                sr.sortingOrder = 10;
                _usingPrefabCursor = false;
            }

            // Create Block Slip indicator (hidden by default)
            CreateBlockSlipIndicator();

            UpdateCursorPosition(0f);
        }

        private void CreateBlockSlipIndicator()
        {
            _blockSlipIndicator = new GameObject("BlockSlipIndicator");
            _blockSlipIndicator.transform.SetParent(transform);

            var sr = _blockSlipIndicator.AddComponent<SpriteRenderer>();
            sr.sprite = CreateBlockSlipSprite();
            sr.color = blockSlipColor;
            sr.sortingOrder = 9; // Behind cursor but in front of tiles

            _blockSlipIndicator.SetActive(false);
        }

        private Sprite CreateBlockSlipSprite()
        {
            // Create a 1x1 unit sprite (100x100 pixels at 100 ppu) that will be scaled to cursor size
            var size = 100;
            var tex = new Texture2D(size, size);
            var pixels = new Color[size * size];

            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // Create a glowing border effect
            for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
                // Outer glow (border)
                if (x < 5 || x >= size - 5 || y < 5 || y >= size - 5)
                    pixels[y * size + x] = Color.white;
                // Inner filled area with reduced opacity
                else if (x >= 5 && x < size - 5 && y >= 5 && y < size - 5)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, 0.3f);

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        private Sprite CreateCursorSprite()
        {
            var tex = new Texture2D(200, 100);
            var pixels = new Color[200 * 100];

            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            for (var x = 0; x < 200; x++)
            for (var y = 0; y < 100; y++)
                if (x < 5 || x >= 195 || y < 5 || y >= 95)
                    pixels[y * 200 + x] = Color.white;

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, 200, 100), new Vector2(0.5f, 0.5f), 100);
        }

        public void HandleInput()
        {
            // Don't process input when game is paused
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)
                return;

            // Cursor movement - Arrow Keys (primary) or WASD (alternate)
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                MoveLeft();
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                MoveRight();
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                MoveUp();
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveDown();
        }

        private void MoveCursor(int dx, int dy)
        {
            // Use current cursorPosition instead of GridX/GridY
            var newX = Mathf.Clamp(_cursorPosition.x + dx, 0, _gridWidth - 1);
            var newY = Mathf.Clamp(_cursorPosition.y + dy, 0, _gridHeight - 1);

            Debug.Log(
                $"[Cursor] MoveCursor called: from ({_cursorPosition.x},{_cursorPosition.y}) to ({newX},{newY}), direction=({dx},{dy})");

            // Check if the new row is active (above visibility threshold)
            if (!IsRowActive(newY))
            {
                Debug.Log($"[Cursor] Blocked movement to row {newY} - not yet active");
                return; // Don't move cursor to inactive rows
            }

            _cursorPosition = new Vector2Int(newX, newY);

            // keep these in sync if other systems use them
            GridX = newX;
            GridY = newY;

            Debug.Log($"[Cursor] Movement successful! New position: ({newX},{newY})");

            UpdateCursorPosition(_currentGridOffset);
        }

        private bool IsRowActive(int gridY)
        {
            // If no TileSpawner or GridRiser, assume all rows are active (backwards compatibility)
            if (_tileSpawner == null || gridRiser == null)
            {
                Debug.LogWarning(
                    $"[Cursor] IsRowActive bypassed - tileSpawner={_tileSpawner != null}, gridRiser={gridRiser != null}");
                return true;
            }

            // Get the real-time grid offset from GridRiser (not the cached value)
            var realTimeOffset = gridRiser.CurrentGridOffset;

            // Calculate the world Y position of this row
            var worldY = gridY * _tileSize + realTimeOffset;
            var visibilityThreshold = 0f; // Only active when fully at y=0 or above

            var isActive = worldY >= visibilityThreshold;

            Debug.Log(
                $"[Cursor] Row {gridY} check: worldY={worldY:F3}, threshold={visibilityThreshold:F3}, offset={realTimeOffset:F3}, isActive={isActive}");

            return isActive;
        }

        public void UpdateCursorPosition(float gridOffset)
        {
            _currentGridOffset = gridOffset;
            var centerX = (_cursorPosition.x + 1f) * _tileSize;
            var centerY = _cursorPosition.y * _tileSize + gridOffset;
            _cursorVisual.transform.position = new Vector3(centerX - 0.5f * _tileSize, centerY, -1f);

            if (!_usingPrefabCursor)
                _cursorVisual.transform.localScale = new Vector3(cursorWidth * _tileSize, cursorHeight * _tileSize, 1f);

            // Update Block Slip indicator position if active
            if (_blockSlipActive && _blockSlipIndicator != null)
            {
                _blockSlipIndicator.transform.position = new Vector3(centerX - 0.5f * _tileSize, centerY, -0.5f);
                _blockSlipIndicator.transform.localScale =
                    new Vector3(cursorWidth * _tileSize, cursorHeight * _tileSize, 1f);
            }
        }

        public void SetBlockSlipIndicator(bool active)
        {
            _blockSlipActive = active;
            if (_blockSlipIndicator != null)
            {
                _blockSlipIndicator.SetActive(active);
                if (active) UpdateCursorPosition(_currentGridOffset);
            }
        }

        public void ShiftCursorUp(int gridHeight, float gridOffset)
        {
            // Shift cursor up in grid coordinates to maintain world position when grid rises
            _cursorPosition.y = Mathf.Min(_cursorPosition.y + 1, gridHeight - 1);
            UpdateCursorPosition(gridOffset);
        }
    }
}