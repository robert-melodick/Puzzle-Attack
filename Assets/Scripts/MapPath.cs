using UnityEngine;

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
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
            
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
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
        lineRenderer.sortingOrder = -1; // Behind nodes
        
        // Set a default material if none exists
        if (lineRenderer.material == null)
        {
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
    }
    
    void GeneratePath()
    {
        if (startNode == null || endNode == null)
            return;
            
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
            lineRenderer.SetPosition(0, startNode.transform.position);
            lineRenderer.SetPosition(1, endNode.transform.position);
        }
    }
    
    void GenerateCurvedPath()
    {
        Vector3 start = startNode.transform.position;
        Vector3 end = endNode.transform.position;
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
        // Path is visible if it's marked as visible AND the start node is unlocked
        bool shouldBeVisible = isVisible && startNode != null && startNode.isUnlocked;
        
        lineRenderer.enabled = shouldBeVisible;
        lineRenderer.startColor = shouldBeVisible ? visibleColor : hiddenColor;
        lineRenderer.endColor = shouldBeVisible ? visibleColor : hiddenColor;
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
}