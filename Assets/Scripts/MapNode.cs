using UnityEngine;
using System.Collections.Generic;

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
    
    public void UpdateVisuals()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
            
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
    }
    
    public void Unlock()
    {
        isUnlocked = true;
        UpdateVisuals();
        
        // Update connected paths visibility
        foreach (var path in connectedPaths)
        {
            path.UpdateVisibility();
        }
    }
    
    public void Complete()
    {
        isCompleted = true;
        UpdateVisuals();
    }
}