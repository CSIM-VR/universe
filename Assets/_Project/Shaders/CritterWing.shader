// Little procedural creatures for the islands (no textures): butterfly wings, dragonfly wings and bodies.
// Draw on a unit quad with uv 0..1; u = 0 at the body (wing root), u = 1 at the wing tip; v runs along the body.
// _Shape: 0 = butterfly wing (round, two lobes, coloured with dark edge and a spot), 1 = dragonfly wing (long, clear,
// faint veins), 2 = body (thin rounded stick). Alpha blended, double sided, unlit with a gentle self glow so they read
// in the dark space scene. URP, Quest single-pass stereo ready.
Shader "SpaceBetweenUs/CritterWing"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.7, 0.3, 1)
        _Edge ("Edge Colour", Color) = (0.12, 0.08, 0.1, 1)
        _Shape ("Shape (0 butterfly, 1 dragonfly, 2 body)", Float) = 0
        _Glow ("Glow", Range(0, 2)) = 0.6
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
                half4 _Color, _Edge;
                half _Shape, _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float u = i.uv.x, w = i.uv.y - 0.5;
                half3 col;
                half a;
                if (_Shape < 0.5)
                {
                    // butterfly: a big fore lobe and a smaller hind lobe
                    float fore = length(float2((u - 0.52) / 0.5, (w - 0.14) / 0.36));
                    float hind = length(float2((u - 0.36) / 0.36, (w + 0.2) / 0.27));
                    float d = min(fore, hind);
                    a = saturate((1.0 - d) * 14.0);
                    float edge = smoothstep(0.72, 0.9, d);
                    float spot = 1.0 - smoothstep(0.08, 0.14, length(float2(u - 0.68, w - 0.18)));
                    col = lerp(_Color.rgb * (0.75 + 0.35 * u), _Edge.rgb, edge);
                    col = lerp(col, half3(1, 1, 0.95), spot * 0.8);
                }
                else if (_Shape < 1.5)
                {
                    // dragonfly: long narrow clear wing with faint veins
                    float d = length(float2((u - 0.5) / 0.5, w / 0.16));
                    a = saturate((1.0 - d) * 10.0) * 0.55;
                    float vein = 0.5 + 0.5 * sin(u * 70.0) * sin(w * 40.0);
                    col = _Color.rgb * (0.8 + 0.3 * vein);
                    a *= 0.7 + 0.3 * vein;
                }
                else
                {
                    // body: a thin rounded stick along v
                    float d = length(float2((u - 0.5) / 0.5, w / 0.5));
                    a = saturate((1.0 - d) * 8.0);
                    col = _Color.rgb * (0.7 + 0.3 * sin(w * 60.0));
                }
                col *= 1.0 + _Glow;
                return half4(col, a * _Color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
