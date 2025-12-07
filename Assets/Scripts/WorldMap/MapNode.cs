using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways] // Makes this script run in edit mode
public class MapNode : MonoBehaviour
{
    [Header("Node Settings")] public string nodeName;

    public bool isUnlocked;
    public bool isCompleted;

    [Header("Visual Settings")] public Sprite unlockedSprite;

    public Sprite lockedSprite;
    public Sprite completedSprite;
    public Color unlockedColor = Color.white;
    public Color lockedColor = Color.gray;

    [Header("Connections")] public List<MapPath> connectedPaths = new();

    [Header("Quick Connect (Editor Only)")] [Tooltip("Drag a node here and use the context menu to connect")]
    public MapNode nodeToConnect;

    public bool makeConnectionBidirectional = true;
    public bool makeConnectionCurved;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisuals();
    }

    private void OnValidate()
    {
        // Called when inspector values change (edit mode and play mode)
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            return;

        if (isCompleted && completedSprite != null)
        {
            spriteRenderer.sprite = completedSprite;
        }
        else if (isUnlocked && unlockedSprite != null)
        {
            spriteRenderer.sprite = unlockedSprite;
            spriteRenderer.color = unlockedColor;
        }
        else if (lockedSprite != null)
        {
            spriteRenderer.sprite = lockedSprite;
            spriteRenderer.color = lockedColor;
        }
        else
        {
            // Fallback: use color to indicate state if sprites aren't set
            spriteRenderer.color = isUnlocked ? unlockedColor : lockedColor;
        }
    }

    public void Unlock()
    {
        isUnlocked = true;
        UpdateVisuals();

        // Update connected paths visibility
        foreach (var path in connectedPaths)
            if (path != null)
                path.UpdateVisibility();
    }

    public void Complete()
    {
        isCompleted = true;
        UpdateVisuals();
    }

    // Editor utilities: Right-click on the component for quick actions
    [ContextMenu("Unlock Node")]
    private void UnlockNode()
    {
        Unlock();
    }

    [ContextMenu("Lock Node")]
    private void LockNode()
    {
        isUnlocked = false;
        isCompleted = false;
        UpdateVisuals();
    }

    [ContextMenu("Complete Node")]
    private void CompleteNode()
    {
        Complete();
    }

    [ContextMenu("Update Visuals")]
    private void RefreshVisuals()
    {
        UpdateVisuals();
    }

    [ContextMenu("Connect to Selected Node")]
    private void ConnectToSelectedNode()
    {
        if (nodeToConnect == null)
        {
            Debug.LogWarning("No node selected in 'Node To Connect' field!");
            return;
        }

        ConnectTo(nodeToConnect, makeConnectionBidirectional, makeConnectionCurved);

        // Clear the field after connecting
        nodeToConnect = null;
    }

    [ContextMenu("Disconnect from Selected Node")]
    private void DisconnectFromSelectedNode()
    {
        if (nodeToConnect == null)
        {
            Debug.LogWarning("No node selected in 'Node To Connect' field!");
            return;
        }

        DisconnectFrom(nodeToConnect);

        // Clear the field after disconnecting
        nodeToConnect = null;
    }

    // Helper method to create a path to another node
    public MapPath ConnectTo(MapNode targetNode, bool bidirectional = true, bool curved = false)
    {
        if (targetNode == null || targetNode == this)
        {
            Debug.LogWarning("Cannot connect to null or self");
            return null;
        }

        // Check if connection already exists
        foreach (var path in connectedPaths)
            if (path != null && (path.startNode == targetNode || path.endNode == targetNode))
            {
                Debug.LogWarning($"Path already exists between {nodeName} and {targetNode.nodeName}");
                return path;
            }

        // Create new GameObject for the path
        var pathObj = new GameObject($"Path_{nodeName}_to_{targetNode.nodeName}");
        pathObj.transform.SetParent(transform.parent); // Put in same parent as nodes
        pathObj.transform.position = (transform.position + targetNode.transform.position) / 2f;

        // Add MapPath component
        var newPath = pathObj.AddComponent<MapPath>();
        newPath.startNode = this;
        newPath.endNode = targetNode;
        newPath.isBidirectional = bidirectional;
        newPath.useCurvedPath = curved;

        // Add to both nodes' connection lists
        if (!connectedPaths.Contains(newPath))
            connectedPaths.Add(newPath);

        if (!targetNode.connectedPaths.Contains(newPath))
            targetNode.connectedPaths.Add(newPath);

        Debug.Log($"Created path from {nodeName} to {targetNode.nodeName}");

        return newPath;
    }

    // Remove a specific path connection
    public void DisconnectFrom(MapNode targetNode)
    {
        if (targetNode == null)
            return;

        MapPath pathToRemove = null;
        foreach (var path in connectedPaths)
            if (path != null && (path.startNode == targetNode || path.endNode == targetNode))
            {
                pathToRemove = path;
                break;
            }

        if (pathToRemove != null)
        {
            connectedPaths.Remove(pathToRemove);
            targetNode.connectedPaths.Remove(pathToRemove);

            if (Application.isPlaying)
                Destroy(pathToRemove.gameObject);
            else
                DestroyImmediate(pathToRemove.gameObject);

            Debug.Log($"Removed path between {nodeName} and {targetNode.nodeName}");
        }
    }
}