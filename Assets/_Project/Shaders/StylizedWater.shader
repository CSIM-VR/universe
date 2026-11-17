// Small stylized lake for the island prototype (URP, Quest friendly: no depth / opaque texture, single-pass stereo).
// Made for Unity's cylinder mesh (radius 0.5 in object space): deep colour in the middle, lighter towards the shore,
// slow drifting ripples that catch the starlight, a sky-tinted fresnel sheen and a soft foam line at the edge.
Shader "SpaceBetweenUs/StylizedWater"
{
    Properties
    {
        _DeepColor ("Deep Colour", Color) = (0.03, 0.16, 0.32, 1)
        _ShallowColor ("Shallow Colour", Color) = (0.16, 0.62, 0.78, 1)
        _SkyColor ("Reflection Tint", Color) = (0.55, 0.62, 0.95, 1)
        _FoamColor ("Shore Foam", Color) = (0.85, 0.97, 1, 1)
        _Alpha ("Opacity", Range(0, 1)) = 0.82
        _RippleScale ("Ripple Scale", Float) = 9
        _RippleSpeed ("Ripple Speed", Float) = 0.35
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.45
        _Sparkle ("Highlight Strength", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _SkyColor, _FoamColor;
                half _Alpha, _RippleStrength, _Sparkle;
                float _RippleScale, _RippleSpeed;
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
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
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

            // cheap smooth value noise
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Height(float2 p, float t)
            {
                return Noise(p + float2(t, t * 0.6)) * 0.6 + Noise(p * 1.9 - float2(t * 0.8, -t * 0.5)) * 0.4;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float r = saturate(length(i.positionOS.xz) * 2.0);          // 0 centre .. 1 shore
                float3 n = normalize(i.normalWS);
                bool top = n.y > 0.5;

                // ripples -> bumped normal (finite differences in object space so they scale with the lake)
                float t = _Time.y * _RippleSpeed;
                float2 q = i.positionOS.xz * _RippleScale;
                float e = 0.05;
                float h = Height(q, t);
                float dx = Height(q + float2(e, 0), t) - h;
                float dz = Height(q + float2(0, e), t) - h;
                // built directly in world space: the lake is a very flat cylinder, so transforming an object-space
                // normal through its (tiny) y scale would flatten the ripples away
                float3 bumped = normalize(float3(-dx / e * _RippleStrength * 0.2, 1, -dz / e * _RippleStrength * 0.2));
                n = top ? bumped : n;

                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sun = GetMainLight();

                // deep in the middle, lighter at the shore
                half3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, smoothstep(0.0, 1.0, r * r));
                // sky sheen at grazing angles
                half fresnel = pow(1.0 - saturate(dot(n, viewDir)), 4.0);
                col = lerp(col, _SkyColor.rgb, fresnel * 0.7);
                // starlight glints on the ripples
                float3 hv = normalize(sun.direction + viewDir);
                half spec = pow(saturate(dot(n, hv)), 180.0) * _Sparkle;
                col += sun.color * spec;
                // soft foam line at the shore, broken up by the ripples
                half foam = smoothstep(0.8, 0.97, r) * (0.55 + 0.45 * h);
                col = lerp(col, _FoamColor.rgb, foam * 0.8);
                // gentle lift from the ambient light so it never goes black
                col += _ShallowColor.rgb * 0.08;

                half alpha = top ? saturate(lerp(_Alpha, 1.0, max(foam, fresnel * 0.5)) + spec) : _Alpha;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
