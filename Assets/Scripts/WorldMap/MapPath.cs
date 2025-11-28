using UnityEngine;

[ExecuteAlways] // Makes this script run in edit mode
public class MapPath : MonoBehaviour
{
    [Header("Path Settings")]
    public MapNode startNode;
    public MapNode endNode;
    public bool isBidirectional = true;
    public bool isVisible = true; // Controls if the path is shown to player
    
    [Header("Path Appearance")]
    public LineRenderer lineRenderer;
    public float pathWidth = 0.1f;
    public Color visibleColor = Color.white;
    public Color hiddenColor = new Color(1, 1, 1, 0); // Transparent
    
    [Header("Curved Path Settings")]
    public bool useCurvedPath = false;
    public Vector3[] customPathPoints; // For custom shaped paths
    public float curveHeight = 1f; // Height of curve for auto-generated curves
    public int curveResolution = 20; // Smoothness of curve
    
    void Awake()
    {
        InitializeLineRenderer();
    }

    void OnValidate()
    {
        // Called when inspector values change (edit mode and play mode)
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            SetupLineRenderer();
            GeneratePath();
            UpdateVisibility();
        }
    }

    void InitializeLineRenderer()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            if (!Application.isPlaying)
            {
                Debug.Log("LineRenderer component was missing. Added one automatically.");
            }
        }

        SetupLineRenderer();
        GeneratePath();
        UpdateVisibility();
    }
    
    void SetupLineRenderer()
    {
        lineRenderer.startWidth = pathWidth;
        lineRenderer.endWidth = pathWidth;
        lineRenderer.useWorldSpace = true;

        // Set sorting layer and order for 2D visibility
        lineRenderer.sortingLayerName = "Default";
        lineRenderer.sortingOrder = -1; // Behind nodes

        // Set a default material if none exists
        if (lineRenderer.material == null || lineRenderer.sharedMaterial == null)
        {
            // Use Unlit/Color shader which works best with LineRenderer
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
                Debug.LogWarning("Unlit/Color shader not found, using Sprites/Default");
            }

            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
            else
            {
                Debug.LogError("Could not find suitable shader for LineRenderer!");
            }
        }

        // IMPORTANT: Set material color to white so it doesn't tint the line
        if (lineRenderer.material != null)
        {
            lineRenderer.material.color = Color.white;
        }

        // Set vertex colors (these are what actually control the line color)
        lineRenderer.startColor = visibleColor;
        lineRenderer.endColor = visibleColor;

        // Only log in play mode to avoid spam in edit mode
        if (Application.isPlaying)
        {
            Debug.Log($"LineRenderer setup: width={pathWidth}, sortingOrder={lineRenderer.sortingOrder}, material={lineRenderer.material?.name}, startColor={lineRenderer.startColor}, endColor={lineRenderer.endColor}");
        }
    }
    
    void GeneratePath()
    {
        if (startNode == null || endNode == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning($"Cannot generate path: startNode={(startNode != null ? startNode.name : "null")}, endNode={(endNode != null ? endNode.name : "null")}");
            }
            return;
        }

        // Use custom points if provided
        if (customPathPoints != null && customPathPoints.Length > 0)
        {
            lineRenderer.positionCount = customPathPoints.Length;
            lineRenderer.SetPositions(customPathPoints);
        }
        // Generate curved path
        else if (useCurvedPath)
        {
            GenerateCurvedPath();
        }
        // Simple straight line
        else
        {
            lineRenderer.positionCount = 2;
            Vector3 startPos = startNode.transform.position;
            Vector3 endPos = endNode.transform.position;

            // Ensure Z position is 0 for 2D
            startPos.z = 0;
            endPos.z = 0;

            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, endPos);

            if (Application.isPlaying)
            {
                Debug.Log($"Path generated from {startNode.name} at {startPos} to {endNode.name} at {endPos}");
            }
        }
    }
    
    void GenerateCurvedPath()
    {
        Vector3 start = startNode.transform.position;
        Vector3 end = endNode.transform.position;

        // Ensure Z position is 0 for 2D
        start.z = 0;
        end.z = 0;

        Vector3 midPoint = (start + end) / 2f;

        // Calculate perpendicular direction for curve
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0) * curveHeight;
        Vector3 controlPoint = midPoint + perpendicular;

        // Generate bezier curve points
        lineRenderer.positionCount = curveResolution;
        for (int i = 0; i < curveResolution; i++)
        {
            float t = i / (float)(curveResolution - 1);
            Vector3 point = CalculateQuadraticBezierPoint(t, start, controlPoint, end);
            point.z = 0; // Ensure all points are at Z=0
            lineRenderer.SetPosition(i, point);
        }
    }
    
    Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        
        Vector3 point = uu * p0;
        point += 2 * u * t * p1;
        point += tt * p2;
        
        return point;
    }
    
    public void UpdateVisibility()
    {
        if (lineRenderer == null)
            return;

        // In edit mode, always show the path so designers can see it
        bool shouldBeVisible;
        if (!Application.isPlaying)
        {
            shouldBeVisible = true; // Always visible in edit mode
        }
        else
        {
            // In play mode, path is visible if it's marked as visible AND the start node is unlocked
            shouldBeVisible = isVisible && startNode != null && startNode.isUnlocked;

            if (!shouldBeVisible && Application.isPlaying)
            {
                Debug.LogWarning($"MapPath not visible: isVisible={isVisible}, startNode={(startNode != null ? startNode.name : "null")}, startNodeUnlocked={(startNode != null ? startNode.isUnlocked.ToString() : "N/A")}");
            }
        }

        lineRenderer.enabled = shouldBeVisible;

        // Set vertex colors
        Color targetColor = shouldBeVisible ? visibleColor : hiddenColor;
        lineRenderer.startColor = targetColor;
        lineRenderer.endColor = targetColor;

        // Ensure material color is white (not tinting)
        if (lineRenderer.material != null)
        {
            lineRenderer.material.color = Color.white;
        }

        if (Application.isPlaying)
        {
            Debug.Log($"UpdateVisibility: enabled={shouldBeVisible}, visibleColor={visibleColor}, targetColor={targetColor}");
        }
    }
    
    public bool CanTraverse(MapNode from)
    {
        if (from == startNode && endNode.isUnlocked)
            return true;
            
        if (isBidirectional && from == endNode && startNode.isUnlocked)
            return true;
            
        return false;
    }
    
    public MapNode GetDestination(MapNode from)
    {
        if (from == startNode)
            return endNode;
        if (isBidirectional && from == endNode)
            return startNode;
        return null;
    }

    // Editor utility: Right-click on the component and select "Regenerate Path"
    [ContextMenu("Regenerate Path")]
    void RegeneratePath()
    {
        InitializeLineRenderer();
    }
}