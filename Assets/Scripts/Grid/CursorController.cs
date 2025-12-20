using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Handles cursor movement, rendering, and BlockSlip visual indicator.
    /// </summary>
    public class CursorController : MonoBehaviour, ICursorCommands
    {
        #region Inspector Fields

        [Header("Cursor Settings")]
        public int cursorWidth = 2;
        public int cursorHeight = 1;
        public Color cursorColor = new(1f, 1f, 1f, 0.5f);
        public GameObject cursorPrefab;

        [Header("Block Slip Indicator")]
        public Color blockSlipColor = new(0f, 1f, 0.5f, 0.7f);

        [SerializeField] private GridManager _gridManager;
        [SerializeField] private GridRiser _gridRiser;

        #endregion

        #region Private Fields

        private Vector2Int _position;
        private float _tileSize;
        private int _gridWidth;
        private int _gridHeight;
        private float _currentGridOffset;

        private TileSpawner _tileSpawner;
        private GameObject _cursorVisual;
        private GameObject _blockSlipIndicator;
        private bool _usingPrefabCursor;
        private bool _blockSlipActive;

        #endregion

        #region Properties

        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public Vector2Int CursorPosition => _position;

        #endregion

        #region Initialization

        public void Initialize(GridManager manager, float tileSize, int gridWidth, int gridHeight,
            TileSpawner spawner = null, GridRiser riser = null)
        {
            _gridManager = manager;
            _tileSize = tileSize;
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            _tileSpawner = spawner;
            if (riser != null) _gridRiser = riser;

            _position = new Vector2Int(0, gridHeight / 2);
            GridX = _position.x;
            GridY = _position.y;

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
            sr.sortingOrder = 9;

            _blockSlipIndicator.SetActive(false);
        }

        #endregion

        #region ICursorCommands

        public void MoveLeft() => MoveCursor(-1, 0);
        public void MoveRight() => MoveCursor(1, 0);
        public void MoveUp() => MoveCursor(0, 1);
        public void MoveDown() => MoveCursor(0, -1);

        public void Swap()
        {
            _gridManager?.RequestSwapAtCursor();
        }

        public void FastRiseGrid()
        {
            _gridRiser?.RequestFastRise();
        }

        #endregion

        #region Input Handling

        public void HandleInput()
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.IsPaused)
                return;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                MoveLeft();
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                MoveRight();
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                MoveUp();
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                MoveDown();
        }

        private void MoveCursor(int dx, int dy)
        {
            var newX = Mathf.Clamp(_position.x + dx, 0, _gridWidth - 2);
            var newY = Mathf.Clamp(_position.y + dy, 0, _gridHeight - 1);

            Debug.Log($"[Cursor] Move from ({_position.x},{_position.y}) to ({newX},{newY})");

            if (!IsRowActive(newY))
            {
                Debug.Log($"[Cursor] Row {newY} not active");
                return;
            }

            _position = new Vector2Int(newX, newY);
            GridX = newX;
            GridY = newY;

            Debug.Log($"[Cursor] Now at ({newX},{newY})");
            UpdateCursorPosition(_currentGridOffset);
        }

        private bool IsRowActive(int gridY)
        {
            if (_tileSpawner == null || _gridRiser == null)
            {
                Debug.LogWarning("[Cursor] IsRowActive bypassed - missing references");
                return true;
            }

            var realTimeOffset = _gridRiser.CurrentGridOffset;
            var worldY = gridY * _tileSize + realTimeOffset;
            const float visibilityThreshold = 0f;

            var isActive = worldY >= visibilityThreshold;
            Debug.Log($"[Cursor] Row {gridY}: worldY={worldY:F3}, active={isActive}");

            return isActive;
        }

        #endregion

        #region Position Updates

        public void UpdateCursorPosition(float gridOffset)
        {
            _currentGridOffset = gridOffset;
            var centerX = (_position.x + 1f) * _tileSize;
            var centerY = _position.y * _tileSize + gridOffset;
            _cursorVisual.transform.position = new Vector3(centerX - 0.5f * _tileSize, centerY, -1f);

            if (!_usingPrefabCursor)
                _cursorVisual.transform.localScale = new Vector3(cursorWidth * _tileSize, cursorHeight * _tileSize, 1f);

            if (_blockSlipActive && _blockSlipIndicator != null)
            {
                _blockSlipIndicator.transform.position = new Vector3(centerX - 0.5f * _tileSize, centerY, -0.5f);
                _blockSlipIndicator.transform.localScale = new Vector3(cursorWidth * _tileSize, cursorHeight * _tileSize, 1f);
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
            _position.y = Mathf.Min(_position.y + 1, gridHeight - 1);
            UpdateCursorPosition(gridOffset);
        }

        #endregion

        #region Sprite Generation

        private Sprite CreateCursorSprite()
        {
            var tex = new Texture2D(200, 100);
            var pixels = new Color[200 * 100];

            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            for (var x = 0; x < 200; x++)
            for (var y = 0; y < 100; y++)
                if (x < 5 || x >= 195 || y < 5 || y >= 95)
                    pixels[y * 200 + x] = Color.white;

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, 200, 100), new Vector2(0.5f, 0.5f), 100);
        }

        private Sprite CreateBlockSlipSprite()
        {
            const int size = 100;
            var tex = new Texture2D(size, size);
            var pixels = new Color[size * size];

            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            for (var x = 0; x < size; x++)
            for (var y = 0; y < size; y++)
            {
                if (x < 5 || x >= size - 5 || y < 5 || y >= size - 5)
                    pixels[y * size + x] = Color.white;
                else
                    pixels[y * size + x] = new Color(1f, 1f, 1f, 0.3f);
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        #endregion
    }
}