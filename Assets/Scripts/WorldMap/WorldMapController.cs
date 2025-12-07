using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WorldMapController : MonoBehaviour
{
    [Header("Player Settings")] public Transform playerMarker;

    public MapNode currentNode;
    public float moveSpeed = 5f;

    [Header("Input Settings")] public KeyCode upKey = KeyCode.W;

    public KeyCode downKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode selectKey = KeyCode.Space;

    [Header("Map Nodes")] public List<MapNode> allNodes = new();

    private readonly List<MapNode> availableNodes = new();

    public bool IsMoving { get; private set; }


    private void Start()
    {
        if (currentNode != null && playerMarker != null) playerMarker.position = currentNode.transform.position;

        // Auto-find all nodes if list is empty
        if (allNodes.Count == 0) allNodes = FindObjectsByType<MapNode>(FindObjectsSortMode.None).ToList();

        UpdateAvailableNodes();
    }

    private void Update()
    {
        if (IsMoving || currentNode == null)
            return;

        HandleInput();
    }

    private void HandleInput()
    {
        var inputDirection = Vector2.zero;

        if (Input.GetKeyDown(upKey))
        {
            inputDirection = Vector2.up;
        }
        else if (Input.GetKeyDown(downKey))
        {
            inputDirection = Vector2.down;
        }
        else if (Input.GetKeyDown(leftKey))
        {
            inputDirection = Vector2.left;
        }
        else if (Input.GetKeyDown(rightKey))
        {
            inputDirection = Vector2.right;
        }
        else if (Input.GetKeyDown(selectKey))
        {
            SelectCurrentNode();
            return;
        }

        if (inputDirection != Vector2.zero)
        {
            var targetNode = GetNodeInDirection(inputDirection);
            if (targetNode != null) MoveToNode(targetNode);
        }
    }

    private MapNode GetNodeInDirection(Vector2 direction)
    {
        UpdateAvailableNodes();

        if (availableNodes.Count == 0)
            return null;

        // Find the node that best matches the input direction
        MapNode bestNode = null;
        var bestScore = -1f;

        foreach (var node in availableNodes)
        {
            Vector2 toNode = (node.transform.position - currentNode.transform.position).normalized;
            var dot = Vector2.Dot(direction, toNode);

            // Only consider nodes that are generally in the input direction
            if (dot > 0.5f && dot > bestScore)
            {
                bestScore = dot;
                bestNode = node;
            }
        }

        return bestNode;
    }

    private void UpdateAvailableNodes()
    {
        availableNodes.Clear();
        if (currentNode == null)
            return;

        // Find all paths in the scene
        var paths = FindObjectsByType<MapPath>(FindObjectsSortMode.None);

        foreach (var path in paths)
        {
            if (!path.CanTraverse(currentNode))
                continue;

            var destination = path.GetDestination(currentNode);
            if (destination != null && !availableNodes.Contains(destination)) availableNodes.Add(destination);
        }
    }

    private MapPath FindPathBetween(MapNode from, MapNode to)
    {
        var paths = FindObjectsByType<MapPath>(FindObjectsSortMode.None);
        foreach (var path in paths)
        {
            var forward = path.startNode == from && path.endNode == to;
            var backward = path.isBidirectional && path.startNode == to && path.endNode == from;

            if (forward || backward)
                return path;
        }

        return null;
    }


    private void MoveToNode(MapNode targetNode)
    {
        var path = FindPathBetween(currentNode, targetNode);
        StartCoroutine(MovePlayerAlongPath(targetNode, path));
    }

    private IEnumerator MovePlayerAlongPath(MapNode targetNode, MapPath path)
    {
        IsMoving = true;

        Vector3[] points = null;

        if (path != null) points = path.GetPathPoints(currentNode);

        // Fallback: no path or broken lineRenderer → straight line
        if (points == null || points.Length < 2)
            points = new[]
            {
                playerMarker.position,
                targetNode.transform.position
            };

        // Move along each segment with consistent speed
        for (var i = 1; i < points.Length; i++)
        {
            var startPos = points[i - 1];
            var endPos = points[i];

            // keep marker on same z as current
            startPos.z = playerMarker.position.z;
            endPos.z = playerMarker.position.z;

            var distance = Vector3.Distance(startPos, endPos);
            var duration = distance / moveSpeed;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                playerMarker.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            playerMarker.position = endPos;
        }

        currentNode = targetNode;
        IsMoving = false;
        UpdateAvailableNodes();
    }


    private void SelectCurrentNode()
    {
        if (currentNode != null)
        {
            Debug.Log($"Selected node: {currentNode.nodeName}");
            // Add your level loading or scene transition logic here
            OnNodeSelected(currentNode);
        }
    }

    // Override this method or add UnityEvents for custom behavior
    protected virtual void OnNodeSelected(MapNode node)
    {
        // Example: Load level, show menu, etc.
        node.Complete();
    }

    // Public methods for external control
    public void UnlockNode(MapNode node)
    {
        if (node != null) node.Unlock();
    }

    public void UnlockNodeByName(string nodeName)
    {
        var node = allNodes.FirstOrDefault(n => n.nodeName == nodeName);
        if (node != null) node.Unlock();
    }

    public void SetPathVisibility(MapPath path, bool visible)
    {
        if (path != null)
        {
            path.isVisible = visible;
            path.UpdateVisibility();
        }
    }
}