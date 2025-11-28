using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class WorldMapController : MonoBehaviour
{
    [Header("Player Settings")]
    public Transform playerMarker;
    public MapNode currentNode;
    public float moveSpeed = 5f;
    
    [Header("Input Settings")]
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode selectKey = KeyCode.Space;
    
    [Header("Map Nodes")]
    public List<MapNode> allNodes = new List<MapNode>();
    
    private bool isMoving = false;
    private List<MapNode> availableNodes = new List<MapNode>();
    
    void Start()
    {
        if (currentNode != null && playerMarker != null)
        {
            playerMarker.position = currentNode.transform.position;
        }
        
        // Auto-find all nodes if list is empty
        if (allNodes.Count == 0)
        {
            allNodes = FindObjectsByType<MapNode>(FindObjectsSortMode.None).ToList();
        }
        
        UpdateAvailableNodes();
    }
    
    void Update()
    {
        if (isMoving || currentNode == null)
            return;
            
        HandleInput();
    }
    
    void HandleInput()
    {
        Vector2 inputDirection = Vector2.zero;
        
        if (Input.GetKeyDown(upKey))
            inputDirection = Vector2.up;
        else if (Input.GetKeyDown(downKey))
            inputDirection = Vector2.down;
        else if (Input.GetKeyDown(leftKey))
            inputDirection = Vector2.left;
        else if (Input.GetKeyDown(rightKey))
            inputDirection = Vector2.right;
        else if (Input.GetKeyDown(selectKey))
        {
            SelectCurrentNode();
            return;
        }
        
        if (inputDirection != Vector2.zero)
        {
            MapNode targetNode = GetNodeInDirection(inputDirection);
            if (targetNode != null)
            {
                MoveToNode(targetNode);
            }
        }
    }
    
    MapNode GetNodeInDirection(Vector2 direction)
    {
        UpdateAvailableNodes();
        
        if (availableNodes.Count == 0)
            return null;
            
        // Find the node that best matches the input direction
        MapNode bestNode = null;
        float bestScore = -1f;
        
        foreach (var node in availableNodes)
        {
            Vector2 toNode = (node.transform.position - currentNode.transform.position).normalized;
            float dot = Vector2.Dot(direction, toNode);
            
            // Only consider nodes that are generally in the input direction
            if (dot > 0.5f && dot > bestScore)
            {
                bestScore = dot;
                bestNode = node;
            }
        }
        
        return bestNode;
    }
    
    void UpdateAvailableNodes()
    {
        availableNodes.Clear();
        
        if (currentNode == null)
            return;
            
        foreach (var path in currentNode.connectedPaths)
        {
            if (path.CanTraverse(currentNode))
            {
                MapNode destination = path.GetDestination(currentNode);
                if (destination != null && !availableNodes.Contains(destination))
                {
                    availableNodes.Add(destination);
                }
            }
        }
    }
    
    void MoveToNode(MapNode targetNode)
    {
        StartCoroutine(MovePlayerToNode(targetNode));
    }
    
    System.Collections.IEnumerator MovePlayerToNode(MapNode targetNode)
    {
        isMoving = true;
        
        Vector3 startPos = playerMarker.position;
        Vector3 endPos = targetNode.transform.position;
        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / moveSpeed;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            playerMarker.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        
        playerMarker.position = endPos;
        currentNode = targetNode;
        isMoving = false;
        
        UpdateAvailableNodes();
    }
    
    void SelectCurrentNode()
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
        if (node != null)
        {
            node.Unlock();
        }
    }
    
    public void UnlockNodeByName(string nodeName)
    {
        MapNode node = allNodes.FirstOrDefault(n => n.nodeName == nodeName);
        if (node != null)
        {
            node.Unlock();
        }
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