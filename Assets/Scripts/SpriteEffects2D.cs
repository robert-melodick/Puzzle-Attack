using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffects2D : MonoBehaviour
{
    SpriteRenderer sr;
    Material mat;

    int flashAmountID;
    int flashColorID;
    int tintAmountID;
    int tintColorID;
    int mosaicBlocksID;
    int waveAmpID;

    float flashTimer;
    float flashDuration;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mat = sr.material; // instanced for this renderer

        flashAmountID = Shader.PropertyToID("_FlashAmount");
        flashColorID  = Shader.PropertyToID("_FlashColor");
        tintAmountID  = Shader.PropertyToID("_TintAmount");
        tintColorID   = Shader.PropertyToID("_TintColor");
        mosaicBlocksID = Shader.PropertyToID("_MosaicBlocks");
        waveAmpID     = Shader.PropertyToID("_WaveAmplitude");
    }

    void Update()
    {
        // fade hit flash back to 0
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(flashTimer / flashDuration);
            mat.SetFloat(flashAmountID, t);
        }
    }

    // --- Public helpers ---

    public void ApplyFlash(Color flashColor, float duration = 0.08f)
    {
        flashDuration = duration;
        flashTimer = duration;
        mat.SetColor(flashColorID, flashColor);
        mat.SetFloat(flashAmountID, 1f);
    }

    public void SetTint(Color tint, float amount)
    {
        mat.SetColor(tintColorID, tint);
        mat.SetFloat(tintAmountID, amount);
    }

    public void MosaicBurst(float blocks = 16f, float duration = 0.25f)
    {
        StartCoroutine(MosaicRoutine(blocks, duration));
    }

    System.Collections.IEnumerator MosaicRoutine(float blocks, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = 1f - (t / duration);  // 1 -> 0
            float currentBlocks = Mathf.Lerp(1f, blocks, lerp);
            mat.SetFloat(mosaicBlocksID, currentBlocks);
            yield return null;
        }
        mat.SetFloat(mosaicBlocksID, 1f);
    }

    public void SetWobble(bool enabled, float amplitude = 0.01f)
    {
        mat.SetFloat(waveAmpID, enabled ? amplitude : 0f);
    }
}
