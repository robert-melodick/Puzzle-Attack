using UnityEngine;

public class FloatingMotion : MonoBehaviour
{
    [Header("Float Settings")]
    public float amplitude = 0.25f;   // How far it moves up/down
    public float frequency = 1f;      // Speed of movement

    private Vector3 _startLocalPos;

    void Start()
    {
        // Store the original local position
        _startLocalPos = transform.localPosition;
    }

    void Update()
    {
        // Calculate new Y offset using sine wave
        float yOffset = Mathf.Sin(Time.time * frequency) * amplitude;

        // Apply smooth local movement
        transform.localPosition = _startLocalPos + new Vector3(0f, yOffset, 0f);
    }
}