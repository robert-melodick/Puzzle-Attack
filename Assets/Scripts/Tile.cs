using UnityEngine;

public class Tile : MonoBehaviour
{
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public int TileType { get; private set; }
    private GridManager gridManager;

    [Header("Sound Effects")]
    public AudioClip landSound;
    public AudioClip matchSound;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void Initialize(int x, int y, int type, GridManager manager)
    {
        GridX = x;
        GridY = y;
        TileType = type;
        gridManager = manager;
    }

    public void PlayLandSound()
    {
        if (landSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(landSound);
        }
    }

    public void PlayMatchSound()
    {
        if (matchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(matchSound);
        }
    }
}