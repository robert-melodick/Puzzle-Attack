using UnityEngine;

[CreateAssetMenu(fileName = "FrozenEffect", menuName = "StatusEffects/Frozen")]
public class FrozenEffect : StatusEffect
{
    public override void OnApplied(BlockStatus block)
    {
        // Spawn ice visual overlay
        var particles = Instantiate(EffectParticlePrefab, block.transform);
        // Store reference if needed for cleanup
    }
    
    public override float GetFallSpeedMultiplier() => 1f;
    public override bool CanBeSwapped() => true; 
    
    public override void OnUpdate(BlockStatus block, float deltaTime)
    {
        // Maybe slowly crack the ice visual
    }
    
    public override void OnRemoved(BlockStatus block)
    {
        // Shatter animation
    }
}