// "Hand of light": almost clear in the middle, glowing at the silhouette (fresnel), with a slow shimmer
// running up the hand. A depth-only pass first so only the outer surface glows (no see-through fingers).
// Additive, URP, single-pass stereo ready (Quest). Works on skinned meshes.
Shader "SpaceBetweenUs/GhostHand"
{
    Properties
    {
        _Color ("Glow Colour (HDR)", Color) = (1.6, 1.15, 0.45, 1)
        _Fill ("Inner Fill", Range(0, 1)) = 0.12
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 3)) = 1.4
        _Shimmer ("Shimmer", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "DepthPrime"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fill, _RimPower, _RimStrength, _Shimmer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.positionOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0 - saturate(abs(dot(n, viewDir))), _RimPower) * _RimStrength;
                // soft band of light drifting from the wrist to the fingertips (bones run along object X)
                half band = 0.5 + 0.5 * sin(i.positionOS.x * 60.0 - _Time.y * 2.5);
                half k = saturate(_Fill + rim) * (1.0 + _Shimmer * (band - 0.5));
                return half4(_Color.rgb, saturate(k));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
