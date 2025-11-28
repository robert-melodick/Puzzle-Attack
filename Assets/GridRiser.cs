using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GridRiser : MonoBehaviour
{
    [Header("Rising Mechanics")]
    public float normalRiseSpeed = 0.5f; // Units per second
    public float fastRiseSpeed = 2f; // Units per second when holding X
    public float gracePeriod = 2f; // Seconds before game over when block reaches top

    [Header("Breathing Room")]
    public bool enableBreathingRoom = true; // Toggle breathing room feature
    public float breathingRoomPerTile = 0.2f; // Seconds of breathing room per tile matched
    public float maxBreathingRoom = 5f; // Maximum breathing room duration

    private float currentGridOffset = 0f; // How much the grid has risen
    private float nextRowSpawnOffset = 0f; // When to spawn next row
    private bool isInGracePeriod = false;
    private float gracePeriodTimer = 1.5f;
    private bool hasBlockAtTop = false;
    private bool gameOver = false;
    private float breathingRoomTimer = 0f; // Time remaining before grid resumes rising

    private GridManager gridManager;
    private GameObject[,] grid;
    private GameObject[,] preloadGrid;
    private TileSpawner tileSpawner;
    private CursorController cursorController;
    private MatchDetector matchDetector;
    private MatchProcessor matchProcessor;
    private float tileSize;
    private int gridWidth;
    private int gridHeight;

    public float CurrentGridOffset => currentGridOffset;
    public bool IsInGracePeriod => isInGracePeriod;
    public bool IsGameOver => gameOver;

    public void Initialize(GridManager manager, GameObject[,] grid, GameObject[,] preloadGrid, TileSpawner spawner, CursorController cursor, MatchDetector detector, MatchProcessor processor, float tileSize, int gridWidth, int gridHeight)
    {
        this.gridManager = manager;
        this.grid = grid;
        this.preloadGrid = preloadGrid;
        this.tileSpawner = spawner;
        this.cursorController = cursor;
        this.matchDetector = detector;
        this.matchProcessor = processor;
        this.tileSize = tileSize;
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
    }

    public void StartRising()
    {
        StartCoroutine(RiseGrid());
    }

    public void AddBreathingRoom(int tilesMatched)
    {
        if (!enableBreathingRoom) return;

        float additionalTime = tilesMatched * breathingRoomPerTile;
        breathingRoomTimer = Mathf.Min(breathingRoomTimer + additionalTime, maxBreathingRoom);
        Debug.Log($"Breathing room: +{additionalTime:F2}s for {tilesMatched} tiles (total: {breathingRoomTimer:F2}s)");
    }

    IEnumerator RiseGrid()
    {
        while (!gameOver)
        {
            // Handle breathing room countdown
            if (breathingRoomTimer > 0f)
            {
                breathingRoomTimer -= Time.deltaTime;
                if (breathingRoomTimer < 0f)
                {
                    breathingRoomTimer = 0f;
                }
            }

            if (!isInGracePeriod && !matchProcessor.IsProcessingMatches && breathingRoomTimer <= 0f)
            {
                // X (primary) or L (alternate) to speed up rising
                float riseSpeed = (Input.GetKey(KeyCode.X) || Input.GetKey(KeyCode.L)) ? fastRiseSpeed : normalRiseSpeed;
                float riseAmount = riseSpeed * Time.deltaTime;

                currentGridOffset += riseAmount;
                nextRowSpawnOffset += riseAmount;

                // Update all tile positions
                foreach (GameObject tile in grid)
                {
                    if (tile != null)
                    {
                        Tile tileScript = tile.GetComponent<Tile>();
                        tile.transform.position = new Vector3(
                            tileScript.GridX * tileSize,
                            tileScript.GridY * tileSize + currentGridOffset,
                            0
                        );
                        tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, currentGridOffset);
                    }
                }

                // Update preload tile positions
                foreach (GameObject tile in preloadGrid)
                {
                    if (tile != null)
                    {
                        Tile tileScript = tile.GetComponent<Tile>();
                        tile.transform.position = new Vector3(
                            tileScript.GridX * tileSize,
                            tileScript.GridY * tileSize + currentGridOffset,
                            0
                        );
                        tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, currentGridOffset);
                    }
                }

                cursorController.UpdateCursorPosition(currentGridOffset);

                // Spawn new row when grid has risen one tile height
                if (nextRowSpawnOffset >= tileSize)
                {
                    nextRowSpawnOffset -= tileSize;
                    currentGridOffset -= tileSize;
                    tileSpawner.SpawnRowAtBottom(currentGridOffset, cursorController);

                    // Update positions after spawn
                    foreach (GameObject tile in grid)
                    {
                        if (tile != null)
                        {
                            Tile tileScript = tile.GetComponent<Tile>();
                            tile.transform.position = new Vector3(
                                tileScript.GridX * tileSize,
                                tileScript.GridY * tileSize + currentGridOffset,
                                0
                            );
                            tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, currentGridOffset);
                        }
                    }

                    // Update preload tile positions
                    foreach (GameObject tile in preloadGrid)
                    {
                        if (tile != null)
                        {
                            Tile tileScript = tile.GetComponent<Tile>();
                            tile.transform.position = new Vector3(
                                tileScript.GridX * tileSize,
                                tileScript.GridY * tileSize + currentGridOffset,
                                0
                            );
                            tileSpawner.UpdateTileActiveState(tile, tileScript.GridY, currentGridOffset);
                        }
                    }

                    // Check for matches after spawning new row (only if not already processing)
                    if (!matchProcessor.IsProcessingMatches)
                    {
                        List<GameObject> matches = matchDetector.GetAllMatches();
                        if (matches.Count > 0)
                        {
                            StartCoroutine(matchProcessor.CheckAndClearMatches());
                        }
                    }
                }

                // Check if any block reached the top
                CheckTopRow();
            }
            else if (isInGracePeriod)
            {
                // Grace period countdown
                gracePeriodTimer -= Time.deltaTime;
                Debug.Log($"Grace Period: {gracePeriodTimer:F2}s remaining!");

                if (gracePeriodTimer <= 0f)
                {
                    TriggerGameOver();
                }
                else if (!hasBlockAtTop)
                {
                    // Block was cleared, exit grace period
                    isInGracePeriod = false;
                    Debug.Log("Grace period ended - blocks cleared!");
                }
            }

            yield return null;
        }
    }

    void CheckTopRow()
    {
        hasBlockAtTop = false;

        // Check if any tile in the top row is at or above the danger threshold
        for (int x = 0; x < gridWidth; x++)
        {
            if (grid[x, gridHeight - 1] != null)
            {
                // Calculate the actual world position of the top row
                float topRowWorldY = (gridHeight - 1) * tileSize + currentGridOffset;

                // Only trigger grace period if top row is actually at the top of the screen
                // (gridHeight - 1) * tileSize is the maximum allowed position
                if (topRowWorldY >= (gridHeight - 1) * tileSize)
                {
                    hasBlockAtTop = true;
                    if (!isInGracePeriod)
                    {
                        isInGracePeriod = true;
                        gracePeriodTimer = gracePeriod;
                        Debug.Log("WARNING: Block reached the top! Grace period started!");
                    }
                    break;
                }
            }
        }
    }

    void TriggerGameOver()
    {
        gameOver = true;
        Debug.Log("GAME OVER! Blocks reached the top!");
        // TODO: Show game over screen, stop all gameplay
    }
}
