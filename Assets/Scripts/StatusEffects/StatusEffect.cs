using UnityEngine;

public abstract class StatusEffect : ScriptableObject
{
    public string EffectName;
    public float Duration = -1f; // -1 for permanent until cleared
    public Sprite EffectIcon;
    public GameObject EffectParticlePrefab;
    
    public abstract void OnApplied(BlockStatus block);
    public abstract void OnUpdate(BlockStatus block, float deltaTime);
    public abstract void OnRemoved(BlockStatus block);
    
    // Movement modifiers
    public virtual float GetFallSpeedMultiplier() => 1f;
    public virtual bool CanBeSwapped() => true;
    public virtual bool BlocksMatching() => false;
}