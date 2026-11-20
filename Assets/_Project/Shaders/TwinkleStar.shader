// Star particles that twinkle: each star gets its own speed and phase from the particle's stable random
// (custom vertex stream: UV0.xy + StableRandom.x in UV0.z, set by StarTwinkle / the art swap).
// Soft round dot drawn procedurally (no texture), a few stars flare now and then. Additive, URP, Quest stereo ready.
Shader "SpaceBetweenUs/TwinkleStar"
{
    Properties
    {
        _Color ("Tint (HDR)", Color) = (1.4, 1.4, 1.6, 1)
        _Twinkle ("Twinkle Amount", Range(0, 1)) = 0.6
        _Speed ("Twinkle Speed", Float) = 1.6
        _Flare ("Flare Chance", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Twinkle, _Speed, _Flare;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float3 uv : TEXCOORD0;      // xy = quad uv, z = stable random 0..1
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = length(i.uv.xy - 0.5) * 2.0;
                half core = saturate(1.0 - r);
                half disc = core * core * core + exp(-r * r * 30.0) * 0.8;     // soft glow + bright centre

                float rnd = i.uv.z;
                float speed = _Speed * (0.4 + rnd * 1.6);
                float phase = rnd * 61.7;
                half tw = 0.5 + 0.5 * sin(_Time.y * speed + phase) * sin(_Time.y * speed * 0.37 + phase * 2.3);
                half bright = lerp(1.0, tw * 1.3, _Twinkle);
                // a few stars (by random) flare briefly every few seconds
                half flareOn = step(1.0 - _Flare, frac(rnd * 13.37));
                half flare = pow(saturate(sin(_Time.y * 0.7 + phase * 3.0)), 60.0) * 2.5 * flareOn;
                half k = disc * (bright + flare);
                return half4(_Color.rgb * i.color.rgb, saturate(k * i.color.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
