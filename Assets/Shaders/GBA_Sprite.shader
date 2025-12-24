Shader "Universal Render Pipeline/2D/GBA Sprite Advanced"
{
    Properties
    {
        // Sprite texture and color (SpriteRenderer uses these names)
        [MainTexture] [NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)

        // === COLOR QUANTIZATION ===
        [Header(Color Quantization)]
        _Levels ("Color Levels", Range(2,64)) = 31
        // Dithering to preserve close shades
        [Toggle] _UseDithering ("Enable Dithering", Float) = 1
        _DitherStrength ("Dither Strength", Range(0,1)) = 0.5
        // Contrast boost before quantization to separate similar colors
        _PreQuantizeContrast ("Pre-Quantize Contrast", Range(0.5, 2.0)) = 1.1

        // === FLASH (hit flash, damage) ===
        [Header(Flash Effect)]
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0

        // === TINT (poison / buff / palette swap) ===
        [Header(Tint Effect)]
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _TintAmount ("Tint Amount", Range(0,1)) = 0
        // Tint blend modes: 0=Replace, 1=Multiply, 2=Additive, 3=Overlay
        [IntRange] _TintMode ("Tint Mode", Range(0,3)) = 0

        // === MOSAIC / PIXELATION ===
        [Header(Mosaic Effect)]
        _MosaicBlocks ("Mosaic Blocks", Range(1,64)) = 1

        // === WOBBLE / WAVE DISTORTION ===
        [Header(Wave Distortion)]
        _WaveSpeed ("Wave Speed", Float) = 4
        _WaveFrequency ("Wave Frequency", Float) = 20
        _WaveAmplitude ("Wave Amplitude", Range(0,0.1)) = 0
        // Vertical wave for different effects
        _WaveAmplitudeY ("Wave Amplitude Y", Range(0,0.1)) = 0

        // === PALETTE SHIFT (cycling colors) ===
        [Header(Palette Effects)]
        _HueShift ("Hue Shift", Range(0,1)) = 0
        _Saturation ("Saturation", Range(0,2)) = 1
        _Brightness ("Brightness", Range(0,2)) = 1

        // === OUTLINE (sprite outline for selection/highlight) ===
        [Header(Outline)]
        [Toggle] _UseOutline ("Enable Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness", Range(0,0.1)) = 0.01

        // === SCANLINES (CRT effect) ===
        [Header(Scanlines)]
        [Toggle] _UseScanlines ("Enable Scanlines", Float) = 0
        _ScanlineIntensity ("Scanline Intensity", Range(0,1)) = 0.2
        _ScanlineCount ("Scanline Count", Range(10,500)) = 160

        // === CHROMATIC ABERRATION ===
        [Header(Chromatic Aberration)]
        _ChromaOffset ("Chroma Offset", Range(0,0.02)) = 0

        // === SHADOW / DROP SHADOW ===
        [Header(Shadow)]
        [Toggle] _UseShadow ("Enable Shadow", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _ShadowOffset ("Shadow Offset", Vector) = (0.02, -0.02, 0, 0)

        // === FADE / DISSOLVE ===
        [Header(Fade and Dissolve)]
        _FadeAmount ("Fade Amount", Range(0,1)) = 1
        [Toggle] _UseDissolve ("Enable Dissolve", Float) = 0
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0,0.1)) = 0.02
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1,0.5,0,1)

        // === UV SCROLLING ===
        [Header(UV Scrolling)]
        _ScrollSpeedX ("Scroll Speed X", Float) = 0
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0
        _ScrollOffsetX ("Scroll Offset X", Float) = 0
        _ScrollOffsetY ("Scroll Offset Y", Float) = 0

        // === SPRITE RENDERER INTERNALS ===
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaCutoff("AlphaCutoff", Range(0,1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GBA_SPRITE_ADVANCED"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // === TEXTURES & SAMPLERS ===
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // === PROPERTIES ===
            float4 _Color;
            float4 _RendererColor;
            float4 _Flip;

            // Quantization
            float _Levels;
            float _UseDithering;
            float _DitherStrength;
            float _PreQuantizeContrast;

            // Flash
            float4 _FlashColor;
            float _FlashAmount;

            // Tint
            float4 _TintColor;
            float _TintAmount;
            float _TintMode;

            // Mosaic
            float _MosaicBlocks;

            // Wave
            float _WaveSpeed;
            float _WaveFrequency;
            float _WaveAmplitude;
            float _WaveAmplitudeY;

            // Palette
            float _HueShift;
            float _Saturation;
            float _Brightness;

            // Outline
            float _UseOutline;
            float4 _OutlineColor;
            float _OutlineThickness;

            // Scanlines
            float _UseScanlines;
            float _ScanlineIntensity;
            float _ScanlineCount;

            // Chromatic
            float _ChromaOffset;

            // Shadow
            float _UseShadow;
            float4 _ShadowColor;
            float4 _ShadowOffset;

            // Fade/Dissolve
            float _FadeAmount;
            float _UseDissolve;
            float _DissolveAmount;
            float _DissolveEdgeWidth;
            float4 _DissolveEdgeColor;

            // UV Scrolling
            float _ScrollSpeedX;
            float _ScrollSpeedY;
            float _ScrollOffsetX;
            float _ScrollOffsetY;

            float _AlphaCutoff;

            // === HELPER FUNCTIONS ===

            // Bayer 4x4 dithering matrix
            float GetBayerDither(float2 pos)
            {
                const float bayerMatrix[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                int x = int(fmod(pos.x, 4.0));
                int y = int(fmod(pos.y, 4.0));
                return bayerMatrix[y * 4 + x] - 0.5; // Center around 0
            }

            // RGB to HSV
            float3 RGBtoHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB
            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Simple noise for dissolve
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // === VERTEX SHADER ===
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Flip handling
                float3 positionOS = IN.positionOS.xyz;
                positionOS.x *= _Flip.x;
                positionOS.y *= _Flip.y;

                OUT.positionHCS = TransformObjectToHClip(positionOS);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);

                return OUT;
            }

            // === FRAGMENT SHADER ===
            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // === UV SCROLLING ===
                // Apply time-based scrolling plus manual offset
                float2 scrollOffset;
                scrollOffset.x = _Time.y * _ScrollSpeedX + _ScrollOffsetX;
                scrollOffset.y = _Time.y * _ScrollSpeedY + _ScrollOffsetY;
                uv = frac(uv + scrollOffset); // frac ensures seamless tiling

                // === MOSAIC ===
                if (_MosaicBlocks > 1.0)
                {
                    float blocks = _MosaicBlocks;
                    uv = floor(uv * blocks) / blocks;
                }

                // === WAVE DISTORTION ===
                if (_WaveAmplitude > 0.0 || _WaveAmplitudeY > 0.0)
                {
                    float waveX = sin(_Time.y * _WaveSpeed + uv.y * _WaveFrequency) * _WaveAmplitude;
                    float waveY = sin(_Time.y * _WaveSpeed * 0.7 + uv.x * _WaveFrequency * 1.3) * _WaveAmplitudeY;
                    uv.x += waveX;
                    uv.y += waveY;
                }

                // === SHADOW (sample behind) ===
                float4 shadowCol = float4(0,0,0,0);
                if (_UseShadow > 0.5)
                {
                    float2 shadowUV = uv - _ShadowOffset.xy;
                    float4 shadowTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUV);
                    shadowCol = float4(_ShadowColor.rgb, shadowTex.a * _ShadowColor.a);
                }

                // === OUTLINE (check neighboring pixels) ===
                float outlineAlpha = 0.0;
                if (_UseOutline > 0.5)
                {
                    float2 offsets[8] = {
                        float2(-1, 0), float2(1, 0), float2(0, -1), float2(0, 1),
                        float2(-1,-1), float2(1,-1), float2(-1, 1), float2(1, 1)
                    };
                    
                    float centerAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                    
                    for (int i = 0; i < 8; i++)
                    {
                        float2 sampleUV = uv + offsets[i] * _OutlineThickness;
                        float sampleAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).a;
                        outlineAlpha = max(outlineAlpha, sampleAlpha);
                    }
                    
                    // Outline only where center is transparent but neighbors aren't
                    outlineAlpha = outlineAlpha * (1.0 - step(0.01, centerAlpha));
                }

                // === CHROMATIC ABERRATION ===
                float4 texCol;
                if (_ChromaOffset > 0.0)
                {
                    float r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(_ChromaOffset, 0)).r;
                    float g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).g;
                    float b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(_ChromaOffset, 0)).b;
                    float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                    texCol = float4(r, g, b, a);
                }
                else
                {
                    texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                }

                // Apply vertex color
                float4 col = texCol * IN.color;

                // === DISSOLVE ===
                if (_UseDissolve > 0.5 && _DissolveAmount > 0.0)
                {
                    float noise = Hash(uv * 100.0);
                    float dissolveEdge = smoothstep(_DissolveAmount - _DissolveEdgeWidth, _DissolveAmount, noise);
                    
                    // Edge glow
                    float edgeGlow = smoothstep(_DissolveAmount - _DissolveEdgeWidth, _DissolveAmount, noise) 
                                   - smoothstep(_DissolveAmount, _DissolveAmount + _DissolveEdgeWidth, noise);
                    col.rgb = lerp(col.rgb, _DissolveEdgeColor.rgb, edgeGlow);
                    
                    // Clip dissolved pixels
                    if (noise < _DissolveAmount)
                        discard;
                }

                // Early alpha clip
                clip(col.a - _AlphaCutoff);

                // === PALETTE ADJUSTMENTS (HSV) ===
                float3 hsv = RGBtoHSV(col.rgb);
                hsv.x = frac(hsv.x + _HueShift); // Hue shift
                hsv.y *= _Saturation;             // Saturation
                hsv.z *= _Brightness;             // Value/Brightness
                col.rgb = HSVtoRGB(hsv);

                // === PRE-QUANTIZE CONTRAST ===
                // Boost contrast before quantization to separate similar colors
                float3 rgb = col.rgb;
                rgb = (rgb - 0.5) * _PreQuantizeContrast + 0.5;
                rgb = saturate(rgb);

                // === GBA COLOR QUANTIZATION WITH DITHERING ===
                float levels = max(_Levels, 2.0);

                if (_UseDithering > 0.5)
                {
                    // Get screen-space position for consistent dithering
                    float2 ditherPos = screenUV * _ScreenParams.xy;
                    float dither = GetBayerDither(ditherPos) * _DitherStrength;
                    
                    // Add dither before quantization
                    rgb += dither / levels;
                }

                // Quantize with rounding instead of floor for better distribution
                rgb = floor(rgb * levels + 0.5) / levels;
                rgb = saturate(rgb);

                // === TINT ===
                if (_TintAmount > 0.0)
                {
                    float3 tinted;
                    
                    if (_TintMode < 0.5)
                    {
                        // Replace mode
                        tinted = _TintColor.rgb;
                    }
                    else if (_TintMode < 1.5)
                    {
                        // Multiply mode (good for shadows/darkness)
                        tinted = rgb * _TintColor.rgb;
                    }
                    else if (_TintMode < 2.5)
                    {
                        // Additive mode (good for glow/buff)
                        tinted = rgb + _TintColor.rgb;
                    }
                    else
                    {
                        // Overlay mode (preserves contrast)
                        float3 overlay;
                        overlay.r = rgb.r < 0.5 ? 2.0 * rgb.r * _TintColor.r : 1.0 - 2.0 * (1.0 - rgb.r) * (1.0 - _TintColor.r);
                        overlay.g = rgb.g < 0.5 ? 2.0 * rgb.g * _TintColor.g : 1.0 - 2.0 * (1.0 - rgb.g) * (1.0 - _TintColor.g);
                        overlay.b = rgb.b < 0.5 ? 2.0 * rgb.b * _TintColor.b : 1.0 - 2.0 * (1.0 - rgb.b) * (1.0 - _TintColor.b);
                        tinted = overlay;
                    }
                    
                    rgb = lerp(rgb, tinted, _TintAmount);
                }

                // === FLASH ===
                rgb = lerp(rgb, _FlashColor.rgb, _FlashAmount);

                // === SCANLINES ===
                if (_UseScanlines > 0.5)
                {
                    float scanline = sin(screenUV.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                    scanline = pow(scanline, 2.0); // Sharpen
                    rgb *= lerp(1.0, scanline, _ScanlineIntensity);
                }

                // === COMPOSE FINAL COLOR ===
                float4 finalCol = float4(rgb, col.a * _FadeAmount);

                // Blend outline underneath
                if (_UseOutline > 0.5 && outlineAlpha > 0.0)
                {
                    float4 outlineResult = float4(_OutlineColor.rgb, outlineAlpha * _OutlineColor.a);
                    finalCol = lerp(outlineResult, finalCol, finalCol.a);
                    finalCol.a = max(outlineAlpha * _OutlineColor.a, finalCol.a);
                }

                // Blend shadow underneath
                if (_UseShadow > 0.5 && shadowCol.a > 0.0)
                {
                    float4 withShadow;
                    withShadow.rgb = lerp(shadowCol.rgb, finalCol.rgb, finalCol.a);
                    withShadow.a = max(shadowCol.a, finalCol.a);
                    finalCol = withShadow;
                }

                return finalCol;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
