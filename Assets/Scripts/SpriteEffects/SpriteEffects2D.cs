using System.Collections;
using UnityEngine;

/// <summary>
/// Manages shader effects for sprites using the GBA Sprite Advanced shader.
/// Provides a clean API for all shader properties with cached property IDs.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffects2D : MonoBehaviour
{
    #region Tint Modes

    public enum TintMode
    {
        Replace = 0,
        Multiply = 1,
        Additive = 2,
        Overlay = 3
    }

    #endregion

    #region Private Fields

    private SpriteRenderer _sr;
    private Material _mat;

    // Cached property IDs for performance
    private int _flashAmountID;
    private int _flashColorID;
    private int _tintAmountID;
    private int _tintColorID;
    private int _tintModeID;
    private int _mosaicBlocksID;
    private int _waveSpeedID;
    private int _waveFrequencyID;
    private int _waveAmplitudeID;
    private int _waveAmplitudeYID;
    private int _hueShiftID;
    private int _saturationID;
    private int _brightnessID;
    private int _useOutlineID;
    private int _outlineColorID;
    private int _outlineThicknessID;
    private int _useScanlinesID;
    private int _scanlineIntensityID;
    private int _scanlineCountID;
    private int _chromaOffsetID;
    private int _useShadowID;
    private int _shadowColorID;
    private int _shadowOffsetID;
    private int _fadeAmountID;
    private int _useDissolveID;
    private int _dissolveAmountID;
    private int _dissolveEdgeWidthID;
    private int _dissolveEdgeColorID;
    private int _levelsID;
    private int _useDitheringID;
    private int _ditherStrengthID;
    private int _preQuantizeContrastID;
    private int _scrollSpeedXID;
    private int _scrollSpeedYID;
    private int _scrollOffsetXID;
    private int _scrollOffsetYID;

    // Flash state
    private Coroutine _flashCoroutine;
    private float _flashDuration;
    private float _flashTimer;

    // Dissolve state
    private Coroutine _dissolveCoroutine;

    // Mosaic state
    private Coroutine _mosaicCoroutine;

    // Scroll state
    private Coroutine _scrollCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _mat = _sr.material; // Instanced for this renderer

        CachePropertyIDs();
    }

    private void Update()
    {
        // Fade hit flash back to 0
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            var t = Mathf.Clamp01(_flashTimer / _flashDuration);
            _mat.SetFloat(_flashAmountID, t);
        }
    }

    private void CachePropertyIDs()
    {
        // Flash
        _flashAmountID = Shader.PropertyToID("_FlashAmount");
        _flashColorID = Shader.PropertyToID("_FlashColor");

        // Tint
        _tintAmountID = Shader.PropertyToID("_TintAmount");
        _tintColorID = Shader.PropertyToID("_TintColor");
        _tintModeID = Shader.PropertyToID("_TintMode");

        // Mosaic
        _mosaicBlocksID = Shader.PropertyToID("_MosaicBlocks");

        // Wave/Wobble
        _waveSpeedID = Shader.PropertyToID("_WaveSpeed");
        _waveFrequencyID = Shader.PropertyToID("_WaveFrequency");
        _waveAmplitudeID = Shader.PropertyToID("_WaveAmplitude");
        _waveAmplitudeYID = Shader.PropertyToID("_WaveAmplitudeY");

        // Palette/HSV
        _hueShiftID = Shader.PropertyToID("_HueShift");
        _saturationID = Shader.PropertyToID("_Saturation");
        _brightnessID = Shader.PropertyToID("_Brightness");

        // Outline
        _useOutlineID = Shader.PropertyToID("_UseOutline");
        _outlineColorID = Shader.PropertyToID("_OutlineColor");
        _outlineThicknessID = Shader.PropertyToID("_OutlineThickness");

        // Scanlines
        _useScanlinesID = Shader.PropertyToID("_UseScanlines");
        _scanlineIntensityID = Shader.PropertyToID("_ScanlineIntensity");
        _scanlineCountID = Shader.PropertyToID("_ScanlineCount");

        // Chromatic Aberration
        _chromaOffsetID = Shader.PropertyToID("_ChromaOffset");

        // Shadow
        _useShadowID = Shader.PropertyToID("_UseShadow");
        _shadowColorID = Shader.PropertyToID("_ShadowColor");
        _shadowOffsetID = Shader.PropertyToID("_ShadowOffset");

        // Fade/Dissolve
        _fadeAmountID = Shader.PropertyToID("_FadeAmount");
        _useDissolveID = Shader.PropertyToID("_UseDissolve");
        _dissolveAmountID = Shader.PropertyToID("_DissolveAmount");
        _dissolveEdgeWidthID = Shader.PropertyToID("_DissolveEdgeWidth");
        _dissolveEdgeColorID = Shader.PropertyToID("_DissolveEdgeColor");

        // Quantization
        _levelsID = Shader.PropertyToID("_Levels");
        _useDitheringID = Shader.PropertyToID("_UseDithering");
        _ditherStrengthID = Shader.PropertyToID("_DitherStrength");
        _preQuantizeContrastID = Shader.PropertyToID("_PreQuantizeContrast");

        // UV Scrolling
        _scrollSpeedXID = Shader.PropertyToID("_ScrollSpeedX");
        _scrollSpeedYID = Shader.PropertyToID("_ScrollSpeedY");
        _scrollOffsetXID = Shader.PropertyToID("_ScrollOffsetX");
        _scrollOffsetYID = Shader.PropertyToID("_ScrollOffsetY");
    }

    #endregion

    #region Flash Effects

    /// <summary>
    /// Apply a flash effect that fades out over duration.
    /// </summary>
    public void ApplyFlash(Color flashColor, float duration = 0.08f,
        bool loop = false, float loopDuration = 1f)
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }

        _flashCoroutine = StartCoroutine(FlashRoutine(flashColor, duration, loop, loopDuration));
    }

    /// <summary>
    /// Set flash amount directly (0-1).
    /// </summary>
    public void SetFlashAmount(float amount)
    {
        _mat.SetFloat(_flashAmountID, Mathf.Clamp01(amount));
    }

    /// <summary>
    /// Set flash color.
    /// </summary>
    public void SetFlashColor(Color color)
    {
        _mat.SetColor(_flashColorID, color);
    }

    /// <summary>
    /// Stop any active flash effect.
    /// </summary>
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

        do
        {
            _flashDuration = duration;
            _flashTimer = duration;
            _mat.SetColor(_flashColorID, flashColor);
            _mat.SetFloat(_flashAmountID, 1f);

            yield return new WaitForSeconds(duration + 0.1f);
            timer += duration + 0.1f;
        } while (loop && timer < loopDuration);

        _mat.SetFloat(_flashAmountID, 0f);
        _flashCoroutine = null;
    }

    #endregion

    #region Tint Effects

    /// <summary>
    /// Set tint color and amount.
    /// </summary>
    public void SetTint(Color tint, float amount)
    {
        _mat.SetColor(_tintColorID, tint);
        _mat.SetFloat(_tintAmountID, Mathf.Clamp01(amount));
    }

    /// <summary>
    /// Set tint with specific blend mode.
    /// </summary>
    public void SetTint(Color tint, float amount, TintMode mode)
    {
        _mat.SetColor(_tintColorID, tint);
        _mat.SetFloat(_tintAmountID, Mathf.Clamp01(amount));
        _mat.SetFloat(_tintModeID, (float)mode);
    }

    /// <summary>
    /// Set tint amount only (0-1).
    /// </summary>
    public void SetTintAmount(float amount)
    {
        _mat.SetFloat(_tintAmountID, Mathf.Clamp01(amount));
    }

    /// <summary>
    /// Set tint color only.
    /// </summary>
    public void SetTintColor(Color color)
    {
        _mat.SetColor(_tintColorID, color);
    }

    /// <summary>
    /// Set tint blend mode.
    /// </summary>
    public void SetTintMode(TintMode mode)
    {
        _mat.SetFloat(_tintModeID, (float)mode);
    }

    /// <summary>
    /// Clear tint effect.
    /// </summary>
    public void ClearTint()
    {
        _mat.SetFloat(_tintAmountID, 0f);
    }

    #endregion

    #region Mosaic/Pixelation Effects

    /// <summary>
    /// Trigger a mosaic burst effect that animates from blocks back to normal.
    /// </summary>
    public void MosaicBurst(float blocks = 16f, float duration = 0.25f)
    {
        if (_mosaicCoroutine != null)
        {
            StopCoroutine(_mosaicCoroutine);
        }
        _mosaicCoroutine = StartCoroutine(MosaicRoutine(blocks, duration));
    }

    /// <summary>
    /// Set mosaic blocks directly (1 = no effect, higher = more pixelated).
    /// </summary>
    public void SetMosaic(float blocks)
    {
        _mat.SetFloat(_mosaicBlocksID, Mathf.Max(1f, blocks));
    }

    /// <summary>
    /// Clear mosaic effect.
    /// </summary>
    public void ClearMosaic()
    {
        if (_mosaicCoroutine != null)
        {
            StopCoroutine(_mosaicCoroutine);
            _mosaicCoroutine = null;
        }
        _mat.SetFloat(_mosaicBlocksID, 1f);
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
        _mosaicCoroutine = null;
    }

    #endregion

    #region Wave/Wobble Effects

    /// <summary>
    /// Enable or disable horizontal wobble effect.
    /// </summary>
    public void SetWobble(bool enabled, float amplitude = 0.01f)
    {
        _mat.SetFloat(_waveAmplitudeID, enabled ? amplitude : 0f);
    }

    /// <summary>
    /// Enable or disable vertical wobble effect.
    /// </summary>
    public void SetWobbleVertical(bool enabled, float amplitude = 0.01f)
    {
        _mat.SetFloat(_waveAmplitudeYID, enabled ? amplitude : 0f);
    }

    /// <summary>
    /// Set full wave distortion parameters.
    /// </summary>
    public void SetWaveDistortion(float amplitudeX, float amplitudeY, float speed = 4f, float frequency = 20f)
    {
        _mat.SetFloat(_waveAmplitudeID, amplitudeX);
        _mat.SetFloat(_waveAmplitudeYID, amplitudeY);
        _mat.SetFloat(_waveSpeedID, speed);
        _mat.SetFloat(_waveFrequencyID, frequency);
    }

    /// <summary>
    /// Clear all wave effects.
    /// </summary>
    public void ClearWave()
    {
        _mat.SetFloat(_waveAmplitudeID, 0f);
        _mat.SetFloat(_waveAmplitudeYID, 0f);
    }

    #endregion

    #region Palette/HSV Effects

    /// <summary>
    /// Shift hue (0-1, wraps around).
    /// </summary>
    public void SetHueShift(float shift)
    {
        _mat.SetFloat(_hueShiftID, shift % 1f);
    }

    /// <summary>
    /// Set saturation multiplier (0 = grayscale, 1 = normal, 2 = oversaturated).
    /// </summary>
    public void SetSaturation(float saturation)
    {
        _mat.SetFloat(_saturationID, Mathf.Clamp(saturation, 0f, 2f));
    }

    /// <summary>
    /// Set brightness multiplier (0 = black, 1 = normal, 2 = overbright).
    /// </summary>
    public void SetBrightness(float brightness)
    {
        _mat.SetFloat(_brightnessID, Mathf.Clamp(brightness, 0f, 2f));
    }

    /// <summary>
    /// Set all palette adjustments at once.
    /// </summary>
    public void SetPalette(float hueShift, float saturation, float brightness)
    {
        _mat.SetFloat(_hueShiftID, hueShift % 1f);
        _mat.SetFloat(_saturationID, Mathf.Clamp(saturation, 0f, 2f));
        _mat.SetFloat(_brightnessID, Mathf.Clamp(brightness, 0f, 2f));
    }

    /// <summary>
    /// Reset palette to defaults.
    /// </summary>
    public void ResetPalette()
    {
        _mat.SetFloat(_hueShiftID, 0f);
        _mat.SetFloat(_saturationID, 1f);
        _mat.SetFloat(_brightnessID, 1f);
    }

    /// <summary>
    /// Animate hue cycling over time.
    /// </summary>
    public Coroutine StartHueCycle(float cyclesPerSecond = 0.5f)
    {
        return StartCoroutine(HueCycleRoutine(cyclesPerSecond));
    }

    private IEnumerator HueCycleRoutine(float cyclesPerSecond)
    {
        var hue = 0f;
        while (true)
        {
            hue = (hue + Time.deltaTime * cyclesPerSecond) % 1f;
            _mat.SetFloat(_hueShiftID, hue);
            yield return null;
        }
    }

    #endregion

    #region Outline Effects

    /// <summary>
    /// Enable sprite outline.
    /// </summary>
    public void SetOutline(bool enabled, Color color = default, float thickness = 0.01f)
    {
        _mat.SetFloat(_useOutlineID, enabled ? 1f : 0f);
        if (enabled)
        {
            _mat.SetColor(_outlineColorID, color == default ? Color.black : color);
            _mat.SetFloat(_outlineThicknessID, Mathf.Clamp(thickness, 0f, 0.1f));
        }
    }

    /// <summary>
    /// Set outline color.
    /// </summary>
    public void SetOutlineColor(Color color)
    {
        _mat.SetColor(_outlineColorID, color);
    }

    /// <summary>
    /// Set outline thickness.
    /// </summary>
    public void SetOutlineThickness(float thickness)
    {
        _mat.SetFloat(_outlineThicknessID, Mathf.Clamp(thickness, 0f, 0.1f));
    }

    /// <summary>
    /// Disable outline.
    /// </summary>
    public void ClearOutline()
    {
        _mat.SetFloat(_useOutlineID, 0f);
    }

    #endregion

    #region Scanline Effects

    /// <summary>
    /// Enable CRT-style scanlines.
    /// </summary>
    public void SetScanlines(bool enabled, float intensity = 0.2f, float count = 160f)
    {
        _mat.SetFloat(_useScanlinesID, enabled ? 1f : 0f);
        if (enabled)
        {
            _mat.SetFloat(_scanlineIntensityID, Mathf.Clamp01(intensity));
            _mat.SetFloat(_scanlineCountID, Mathf.Clamp(count, 10f, 500f));
        }
    }

    /// <summary>
    /// Disable scanlines.
    /// </summary>
    public void ClearScanlines()
    {
        _mat.SetFloat(_useScanlinesID, 0f);
    }

    #endregion

    #region Chromatic Aberration

    /// <summary>
    /// Set chromatic aberration offset (0 = disabled).
    /// </summary>
    public void SetChromaticAberration(float offset)
    {
        _mat.SetFloat(_chromaOffsetID, Mathf.Clamp(offset, 0f, 0.02f));
    }

    /// <summary>
    /// Disable chromatic aberration.
    /// </summary>
    public void ClearChromaticAberration()
    {
        _mat.SetFloat(_chromaOffsetID, 0f);
    }

    #endregion

    #region Shadow Effects

    /// <summary>
    /// Enable drop shadow.
    /// </summary>
    public void SetShadow(bool enabled, Color color = default, Vector2 offset = default)
    {
        _mat.SetFloat(_useShadowID, enabled ? 1f : 0f);
        if (enabled)
        {
            _mat.SetColor(_shadowColorID, color == default ? new Color(0, 0, 0, 0.5f) : color);
            var shadowOffset = offset == default ? new Vector2(0.02f, -0.02f) : offset;
            _mat.SetVector(_shadowOffsetID, new Vector4(shadowOffset.x, shadowOffset.y, 0, 0));
        }
    }

    /// <summary>
    /// Disable shadow.
    /// </summary>
    public void ClearShadow()
    {
        _mat.SetFloat(_useShadowID, 0f);
    }

    #endregion

    #region Fade/Dissolve Effects

    /// <summary>
    /// Set fade amount (0 = invisible, 1 = fully visible).
    /// </summary>
    public void SetFade(float amount)
    {
        _mat.SetFloat(_fadeAmountID, Mathf.Clamp01(amount));
    }

    /// <summary>
    /// Animate fade in.
    /// </summary>
    public Coroutine FadeIn(float duration = 0.5f)
    {
        return StartCoroutine(FadeRoutine(0f, 1f, duration));
    }

    /// <summary>
    /// Animate fade out.
    /// </summary>
    public Coroutine FadeOut(float duration = 0.5f)
    {
        return StartCoroutine(FadeRoutine(1f, 0f, duration));
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var progress = t / duration;
            _mat.SetFloat(_fadeAmountID, Mathf.Lerp(from, to, progress));
            yield return null;
        }
        _mat.SetFloat(_fadeAmountID, to);
    }

    /// <summary>
    /// Enable dissolve effect.
    /// </summary>
    public void SetDissolve(bool enabled, float amount = 0f, float edgeWidth = 0.02f, Color edgeColor = default)
    {
        _mat.SetFloat(_useDissolveID, enabled ? 1f : 0f);
        if (enabled)
        {
            _mat.SetFloat(_dissolveAmountID, Mathf.Clamp01(amount));
            _mat.SetFloat(_dissolveEdgeWidthID, Mathf.Clamp(edgeWidth, 0f, 0.1f));
            _mat.SetColor(_dissolveEdgeColorID, edgeColor == default ? new Color(1f, 0.5f, 0f, 1f) : edgeColor);
        }
    }

    /// <summary>
    /// Set dissolve amount directly (0-1).
    /// </summary>
    public void SetDissolveAmount(float amount)
    {
        _mat.SetFloat(_dissolveAmountID, Mathf.Clamp01(amount));
    }

    /// <summary>
    /// Animate dissolve in (from dissolved to solid).
    /// </summary>
    public Coroutine DissolveIn(float duration = 0.5f, Color edgeColor = default)
    {
        if (_dissolveCoroutine != null)
        {
            StopCoroutine(_dissolveCoroutine);
        }
        _dissolveCoroutine = StartCoroutine(DissolveRoutine(1f, 0f, duration, edgeColor));
        return _dissolveCoroutine;
    }

    /// <summary>
    /// Animate dissolve out (from solid to dissolved).
    /// </summary>
    public Coroutine DissolveOut(float duration = 0.5f, Color edgeColor = default)
    {
        if (_dissolveCoroutine != null)
        {
            StopCoroutine(_dissolveCoroutine);
        }
        _dissolveCoroutine = StartCoroutine(DissolveRoutine(0f, 1f, duration, edgeColor));
        return _dissolveCoroutine;
    }

    private IEnumerator DissolveRoutine(float from, float to, float duration, Color edgeColor)
    {
        _mat.SetFloat(_useDissolveID, 1f);
        if (edgeColor != default)
        {
            _mat.SetColor(_dissolveEdgeColorID, edgeColor);
        }

        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var progress = t / duration;
            _mat.SetFloat(_dissolveAmountID, Mathf.Lerp(from, to, progress));
            yield return null;
        }

        _mat.SetFloat(_dissolveAmountID, to);

        // Disable dissolve if fully solid
        if (Mathf.Approximately(to, 0f))
        {
            _mat.SetFloat(_useDissolveID, 0f);
        }

        _dissolveCoroutine = null;
    }

    /// <summary>
    /// Disable dissolve.
    /// </summary>
    public void ClearDissolve()
    {
        if (_dissolveCoroutine != null)
        {
            StopCoroutine(_dissolveCoroutine);
            _dissolveCoroutine = null;
        }
        _mat.SetFloat(_useDissolveID, 0f);
        _mat.SetFloat(_dissolveAmountID, 0f);
    }

    #endregion

    #region Quantization Settings

    /// <summary>
    /// Set GBA color quantization levels (2-64).
    /// </summary>
    public void SetColorLevels(float levels)
    {
        _mat.SetFloat(_levelsID, Mathf.Clamp(levels, 2f, 64f));
    }

    /// <summary>
    /// Enable/disable dithering.
    /// </summary>
    public void SetDithering(bool enabled, float strength = 0.5f)
    {
        _mat.SetFloat(_useDitheringID, enabled ? 1f : 0f);
        if (enabled)
        {
            _mat.SetFloat(_ditherStrengthID, Mathf.Clamp01(strength));
        }
    }

    /// <summary>
    /// Set pre-quantize contrast boost.
    /// </summary>
    public void SetPreQuantizeContrast(float contrast)
    {
        _mat.SetFloat(_preQuantizeContrastID, Mathf.Clamp(contrast, 0.5f, 2f));
    }

    #endregion

    #region UV Scrolling Effects

    /// <summary>
    /// Set continuous UV scrolling speed (units per second).
    /// Positive X scrolls right, positive Y scrolls up.
    /// </summary>
    public void SetScrollSpeed(float speedX, float speedY)
    {
        _mat.SetFloat(_scrollSpeedXID, speedX);
        _mat.SetFloat(_scrollSpeedYID, speedY);
    }

    /// <summary>
    /// Set continuous UV scrolling speed using a Vector2.
    /// </summary>
    public void SetScrollSpeed(Vector2 speed)
    {
        _mat.SetFloat(_scrollSpeedXID, speed.x);
        _mat.SetFloat(_scrollSpeedYID, speed.y);
    }

    /// <summary>
    /// Set manual UV offset (0-1 range, wraps automatically).
    /// Useful for precise positioning or script-driven scrolling.
    /// </summary>
    public void SetScrollOffset(float offsetX, float offsetY)
    {
        _mat.SetFloat(_scrollOffsetXID, offsetX);
        _mat.SetFloat(_scrollOffsetYID, offsetY);
    }

    /// <summary>
    /// Set manual UV offset using a Vector2.
    /// </summary>
    public void SetScrollOffset(Vector2 offset)
    {
        _mat.SetFloat(_scrollOffsetXID, offset.x);
        _mat.SetFloat(_scrollOffsetYID, offset.y);
    }

    /// <summary>
    /// Set both scroll speed and offset at once.
    /// </summary>
    public void SetScroll(Vector2 speed, Vector2 offset)
    {
        _mat.SetFloat(_scrollSpeedXID, speed.x);
        _mat.SetFloat(_scrollSpeedYID, speed.y);
        _mat.SetFloat(_scrollOffsetXID, offset.x);
        _mat.SetFloat(_scrollOffsetYID, offset.y);
    }

    /// <summary>
    /// Enable diagonal scrolling with a single speed value.
    /// Angle in degrees (0 = right, 90 = up, 180 = left, 270 = down).
    /// </summary>
    public void SetScrollAngle(float speed, float angleDegrees)
    {
        float angleRad = angleDegrees * Mathf.Deg2Rad;
        float speedX = Mathf.Cos(angleRad) * speed;
        float speedY = Mathf.Sin(angleRad) * speed;
        SetScrollSpeed(speedX, speedY);
    }

    /// <summary>
    /// Animate scroll offset from current to target over duration.
    /// </summary>
    public Coroutine ScrollTo(Vector2 targetOffset, float duration)
    {
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
        }
        _scrollCoroutine = StartCoroutine(ScrollToRoutine(targetOffset, duration));
        return _scrollCoroutine;
    }

    private IEnumerator ScrollToRoutine(Vector2 targetOffset, float duration)
    {
        Vector2 startOffset = new Vector2(
            _mat.GetFloat(_scrollOffsetXID),
            _mat.GetFloat(_scrollOffsetYID)
        );

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            Vector2 current = Vector2.Lerp(startOffset, targetOffset, progress);
            SetScrollOffset(current);
            yield return null;
        }

        SetScrollOffset(targetOffset);
        _scrollCoroutine = null;
    }

    /// <summary>
    /// Stop continuous scrolling and reset offset.
    /// </summary>
    public void ClearScroll()
    {
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
            _scrollCoroutine = null;
        }
        _mat.SetFloat(_scrollSpeedXID, 0f);
        _mat.SetFloat(_scrollSpeedYID, 0f);
        _mat.SetFloat(_scrollOffsetXID, 0f);
        _mat.SetFloat(_scrollOffsetYID, 0f);
    }

    /// <summary>
    /// Stop continuous scrolling but keep current offset.
    /// </summary>
    public void StopScroll()
    {
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
            _scrollCoroutine = null;
        }
        _mat.SetFloat(_scrollSpeedXID, 0f);
        _mat.SetFloat(_scrollSpeedYID, 0f);
    }

    /// <summary>
    /// Get current scroll speed.
    /// </summary>
    public Vector2 GetScrollSpeed()
    {
        return new Vector2(
            _mat.GetFloat(_scrollSpeedXID),
            _mat.GetFloat(_scrollSpeedYID)
        );
    }

    /// <summary>
    /// Get current scroll offset.
    /// </summary>
    public Vector2 GetScrollOffset()
    {
        return new Vector2(
            _mat.GetFloat(_scrollOffsetXID),
            _mat.GetFloat(_scrollOffsetYID)
        );
    }

    #endregion

    #region Utility

    /// <summary>
    /// Reset all effects to defaults.
    /// </summary>
    public void ResetAllEffects()
    {
        StopFlash();
        ClearTint();
        ClearMosaic();
        ClearWave();
        ResetPalette();
        ClearOutline();
        ClearScanlines();
        ClearChromaticAberration();
        ClearShadow();
        ClearDissolve();
        ClearScroll();
        SetFade(1f);
    }

    /// <summary>
    /// Get the material instance for direct access if needed.
    /// </summary>
    public Material GetMaterial() => _mat;

    #endregion
}