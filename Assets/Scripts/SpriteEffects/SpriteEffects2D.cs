using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffects2D : MonoBehaviour
{
    private int flashAmountID;
    private int flashColorID;

    private Coroutine flashCoroutine;
    private float flashDuration;

    private float flashTimer;
    private Material mat;
    private int mosaicBlocksID;
    private SpriteRenderer sr;
    private int tintAmountID;
    private int tintColorID;
    private int waveAmpID;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mat = sr.material; // instanced for this renderer

        flashAmountID = Shader.PropertyToID("_FlashAmount");
        flashColorID = Shader.PropertyToID("_FlashColor");
        tintAmountID = Shader.PropertyToID("_TintAmount");
        tintColorID = Shader.PropertyToID("_TintColor");
        mosaicBlocksID = Shader.PropertyToID("_MosaicBlocks");
        waveAmpID = Shader.PropertyToID("_WaveAmplitude");
    }

    private void Update()
    {
        // fade hit flash back to 0
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            var t = Mathf.Clamp01(flashTimer / flashDuration);
            mat.SetFloat(flashAmountID, t);
        }
    }

    // --- Public helpers ---

    public void ApplyFlash(Color flashColor, float duration = 0.08f,
        bool loop = false, float loopDuration = 1f)
    {
        // optional: stop any previous flash loop first
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        flashCoroutine = StartCoroutine(FlashRoutine(flashColor, duration, loop, loopDuration));
    }

    public void StopFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        mat.SetFloat(flashAmountID, 0f);
        flashTimer = 0f;
    }

    private IEnumerator FlashRoutine(Color flashColor, float duration, bool loop, float loopDuration)
    {
        var timer = 0f;

        while (loop && timer < loopDuration)
        {
            flashDuration = duration;
            flashTimer = duration;
            mat.SetColor(flashColorID, flashColor);
            mat.SetFloat(flashAmountID, 1f);

            yield return new WaitForSeconds(duration + 0.1f);
            timer += duration;
        }

        // make sure it ends turned off
        mat.SetFloat(flashAmountID, 0f);
        flashCoroutine = null;
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

    private IEnumerator MosaicRoutine(float blocks, float duration)
    {
        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var lerp = 1f - t / duration; // 1 -> 0
            var currentBlocks = Mathf.Lerp(1f, blocks, lerp);
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