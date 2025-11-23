using UnityEngine;

public class Tile : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int TileType { get; private set; }
    private GridManager gridManager;
    
    public void Initialize(int x, int y, int type, GridManager manager)
    {
        GridX = x;
        GridY = y;
        TileType = type;
        gridManager = manager;
    }
}