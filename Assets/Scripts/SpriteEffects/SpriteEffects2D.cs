using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffects2D : MonoBehaviour
{
    private int _flashAmountID;
    private int _flashColorID;

    private Coroutine _flashCoroutine;
    private float _flashDuration;

    private float _flashTimer;
    private Material _mat;
    private int _mosaicBlocksID;
    private SpriteRenderer _sr;
    private int _tintAmountID;
    private int _tintColorID;
    private int _waveAmpID;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mat = _sr.material; // instanced for this renderer

        _flashAmountID = Shader.PropertyToID("_FlashAmount");
        _flashColorID = Shader.PropertyToID("_FlashColor");
        _tintAmountID = Shader.PropertyToID("_TintAmount");
        _tintColorID = Shader.PropertyToID("_TintColor");
        _mosaicBlocksID = Shader.PropertyToID("_MosaicBlocks");
        _waveAmpID = Shader.PropertyToID("_WaveAmplitude");
    }

    private void Update()
    {
        // fade hit flash back to 0
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            var t = Mathf.Clamp01(_flashTimer / _flashDuration);
            _mat.SetFloat(_flashAmountID, t);
        }
    }

    // --- Public helpers ---

    public void ApplyFlash(Color flashColor, float duration = 0.08f,
        bool loop = false, float loopDuration = 1f)
    {
        // optional: stop any previous flash loop first
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        _flashCoroutine = StartCoroutine(FlashRoutine(flashColor, duration, loop, loopDuration));
    }

    public void StopFlash()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        _mat.SetFloat(_flashAmountID, 0f);
        _flashTimer = 0f;
    }

    private IEnumerator FlashRoutine(Color flashColor, float duration, bool loop, float loopDuration)
    {
        var timer = 0f;

        while (loop && timer < loopDuration)
        {
            _flashDuration = duration;
            _flashTimer = duration;
            _mat.SetColor(_flashColorID, flashColor);
            _mat.SetFloat(_flashAmountID, 1f);

            yield return new WaitForSeconds(duration + 0.1f);
            timer += duration;
        }

        // make sure it ends turned off
        _mat.SetFloat(_flashAmountID, 0f);
        _flashCoroutine = null;
    }

    public void SetTint(Color tint, float amount)
    {
        _mat.SetColor(_tintColorID, tint);
        _mat.SetFloat(_tintAmountID, amount);
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
            _mat.SetFloat(_mosaicBlocksID, currentBlocks);
            yield return null;
        }

        _mat.SetFloat(_mosaicBlocksID, 1f);
    }

    public void SetWobble(bool enabled, float amplitude = 0.01f)
    {
        _mat.SetFloat(_waveAmpID, enabled ? amplitude : 0f);
    }
}