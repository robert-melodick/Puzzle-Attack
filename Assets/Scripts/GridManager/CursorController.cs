using UnityEngine;

public class CursorController : MonoBehaviour, ICursorCommands
{
    [Header("Cursor Settings")]
    public int cursorWidth = 2;
    public int cursorHeight = 1;
    public Color cursorColor = new Color(1f, 1f, 1f, 0.5f);
    public GameObject cursorPrefab;

    [Header("Block Slip Indicator")]
    public Color blockSlipColor = new Color(0f, 1f, 0.5f, 0.7f); // Green/cyan glow

    private Vector2Int cursorPosition;
    private GameObject cursorVisual;
    private GameObject blockSlipIndicator;
    private bool usingPrefabCursor = false;

    [SerializeField]
    private GridManager gridManager;
    [SerializeField]
    private GridRiser gridRiser;

    public int GridX { get; private set; }
    public int GridY { get; private set; }

    private float tileSize;
    private float currentGridOffset = 0f;
    private bool blockSlipActive = false;

    public Vector2Int CursorPosition => cursorPosition;

    public void Initialize(GridManager manager, float tileSize, int gridWidth, int gridHeight)
    {
        this.gridManager = manager;
        this.tileSize = tileSize;

        cursorPosition = new Vector2Int(0, gridHeight / 2);
        GridX = cursorPosition.x;
        GridY = cursorPosition.y;

        CreateCursor();
    }


    void CreateCursor()
    {
        if (cursorPrefab != null)
        {
            cursorVisual = Instantiate(cursorPrefab, Vector3.zero, Quaternion.identity, transform);
            usingPrefabCursor = true;
        }
        else
        {
            cursorVisual = new GameObject("Cursor");
            cursorVisual.transform.SetParent(transform);

            SpriteRenderer sr = cursorVisual.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCursorSprite();
            sr.color = cursorColor;
            sr.sortingOrder = 10;
            usingPrefabCursor = false;
        }

        // Create Block Slip indicator (hidden by default)
        CreateBlockSlipIndicator();

        UpdateCursorPosition(0f);
    }

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

    void CreateBlockSlipIndicator()
    {
        blockSlipIndicator = new GameObject("BlockSlipIndicator");
        blockSlipIndicator.transform.SetParent(transform);

        SpriteRenderer sr = blockSlipIndicator.AddComponent<SpriteRenderer>();
        sr.sprite = CreateBlockSlipSprite();
        sr.color = blockSlipColor;
        sr.sortingOrder = 9; // Behind cursor but in front of tiles

        blockSlipIndicator.SetActive(false);
    }

    Sprite CreateBlockSlipSprite()
    {
        // Create a 1x1 unit sprite (100x100 pixels at 100 ppu) that will be scaled to cursor size
        int size = 100;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        // Create a glowing border effect
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // Outer glow (border)
                if (x < 5 || x >= size - 5 || y < 5 || y >= size - 5)
                {
                    pixels[y * size + x] = Color.white;
                }
                // Inner filled area with reduced opacity
                else if (x >= 5 && x < size - 5 && y >= 5 && y < size - 5)
                {
                    pixels[y * size + x] = new Color(1f, 1f, 1f, 0.3f);
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
    }

    Sprite CreateCursorSprite()
    {
        Texture2D tex = new Texture2D(200, 100);
        Color[] pixels = new Color[200 * 100];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        for (int x = 0; x < 200; x++)
        {
            for (int y = 0; y < 100; y++)
            {
                if (x < 5 || x >= 195 || y < 5 || y >= 95)
                {
                    pixels[y * 200 + x] = Color.white;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 200, 100), new Vector2(0.5f, 0.5f), 100);
    }

    public void HandleInput()
    {
        // Cursor movement - Arrow Keys (primary) or WASD (alternate)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            MoveLeft();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            MoveRight();
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            MoveUp();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            MoveDown();
        }
    }

    private void MoveCursor(int dx, int dy)
    {
        Debug.Log($"Moving cursor by ({dx}, {dy})");

        // Use current cursorPosition instead of GridX/GridY
        int newX = Mathf.Clamp(cursorPosition.x + dx, 0, gridManager.gridWidth - 1);
        int newY = Mathf.Clamp(cursorPosition.y + dy, 0, gridManager.gridHeight - 1);

        cursorPosition = new Vector2Int(newX, newY);

        // keep these in sync if other systems use them
        GridX = newX;
        GridY = newY;

        UpdateCursorPosition(currentGridOffset);
    }

    public void Swap()
    {
        if (gridManager != null)
        {
            gridManager.RequestSwapAtCursor();
        }
    }

    public void FastRiseGrid()
    {
        if (gridManager != null)
        {
            gridRiser.RequestFastRise();
        }
    }

    public void UpdateCursorPosition(float gridOffset)
    {
        currentGridOffset = gridOffset;
        float centerX = (cursorPosition.x + 1f) * tileSize;
        float centerY = cursorPosition.y * tileSize + gridOffset;
        cursorVisual.transform.position = new Vector3(centerX - 0.5f * tileSize, centerY, -1f);

        if(!usingPrefabCursor)
        {
            cursorVisual.transform.localScale = new Vector3(cursorWidth * tileSize, cursorHeight * tileSize, 1f);
        }

        // Update Block Slip indicator position if active
        if (blockSlipActive && blockSlipIndicator != null)
        {
            blockSlipIndicator.transform.position = new Vector3(centerX - 0.5f * tileSize, centerY, -0.5f);
            blockSlipIndicator.transform.localScale = new Vector3(cursorWidth * tileSize, cursorHeight * tileSize, 1f);
        }
    }

    public void SetBlockSlipIndicator(bool active)
    {
        blockSlipActive = active;
        if (blockSlipIndicator != null)
        {
            blockSlipIndicator.SetActive(active);
            if (active)
            {
                UpdateCursorPosition(currentGridOffset);
            }
        }
    }

    public void ShiftCursorUp(int gridHeight, float gridOffset)
    {
        // Shift cursor up in grid coordinates to maintain world position when grid rises
        cursorPosition.y = Mathf.Min(cursorPosition.y + 1, gridHeight - 1);
        UpdateCursorPosition(gridOffset);
    }
}
