// The map's chunks, lit by URP 2D lights like any sprite. Where the painter tagged a pixel with a
// ground material (alpha below 1: 255 - id), the pixel's colour is only light and shade (grey 128 =
// as lit), and the sharp baked material, repeating in world space, supplies the surface.
Shader "OSistema/MapLit"
{
    Properties
    {
        _MainTex("Painted map", 2D) = "white" {}
        _Mat1("Asphalt", 2D) = "grey" {}
        _Mat2("Basoli", 2D) = "grey" {}
        _Mat3("Kerb east-west", 2D) = "grey" {}
        _Mat4("Kerb north-south", 2D) = "grey" {}
        _Mat5("Slabs", 2D) = "grey" {}
        _Period("Cells per repeat", Float) = 8
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                float2 world : TEXCOORD2;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_TexelSize;
            TEXTURE2D(_Mat1); SAMPLER(sampler_Mat1);
            TEXTURE2D(_Mat2);
            TEXTURE2D(_Mat3);
            TEXTURE2D(_Mat4);
            TEXTURE2D(_Mat5);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Period;
            CBUFFER_END

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.world = TransformObjectToWorld(v.positionOS).xy;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = v.color * _Color;
                return o;
            }

            half3 Ground(int id, float2 w)
            {
                float2 uv = w / _Period;
                // One sampler for all: same settings (repeat, trilinear).
                if (id == 1) return SAMPLE_TEXTURE2D(_Mat1, sampler_Mat1, uv).rgb;
                if (id == 2) return SAMPLE_TEXTURE2D(_Mat2, sampler_Mat1, uv).rgb;
                if (id == 3) return SAMPLE_TEXTURE2D(_Mat3, sampler_Mat1, uv).rgb;
                if (id == 4) return SAMPLE_TEXTURE2D(_Mat4, sampler_Mat1, uv).rgb;
                return SAMPLE_TEXTURE2D(_Mat5, sampler_Mat1, uv).rgb;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                int id = (int)round((1.0 - c.a) * 255.0);
                if (id > 0 && id < 16)
                {
                    c.rgb = Ground(id, i.world) * c.rgb * 2.0;
                    c.a = 1.0;
                }
                c *= i.color;

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(c.rgb, c.a, half4(1, 1, 1, 1), half3(0, 0, 1), surfaceData);
                InitializeInputData(i.uv, i.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }
    }
}
