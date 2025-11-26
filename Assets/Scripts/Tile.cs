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

    public void PlayMatchSound(int combo = 1)
    {
        if (matchSound != null && audioSource != null)
        {
            // Calculate pitch based on combo (baseline at combo 1, increases by 0.1 per combo)
            float pitch = 1.0f + ((combo - 1) * 0.1f);

            // Clamp pitch to reasonable range (0.5 to 2.0)
            pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(matchSound);
        }
    }
}