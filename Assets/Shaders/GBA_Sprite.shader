Shader "Universal Render Pipeline/2D/GBA Sprite"
{
    Properties
    {
        // Sprite texture and color (SpriteRenderer uses these names)
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1,1,1,1)

        // GBA-ish color levels (approx 5-bit per channel)
        _Levels ("Color Levels", Range(2,64)) = 31

        // Flash (hit flash etc.)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0

        // Tint (poison / buff / palette swap)
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _TintAmount ("Tint Amount", Range(0,1)) = 0

        // Mosaic / blocky pixelation
        _MosaicBlocks ("Mosaic Blocks", Range(1,64)) = 1

        // Wobble / heat distortion
        _WaveSpeed ("Wave Speed", Float) = 4
        _WaveFrequency ("Wave Frequency", Float) = 20
        _WaveAmplitude ("Wave Amplitude", Range(0,0.1)) = 0

        // Required for SpriteRenderer (sorting, etc.)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaCutoff("AlphaCutoff", Range(0,1)) = 0.5
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
            Name "GBA_SPRITE_UNLIT"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // TEXTURES & SAMPLERS
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // PROPERTIES
            float4 _MainTex_ST;
            float4 _Color;
            float4 _RendererColor;
            float4 _Flip;

            float _Levels;
            float4 _FlashColor;
            float _FlashAmount;

            float4 _TintColor;
            float _TintAmount;

            float _MosaicBlocks;

            float _WaveSpeed;
            float _WaveFrequency;
            float _WaveAmplitude;
            float _AlphaCutoff;

            // VERTEX INPUT / OUTPUT
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Flip handling (used by SpriteRenderer for flipping)
                float3 positionOS = IN.positionOS.xyz;
                positionOS.x *= _Flip.x;
                positionOS.y *= _Flip.y;

                float4 positionWS = float4(positionOS, 1.0);
                OUT.positionHCS = TransformObjectToHClip(positionWS.xyz);

                OUT.uv = IN.texcoord; // we keep UV simple, no tiling/offset here
                OUT.color = IN.color * _Color * _RendererColor;

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // --- UV EFFECTS: MOSAIC + WOBBLE ---

                float2 uv = IN.uv;

                // Mosaic (blocky pixels for death bursts etc.)
                if (_MosaicBlocks > 1.0)
                {
                    float blocks = _MosaicBlocks;
                    uv = floor(uv * blocks) / blocks;
                }

                // Wobble (heat / water / ghost shimmer)
                if (_WaveAmplitude > 0.0)
                {
                    // _Time.y is time in seconds
                    float wave = sin(_Time.y * _WaveSpeed + uv.y * _WaveFrequency) * _WaveAmplitude;
                    uv.x += wave;
                }

                // Sample texture
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Apply vertex / renderer color tint
                float4 col = texCol * IN.color;

                // Discard transparent pixels early
                clip(col.a - _AlphaCutoff);

                // --- GBA-STYLE COLOR QUANTIZATION ---

                float3 rgb = col.rgb;
                float levels = max(_Levels, 2.0); // avoid divide-by-zero

                rgb = floor(rgb * levels) / levels;

                // --- TINT (palette-ish recolor) ---
                // 0 = no tint, 1 = full TintColor
                rgb = lerp(rgb, _TintColor.rgb, _TintAmount);

                // --- FLASH (hit flash / damage) ---
                // 0 = no flash, 1 = full FlashColor
                rgb = lerp(rgb, _FlashColor.rgb, _FlashAmount);

                float4 finalCol = float4(rgb, col.a);
                return finalCol;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
