using UnityEngine;

[RequireComponent(typeof(Camera))]
public class WorldMapCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                     // playerMarker
    public WorldMapController worldMapController;

    [Header("Dead Zone (fraction of screen)")]
    [Range(0f, 0.5f)] public float horizontalDeadZone = 0.2f;
    [Range(0f, 0.5f)] public float verticalDeadZone = 0.2f;

    [Header("Movement")]
    public float followSpeed = 5f;              // while following near edge
    public float centerSpeed = 6f;              // when snapping to node center

    // For keeping camera from going out of bounds
    [Header("Optional World Bounds")]
    public bool useBounds = false;
    public Vector2 minBounds;                   // bottom-left world pos
    public Vector2 maxBounds;                   // top-right world pos

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 camPos = transform.position;

        // Where is the target on the screen? (0–1 in both axes)
        Vector3 viewportPos = cam.WorldToViewportPoint(target.position);

        bool outsideDeadZone =
            viewportPos.x < horizontalDeadZone ||
            viewportPos.x > 1f - horizontalDeadZone ||
            viewportPos.y < verticalDeadZone ||
            viewportPos.y > 1f - verticalDeadZone;

        Vector3 desiredPos = camPos;

        // Only follow when leaving the dead zone
        if (worldMapController == null || worldMapController.IsMoving)
        {
            if (outsideDeadZone)
            {
                desiredPos = new Vector3(target.position.x, target.position.y, camPos.z);
            }
        }
        // When NOT moving, always center on the node
        else
        {
            desiredPos = new Vector3(target.position.x, target.position.y, camPos.z);
        }

        // Clamping to world bounds
        if (useBounds)
        {
            desiredPos.x = Mathf.Clamp(desiredPos.x, minBounds.x, maxBounds.x);
            desiredPos.y = Mathf.Clamp(desiredPos.y, minBounds.y, maxBounds.y);
        }

        float speed = (worldMapController != null && !worldMapController.IsMoving)
            ? centerSpeed
            : followSpeed;

        transform.position = Vector3.Lerp(camPos, desiredPos, Time.deltaTime * speed);
    }
}
