using UnityEngine;

public class BlockStatus : MonoBehaviour
{
    public StatusEffect CurrentEffect { get; private set; }
    private float _effectTimer;
    
    public void ApplyEffect(StatusEffect effect)
    {
        // Handle effect stacking/replacement logic
        CurrentEffect = effect;
        _effectTimer = effect.Duration;
        effect.OnApplied(this);
    }
    
    public void ClearEffect()
    {
        CurrentEffect?.OnRemoved(this);
        CurrentEffect = null;
    }
    
    void Update()
    {
        if (CurrentEffect != null)
        {
            _effectTimer -= Time.deltaTime;
            CurrentEffect.OnUpdate(this, Time.deltaTime);
            
            if (_effectTimer <= 0)
                ClearEffect();
        }
    }
}