using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways] // Makes this script run in edit mode
public class MapNode : MonoBehaviour
{
    [Header("Node Settings")]
    public string nodeName;
    public bool isUnlocked = false;
    public bool isCompleted = false;
    
    [Header("Visual Settings")]
    public Sprite unlockedSprite;
    public Sprite lockedSprite;
    public Sprite completedSprite;
    public Color unlockedColor = Color.white;
    public Color lockedColor = Color.gray;
    
    [Header("Connections")]
    public List<MapPath> connectedPaths = new List<MapPath>();
    
    private SpriteRenderer spriteRenderer;
    
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisuals();
    }

    void OnValidate()
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
        {
            if (path != null)
            {
                path.UpdateVisibility();
            }
        }
    }

    public void Complete()
    {
        isCompleted = true;
        UpdateVisuals();
    }

    // Editor utilities: Right-click on the component for quick actions
    [ContextMenu("Unlock Node")]
    void UnlockNode()
    {
        Unlock();
    }

    [ContextMenu("Lock Node")]
    void LockNode()
    {
        isUnlocked = false;
        isCompleted = false;
        UpdateVisuals();
    }

    [ContextMenu("Complete Node")]
    void CompleteNode()
    {
        Complete();
    }

    [ContextMenu("Update Visuals")]
    void RefreshVisuals()
    {
        UpdateVisuals();
    }
}