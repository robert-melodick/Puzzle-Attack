using UnityEngine;

public class CursorController : MonoBehaviour
{
    [Header("Cursor Settings")]
    public int cursorWidth = 2;
    public int cursorHeight = 1;
    public Color cursorColor = new Color(1f, 1f, 1f, 0.5f);
    public GameObject cursorPrefab;

    private Vector2Int cursorPosition;
    private GameObject cursorVisual;
    private bool usingPrefabCursor = false;
    private GridManager gridManager;
    private float tileSize;
    private float currentGridOffset = 0f;

    public Vector2Int CursorPosition => cursorPosition;

    public void Initialize(GridManager manager, float tileSize, int gridWidth, int gridHeight)
    {
        this.gridManager = manager;
        this.tileSize = tileSize;
        cursorPosition = new Vector2Int(0, gridHeight / 2);
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

        UpdateCursorPosition(0f);
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

    public void HandleInput(int gridWidth, int gridHeight)
    {
        // Cursor movement - Arrow Keys (primary) or WASD (alternate)
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            MoveCursor(-1, 0, gridWidth, gridHeight);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            MoveCursor(1, 0, gridWidth, gridHeight);
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            MoveCursor(0, 1, gridWidth, gridHeight);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            MoveCursor(0, -1, gridWidth, gridHeight);
        }
    }

    void MoveCursor(int deltaX, int deltaY, int gridWidth, int gridHeight)
    {
        int newX = Mathf.Clamp(cursorPosition.x + deltaX, 0, gridWidth - 2);
        int newY = Mathf.Clamp(cursorPosition.y + deltaY, 0, gridHeight - 1);

        cursorPosition = new Vector2Int(newX, newY);
        UpdateCursorPosition(currentGridOffset);
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
    }

    public void ShiftCursorUp(int gridHeight, float gridOffset)
    {
        // Shift cursor up in grid coordinates to maintain world position when grid rises
        cursorPosition.y = Mathf.Min(cursorPosition.y + 1, gridHeight - 1);
        UpdateCursorPosition(gridOffset);
    }
}
