Shader "Puzzle/Color"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.4
        _Metallic ("Metallic", Range(0,1)) = 0.0
        [HDR]_EmissionColor ("Emission Color", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags{ "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags{ "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half4 _EmissionColor;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 l = normalize(half3(0.35, 0.85, 0.2));
                half ndl = saturate(dot(n, l));
                half3 diffuse = _BaseColor.rgb * (0.35h + 0.65h * ndl);

                half3 v = half3(0, 0, 1);
                half3 h = normalize(l + v);
                half spec = pow(saturate(dot(n, h)), lerp(8.0h, 64.0h, _Smoothness));
                half3 specCol = lerp(half3(0.04, 0.04, 0.04), _BaseColor.rgb, _Metallic) * spec * 0.35h;

                half3 col = diffuse + specCol + _EmissionColor.rgb;
                return half4(col, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags{ "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed _Smoothness;
            fixed _Metallic;
            fixed4 _EmissionColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 nrm : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.nrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 n = normalize(i.nrm);
                fixed3 l = normalize(fixed3(0.35, 0.85, 0.2));
                fixed ndl = saturate(dot(n, l));
                fixed3 diffuse = _BaseColor.rgb * (0.35 + 0.65 * ndl);

                fixed3 v = fixed3(0, 0, 1);
                fixed3 h = normalize(l + v);
                fixed spec = pow(saturate(dot(n, h)), lerp(8, 64, _Smoothness));
                fixed3 specCol = lerp(fixed3(0.04, 0.04, 0.04), _BaseColor.rgb, _Metallic) * spec * 0.35;

                fixed3 col = diffuse + specCol + _EmissionColor.rgb;
                return fixed4(col, _BaseColor.a);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
