using UnityEngine;

public class SpriteEffectsDebugger : MonoBehaviour
{
    [Header("Debug Targets")] public SpriteEffects2D[] targets;

    [Header("Shader Effect Controls")] [Range(0f, 1f)]
    public float wobbleMod = 0.02f;

    [Range(0f, 32f)] public float mosaicBlocks = 8f;

    public float mosaicDuration = 0.5f;

    [Header("Flash Settings")] public Color flashColor = Color.white;

    public float flashDuration = 0.1f;

    [Header("Tint Settings")] public Color tintColor = Color.magenta;

    public float tintOpacity = 0.5f;

    private void Update()
    {
        // Start Flashing
        if (Input.GetKeyDown(KeyCode.Alpha1))
            foreach (var t in targets)
                if (t != null)
                    t.ApplyFlash(flashColor, flashDuration, true, 3f);

        // Apply Tint (poison color)
        if (Input.GetKeyDown(KeyCode.Alpha2))
            foreach (var t in targets)
                if (t != null)
                    t.SetTint(tintColor, tintOpacity);

        // Toggle Mosaic
        if (Input.GetKeyDown(KeyCode.Alpha3))
            foreach (var t in targets)
                if (t != null)
                    t.MosaicBurst(mosaicBlocks, mosaicDuration);

        // Toggle Wobble
        if (Input.GetKeyDown(KeyCode.Alpha4))
            foreach (var t in targets)
                if (t != null)
                    t.SetWobble(true, wobbleMod);

        // Clear Effects
        if (Input.GetKeyDown(KeyCode.Alpha5))
            foreach (var t in targets)
                if (t != null)
                    ClearAllEffects();
    }

    public void ClearAllEffects()
    {
        foreach (var t in targets)
            if (t != null)
            {
                t.SetTint(Color.white, 0f);
                t.SetWobble(false, 0f);
                t.MosaicBurst(0f, 0.1f);
                t.StopFlash();
            }
    }
}