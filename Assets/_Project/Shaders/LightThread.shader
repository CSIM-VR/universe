// Soft glowing thread for the step-6 reveal (LineRenderer, Texture Mode = Stretch).
// Bright thin core + wide soft halo across the width, glowing beads that flow from the island (uv.x = 0)
// towards the asteroid (uv.x = 1), faded at both ends. Colour comes from the LineRenderer gradient * _Color.
// Additive, no depth write, single-pass stereo ready (Quest).
Shader "SpaceBetweenUs/LightThread"
{
    Properties
    {
        _Color ("Tint (HDR)", Color) = (1.4, 1.2, 0.8, 1)
        _Beads ("Light waves along the thread", Float) = 3
        _FlowSpeed ("Flow Speed", Float) = 0.12
        _CoreWidth ("Core Width", Range(0.02, 0.5)) = 0.22
        _GlowWidth ("Glow Width", Range(0.05, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
                float _Beads, _FlowSpeed, _CoreWidth, _GlowWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float across = abs(i.uv.y - 0.5) * 2.0;                       // 0 centre .. 1 edge
                // soft like the slot halos: gaussian glow, no hard core line
                half core = exp(-pow(across / _CoreWidth, 2.0)) * 0.55;
                half glow = exp(-pow(across / _GlowWidth, 2.0)) * 0.45;
                // gentle waves of light drifting outwards + slow breathing
                half wave = 0.5 + 0.5 * sin((i.uv.x * _Beads - _Time.y * _FlowSpeed * _Beads) * 6.2831853);
                half breathe = 0.8 + 0.2 * sin(_Time.y * 1.6);
                half ends = smoothstep(0.0, 0.08, i.uv.x) * smoothstep(1.0, 0.8, i.uv.x);
                half k = (core + glow) * (0.65 + 0.35 * wave) * breathe * ends;
                half3 col = _Color.rgb * i.color.rgb;
                return half4(col, saturate(k * i.color.a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
