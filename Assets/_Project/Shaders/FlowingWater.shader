// A stream running out of the island's lake and spilling over the edge as a waterfall into space.
// Mesh made by PrototypeArtSwap: uv.x = across (0..1), uv.y = metres along the flow, uv2.x = 0..1 along the
// whole path (for _Fill, how far the water has run), vertex colour r = waterfall amount, a = opacity.
// Transparent, URP, single-pass stereo ready (Quest).
Shader "SpaceBetweenUs/FlowingWater"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.10, 0.42, 0.62, 1)
        _ShallowColor ("Shallow", Color) = (0.35, 0.80, 0.90, 1)
        _FoamColor ("Foam", Color) = (0.90, 0.98, 1, 1)
        _Speed ("Flow Speed", Float) = 0.35
        _Tiling ("Streak Tiling", Float) = 14
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        _Fill ("Fill (0 dry .. 1 flowing)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _FoamColor;
                float _Speed, _Tiling, _Opacity, _Fill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float progress : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.progress = v.uv2.x;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float fall = i.color.r;
                float across = abs(i.uv.x - 0.5) * 2.0;                     // 0 middle .. 1 bank
                float speed = _Speed * (1.0 + fall * 3.0);                  // falls faster than it flows
                float along = i.uv.y * _Tiling - _Time.y * speed * _Tiling;
                // long streaks drifting downstream, two layers
                float s1 = noise(float2(i.uv.x * 6.0, along * 0.35));
                float s2 = noise(float2(i.uv.x * 11.0 + 3.1, along * 0.7 + 5.0));
                float streak = s1 * 0.6 + s2 * 0.4;

                half3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(across * 0.8 + streak * 0.4));
                // foam: along the banks, on bright streaks, and much more in the waterfall
                float foam = smoothstep(0.75, 1.0, across) * 0.6 + smoothstep(0.62, 0.85, streak) * (0.35 + fall * 0.65);
                col = lerp(col, _FoamColor.rgb, saturate(foam));

                float bank = smoothstep(1.0, 0.8, across);                  // soft edges
                float front = smoothstep(_Fill, _Fill - 0.04, i.progress); // water only as far as it has run
                float a = _Opacity * bank * front * i.color.a * lerp(1.0, 0.75 + streak * 0.4, fall);
                return half4(col, saturate(a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
